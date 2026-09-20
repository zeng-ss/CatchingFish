using System.Collections.Generic;
using Config;
using Controller;
using Model;
using Tool;
using UnityEngine;
using View;

namespace Core
{
    public class GameMgr : MonoSingleton<GameMgr>
    {
        [Header("场景引用")] [Tooltip("整体滚动的世界根节点 BG")] [SerializeField] private Transform worldRoot;
        [SerializeField] private HookView hookView;

        [Tooltip("开场是否先播玩法教程（面板点 [开始游戏] 之后才进 Ready）。调试时可以关掉")]
        [SerializeField]
        private bool playTutorial = true;

        private GameConfig _cfg;
        private InputController _input;
        private BGController _bgCtrl;
        private HookController _hookCtrl;
        private FishController _fishCtrl;
        private GameState _state = GameState.Tutorial;
        private GameModel _gModel;
        private bool _tutorialDone;

        private CameraController _camCtrl;
        private bool _camMovedToPlay;
        private bool _camMovedBack;

        /// <summary>
        /// 开场时序：
        ///   ① 点击之后鱼钩先往下走，这一段**镜头和背景都不动**（画面还是开始画面）；
        ///   ② 鱼钩落到"下边界往上 1/5 个屏幕高"的位置时，镜头开始**加速下移**；
        ///   ③ 镜头移动的时长 = 鱼钩从触发点走到 hookMiddleY 的时间，
        ///      所以鱼钩刚好停在游玩位置的那一刻，镜头也刚好到位；
        ///   ④ 那一刻之后 depth 超过 CastDepth，WorldScrollAt 才开始大于 0 —— 背景开始滚动。
        /// </summary>
        public GameConfig Config => _cfg;

        /// <summary>表现层按 id 来"拉"鱼的状态用。</summary>
        public FishController FishCtrl => _fishCtrl;

        /// <summary>鱼钩根节点的 Transform。表现层把抓到的鱼挂到它下面时用。</summary>
        public Transform HookRoot => hookView != null ? hookView.transform : null;

        public GameState State => _state;

        protected override void Awake()
        {
            base.Awake();
            if (Instance != this) return;

            _cfg = GameConfig.Get();
            _gModel = new GameModel();
            _input = new InputController();

            Camera cam = Camera.main;
            Transform bgTiles = worldRoot != null ? worldRoot.Find("bgs") : null;
            Transform fishRoot = worldRoot != null ? worldRoot.Find("fishs") : null;

            _bgCtrl = new BGController(worldRoot, bgTiles, cam);

            // 开场 / 收场镜头：组件挂在 Main Camera 上
            _camCtrl = cam != null ? cam.GetComponent<CameraController>() : null;
            if (_camCtrl == null) Debug.LogWarning("[GameMgr] Main Camera 上没有 CameraController，开场/收场镜头动画不会播放");
            _fishCtrl = new FishController(fishRoot, cam);
            _hookCtrl = new HookController(_gModel, _fishCtrl, cam, hookView.transform.position.z);

            // 显示层持有控制层引用（View → Controller）
            hookView.Bind(_hookCtrl);

            // 教程面板点 [开始游戏] 之后才真正开局。两边只走事件，谁也不持有谁
            EventMgr.Subscribe(GameEvent.TutorialStartGame, OnTutorialStartGame);
        }

        /// <summary>教程看完 / 被跳过：这一局正式开始。</summary>
        private void OnTutorialStartGame(object payload)
        {
            if (_tutorialDone)
            {
                return;
            }

            _tutorialDone = true;
            RestartGame();
        }

        private void Start()
        {
            _input.Init(Camera.main);
            RestartGame();
        }

        protected override void OnDestroy()
        {
            EventMgr.Clear();
            base.OnDestroy();
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            _input.Tick();
            bool canControl = _state == GameState.CastingDown || _state == GameState.ReelingUp;
            _hookCtrl.TickHorizontal(dt, canControl, _input);

            TickState(dt);
            _fishCtrl.Tick(dt, _state, _gModel.Depth, ScrollDir);
            // 背景贴图循环放最后
            _bgCtrl.Tick();
        }

        /// <summary>背景是否在滚动：+1 世界上移（下潜），-1 世界下移（上浮），0 静止。</summary>
        private int ScrollDir
        {
            get
            {
                if (!_cfg.IsWorldScrolling(_gModel.Depth)) return 0;
                if (_state is GameState.ReelingUp or GameState.FastReturn) return -1;
                return _state == GameState.CastingDown ? 1 : 0;
            }
        }

        // ------------------------------------------------------------------
        // 状态机
        // ------------------------------------------------------------------

        private void TickState(float dt)
        {
            switch (_state)
            {
                // 教程阶段：主流程完全静默 —— 面板盖在画面上，玩家的点击留给面板上的按钮
                case GameState.Tutorial:
                    break;

                case GameState.Ready:
                    if (_input.PressedThisFrame) ChangeState(GameState.CastingDown);
                    break;

                case GameState.CastingDown:
                    Advance(dt, _cfg.descendSpeed, true);
                    // 碰撞由 HookView 的 Trigger 回调驱动，这里只维护阶段和冷却
                    _hookCtrl.TickHurtCoolTime(dt);
                    if (_gModel.IsDead)
                    {
                        ChangeState(GameState.Failed);
                    }
                    else if (_gModel.IsAtBottom)
                    {
                        ChangeState(GameState.ReelingUp);
                    }

                    break;

                case GameState.ReelingUp:
                    Advance(dt, _cfg.ascendSpeed, false);
                    if (_gModel.IsInventoryFull)
                    {
                        // 满仓：不能再操作了，加速收线回原点并结算
                        ChangeState(GameState.FastReturn);
                    }
                    else
                    {
                        // 碰撞由 Trigger 回调驱动，这里不需要主动扫描
                        if (_gModel.Depth <= 0f) ChangeState(GameState.Settlement);
                    }

                    break;

                case GameState.FastReturn:
                    Advance(dt, _cfg.fastReturnSpeed, false);
                    if (_gModel.Depth <= 0f) ChangeState(GameState.Settlement);

                    break;

                default:
                    // 结算界面除了按钮，也支持直接按 R 再来一局
                    if (Input.GetKeyDown(KeyCode.R)) RestartGame();

                    break;
            }
        }

        /// <summary>推进深度，并把深度同步给背景滚动和鱼钩纵向表现。</summary>
        private void Advance(float dt, float speed, bool isDown)
        {
            _gModel.SetDepth(_gModel.Depth + (isDown ? 1f : -1f) * speed * dt);
            _bgCtrl.SetScroll(_cfg.WorldScrollAt(_gModel.Depth));
            _hookCtrl.ApplyDepth(_gModel.Depth, isDown, speed);

            TickCamera(speed, isDown);
        }

        private void TickCamera(float speed, bool isDown)
        {
            if (_camCtrl == null) return;

            if (isDown)
            {
                if (_camMovedToPlay) return;

                Camera cam = Camera.main;
                if (cam == null) return;

                float halfHeight = ViewportUtil.HalfHeight(cam);
                float cameraY = cam.transform.position.y;

                // 下边界往上 1/5 个屏幕高（屏幕总高 = 2 * halfHeight）
                float triggerY = cameraY - halfHeight + halfHeight * 2f * 0.2f;

                float hookY = _hookCtrl.Position.y;
                if (hookY > triggerY) return;

                _camMovedToPlay = true;
                _camMovedBack = false;
                _camCtrl.StartMove(false, Mathf.Max(0.05f, (hookY - _cfg.hookMiddleY) / Mathf.Max(0.01f, speed)));
                return;
            }

            // 上浮：背景还在滚的时候镜头不动；等 depth 回到 CastDepth（背景停住）再一起升
            if (_camMovedBack || _gModel.Depth > _cfg.CastDepth) return;

            _camMovedBack = true;
            _camCtrl.StartMove(true,
                Mathf.Max(0.05f, (_cfg.hookStartY - _hookCtrl.Position.y) / Mathf.Max(0.01f, speed)));
        }

        private void ChangeState(GameState next)
        {
            if (_state == next) return;

            _state = next;
            EventMgr.Publish(GameEvent.StateChanged, _state);

            switch (next)
            {
                case GameState.Settlement or GameState.Failed:
                    // 正常结算是上浮时镜头和鱼钩就一起回去了；
                    // 只有"血量归零"没有上浮过程，这里补一次收场 ——
                    // 镜头和鱼钩必须用同一个时长，否则又变成只有镜头在动
                    if (next == GameState.Failed)
                    {
                        _camCtrl?.StartMove(true, _cfg.cameraBackDuration);
                        _hookCtrl?.ReelOutForSettle(_cfg.cameraBackDuration);
                    }
                    EventMgr.Publish(GameEvent.GameSettle, new SettlePayload
                    {
                        IsWin = _gModel.IsWin,
                        Score = _gModel.Score,
                        CaughtCount = _gModel.CaughtCount,
                        FishList = new List<CaughtFish>(_gModel.Caught),
                    });
                    break;
            }
        }

        /// <summary>
        /// 重开一局。
        /// 教程还没看过时，开局前先回到 Tutorial 状态（面板会演一遍玩法）；
        /// 看完之后再重开就直接进 Ready。
        /// </summary>
        public void RestartGame()
        {
            // 被抓住的鱼先还回对象池，再清空钩子和全部存活鱼
            _fishCtrl.RecycleCaught(_hookCtrl.Caught);
            _hookCtrl.ResetDive();
            _fishCtrl.RecycleAll();

            _gModel.Reset(_cfg.maxHp, _cfg.maxCatch, _cfg.maxDepth);
            _camMovedToPlay = false;   // 下一局重新播开场镜头
            _camMovedBack = false;
            _camCtrl?.StartMove(true, 0f);   // 立刻回到开始画面
            _bgCtrl.ResetScroll();
            _state = playTutorial && !_tutorialDone ? GameState.Tutorial : GameState.Ready;

            EventMgr.Publish(GameEvent.StateChanged, _state);
        }
    }
}