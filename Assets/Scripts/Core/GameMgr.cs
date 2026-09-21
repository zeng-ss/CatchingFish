using System.Collections.Generic;
using Config;
using Controller;
using Model;
using Tool;
using UnityEngine;
using View;

namespace Core
{
    /// <summary>
    /// 【入口层 · 组合根】游戏唯一的逻辑 MonoBehaviour。
    ///
    /// 它只做三件事：
    ///   ① 在 Awake 里把控制层 new 出来（构造函数注入依赖），并把控制器绑给对应的 View；
    ///   ② 每帧按固定顺序驱动它们；
    ///   ③ 维护状态机，在状态切换时把结果广播出去。
    ///
    /// MVC 依赖方向：
    ///     Model ◄── Controller ◄── View          Controller 持有 Model，View 持有 Controller
    ///     View  ──► Controller（每帧自己"拉"状态）   Controller 从不引用、不写任何 View
    /// 所以这里的 View 引用只有两个：场景里的 `hookView`，以及通过事件往来的 HUD / 面板。
    /// </summary>
    public class GameMgr : MonoSingleton<GameMgr>
    {
        [Header("场景引用")] [Tooltip("整体滚动的世界根节点 BG")] [SerializeField]
        private Transform worldRoot;

        [SerializeField] private HookView hookView;
        [SerializeField] private BgSizeConfig bgSizeConfig;

        [Tooltip("开场是否先播玩法教程（点 [开始游戏] 之后才进 Ready）")] [SerializeField]
        private bool playTutorial = true;

        private GameConfig _cfg;
        private GameModel _model;
        private InputController _input;
        private BGController _bgCtrl;
        private HookController _hookCtrl;
        private FishController _fishCtrl;
        private CameraController _camCtrl;
        private Camera _cam;

        private GameState _state = GameState.Tutorial;
        private bool _tutorialDone;
        private bool _camMovedToPlay;
        private bool _camMovedBack;

        /// <summary>
        /// 表现层按 id 来"拉"鱼的状态用。
        /// </summary>
        public IFishStateSource FishCtrl => _fishCtrl;

        /// <summary>鱼钩根节点 —— 表现层把抓到的鱼挂到它下面时用。</summary>
        public Transform HookRoot => hookView != null ? hookView.transform : null;

        public GameState State => _state;

        // ==================================================================
        // 生命周期
        // ==================================================================

        protected override void Awake()
        {
            base.Awake();
            if (Instance != this) return;

            QualitySettings.vSyncCount = 0; Application.targetFrameRate = 60;
            _cfg = GameConfig.Get();
            _model = new GameModel();
            _input = new InputController();

            Camera cam = Camera.main;
            _cam = cam;
            Transform bgTiles = worldRoot != null ? worldRoot.Find("bgs") : null;
            Transform fishRoot = worldRoot != null ? worldRoot.Find("fishs") : null;
            Transform headImg = worldRoot != null ? worldRoot.Find("head") : null;
            ApplyOrientationScale(bgTiles, headImg);

            // 控制层只依赖 Model 和彼此，不认识任何 View
            _bgCtrl = new BGController(worldRoot, bgTiles, cam);
            _fishCtrl = new FishController(fishRoot, cam);
            _hookCtrl = new HookController(_model, _fishCtrl, cam, hookView.transform.position.z);

            // View → Controller：表现层拿到控制器引用，之后每帧自己拉状态
            hookView.Bind(_hookCtrl);

            // 开场 / 收场镜头组件挂在 Main Camera 上
            _camCtrl = cam != null ? cam.GetComponent<CameraController>() : null;
            if (_camCtrl == null)
            {
                Debug.LogWarning("[GameMgr] Main Camera 上没有 CameraController，开场镜头不会播放。");
            }

            // 教程面板点 [开始游戏] 之后才真正开局：两边只走事件，谁也不持有谁
            EventMgr.Subscribe(GameEvent.TutorialStartGame, OnTutorialStartGame);
        }

        private void ApplyOrientationScale(Transform bgTiles, Transform headImg)
        {
            bool isLandscape = Screen.width > Screen.height;

            float bgScaleX = isLandscape
                ? bgSizeConfig.bgSizes[0].hScale
                : bgSizeConfig.bgSizes[0].pScale;

            float headScaleX = isLandscape
                ? bgSizeConfig.bgSizes[1].hScale
                : bgSizeConfig.bgSizes[1].pScale;

            SetScaleX(bgTiles.transform, bgScaleX);
            SetScaleX(headImg.transform, headScaleX);
        }

        private void SetScaleX(Transform target, float x)
        {
            var scale = target.localScale;
            scale.x = x;
            target.localScale = scale;
        }

        private void Start()
        {
            _input.Init(_cam);
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

            bool canControl = _state is GameState.CastingDown or GameState.ReelingUp;
            _hookCtrl.TickHorizontal(dt, canControl, _input);
            TickState(dt);
            _fishCtrl.Tick(dt, _state, _model.Depth, ScrollDir);
            _bgCtrl.Tick(); // 背景贴图循环，放最后
        }

        /// <summary>背景滚动方向：+1 世界上移（下潜），-1 世界下移（上浮），0 静止。</summary>
        private int ScrollDir
        {
            get
            {
                if (!_cfg.IsWorldScrolling(_model.Depth)) return 0;
                if (_state is GameState.ReelingUp or GameState.FastReturn) return -1;
                return _state == GameState.CastingDown ? 1 : 0;
            }
        }

        // ==================================================================
        // 状态机
        // ==================================================================

        private void TickState(float dt)
        {
            switch (_state)
            {
                // 教程阶段主流程完全静默：面板盖在画面上，点击留给面板按钮
                case GameState.Tutorial:
                    break;

                case GameState.Ready:
                    if (_input.PressedThisFrame) ChangeState(GameState.CastingDown);
                    break;

                case GameState.CastingDown:
                    Advance(dt, _cfg.descendSpeed, true);
                    _hookCtrl.TickHurtCoolTime(dt); // 碰撞由 HookView 的 Trigger 回调驱动
                    if (_model.IsDead)
                    {
                        ChangeState(GameState.Failed);
                    }
                    else if (_model.IsAtBottom)
                    {
                        ChangeState(GameState.ReelingUp);
                    }

                    break;

                case GameState.ReelingUp:
                    Advance(dt, _cfg.ascendSpeed, false);
                    if (_model.IsInventoryFull)
                    {
                        ChangeState(GameState.FastReturn); // 满仓：不能再操作，加速收线
                    }
                    else if (_model.Depth <= 0f)
                    {
                        ChangeState(GameState.Settlement);
                    }

                    break;

                case GameState.FastReturn:
                    Advance(dt, _cfg.fastReturnSpeed, false);
                    if (_model.Depth <= 0f) ChangeState(GameState.Settlement);
                    break;

                default:
                    if (Input.GetKeyDown(KeyCode.R)) RestartGame();
                    break;
            }
        }

        /// <summary>推进深度，并把深度同步给背景滚动、鱼钩纵向表现和镜头。</summary>
        private void Advance(float dt, float speed, bool isDown)
        {
            _model.SetDepth(_model.Depth + (isDown ? 1f : -1f) * speed * dt);
            _bgCtrl.SetScroll(_cfg.WorldScrollAt(_model.Depth));
            _hookCtrl.ApplyDepth(_model.Depth, isDown, speed);

            TickCamera(speed, isDown);
        }

        /// <summary>
        /// 开场时序：
        ///   ① 点击后鱼钩先往下走，这一段镜头和背景都不动（画面还是开始画面）；
        ///   ② 鱼钩落到"下边界往上 1/5 个屏幕高"时，镜头开始下移；
        ///   ③ 镜头时长 = 鱼钩从触发点走到 hookMiddleY 的时间，且用同一种缓动 ——
        ///      所以鱼钩停到游玩位置的那一刻，镜头也刚好到位；
        ///   ④ 那一刻之后深度超过 CastDepth，世界才开始滚动。
        ///
        /// 收场反过来：等深度回到 CastDepth（背景停住）镜头才和鱼钩一起升回开始画面。
        /// </summary>
        private void TickCamera(float speed, bool isDown)
        {
            if (_camCtrl == null || _cam == null) return;

            if (isDown)
            {
                if (_camMovedToPlay) return;

                float halfHeight = ViewportUtil.HalfHeight(_cam);
                float triggerY = _cam.transform.position.y - halfHeight + halfHeight * 0.4f;
                float hookY = _hookCtrl.Position.y;
                if (hookY > triggerY) return;

                _camMovedToPlay = true;
                _camMovedBack = false;
                _camCtrl.StartMove(false, Mathf.Max(0.05f, (hookY - _cfg.hookMiddleY) / speed));
                return;
            }

            if (_camMovedBack || _model.Depth > _cfg.CastDepth) return;

            _camMovedBack = true;
            _camCtrl.StartMove(true, Mathf.Max(0.05f, (_cfg.hookStartY - _hookCtrl.Position.y) / speed));
        }

        private void ChangeState(GameState next)
        {
            if (_state == next) return;

            _state = next;
            EventMgr.Publish(GameEvent.StateChanged, _state);

            if (next is not (GameState.Settlement or GameState.Failed)) return;

            // "氧气耗尽"没有上浮过程，补一次收场：镜头和鱼钩用同一个时长，避免只有镜头在动
            if (next == GameState.Failed)
            {
                _camCtrl?.StartMove(true, _cfg.cameraBackDuration);
                _hookCtrl.ReelOutForSettle(_cfg.cameraBackDuration);
            }

            EventMgr.Publish(GameEvent.GameSettle, new SettlePayload
            {
                IsWin = _model.IsWin,
                Score = _model.Score,
                CaughtCount = _model.CaughtCount,
                FishList = new List<CaughtFish>(_model.Caught),
            });
        }

        /// <summary>重开一局。教程没播过就先回到 Tutorial，播过了直接进 Ready。</summary>
        public void RestartGame()
        {
            _fishCtrl.RecycleCaught(_hookCtrl.Caught);
            _hookCtrl.ResetDive();
            _fishCtrl.RecycleAll();

            _model.Reset(_cfg.maxHp, _cfg.maxCatch, _cfg.maxDepth);
            _camMovedToPlay = false;
            _camMovedBack = false;
            _camCtrl?.StartMove(true, 0f); // 立刻回到开始画面
            _bgCtrl.ResetScroll();
            _state = playTutorial && !_tutorialDone ? GameState.Tutorial : GameState.Ready;

            EventMgr.Publish(GameEvent.StateChanged, _state);
        }

        /// <summary>教程看完 / 被跳过：这一局正式开始。</summary>
        private void OnTutorialStartGame(object payload)
        {
            if (_tutorialDone) return;

            _tutorialDone = true;
            RestartGame();
        }
    }
}