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

        private GameConfig _cfg;
        private InputController _input;
        private BGController _bgCtrl;
        private HookController _hookCtrl;
        private FishController _fishCtrl;
        private GameState _state = GameState.Ready;

        public GameModel GModel { get; private set; }
        public GameState State => _state;
        public GameConfig Config => _cfg;

        protected override void Awake()
        {
            base.Awake();
            if (Instance != this) return;

            _cfg = GameConfig.Get();
            GModel = new GameModel();
            _input = new InputController();

            Camera cam = Camera.main;
            Transform bgTiles = worldRoot != null ? worldRoot.Find("bgs") : null;
            Transform fishRoot = worldRoot != null ? worldRoot.Find("fishs") : null;

            _bgCtrl = new BGController(worldRoot, bgTiles, cam);
            _hookCtrl = new HookController(hookView, cam);
            _fishCtrl = new FishController(fishRoot, cam);
        }

        private void Start()
        {
            _input.Init(Camera.main);
            RestartGame();
        }

        protected override void OnDestroy()
        {
            _hookCtrl?.Dispose();
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
            _fishCtrl.Tick(dt, _state, GModel.Depth, ScrollDir);
            // 背景贴图循环放最后
            _bgCtrl.Tick();
        }

        /// <summary>背景是否在滚动：+1 世界上移（下潜），-1 世界下移（上浮），0 静止。</summary>
        private int ScrollDir
        {
            get
            {
                if (!_cfg.IsWorldScrolling(GModel.Depth)) return 0;
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
                case GameState.Ready:
                    if (_input.PressedThisFrame) ChangeState(GameState.CastingDown);
                    break;

                case GameState.CastingDown:
                    Advance(dt, _cfg.descendSpeed, true);
                    _hookCtrl.CheckHurt(GModel, _fishCtrl.Actives, dt);
                    if (GModel.IsDead)
                    {
                        ChangeState(GameState.Failed);
                    }
                    else if (GModel.IsAtBottom)
                    {
                        ChangeState(GameState.ReelingUp);
                    }

                    break;

                case GameState.ReelingUp:
                    Advance(dt, _cfg.ascendSpeed, false);
                    if (GModel.IsInventoryFull)
                    {
                        // 满仓：不能再操作了，加速收线回原点并结算
                        ChangeState(GameState.FastReturn);
                    }
                    else
                    {
                        _hookCtrl.CheckCatch(GModel, _fishCtrl.Actives);
                        if (GModel.Depth <= 0f) ChangeState(GameState.Settlement);
                    }

                    break;

                case GameState.FastReturn:
                    Advance(dt, _cfg.fastReturnSpeed, false);
                    if (GModel.Depth <= 0f) ChangeState(GameState.Settlement);

                    break;

                default:
                    // 结算界面除了按钮，也支持直接按 R 再来一局
                    if (Input.GetKeyDown(KeyCode.R)) RestartGame();

                    break;
            }
        }

        /// <summary>推进深度，并把深度同步给背景滚动和鱼钩纵向表现。</summary>
        private void Advance(float dt, float speed, bool descending)
        {
            GModel.SetDepth(GModel.Depth + (descending ? 1f : -1f) * speed * dt);
            _bgCtrl.SetScroll(_cfg.WorldScrollAt(GModel.Depth));
            _hookCtrl.ApplyDepth(GModel.Depth, descending, speed);
        }

        private void ChangeState(GameState next)
        {
            if (_state == next) return;

            _state = next;
            EventMgr.Publish(GameEvent.StateChanged, _state);

            switch (next)
            {
                case GameState.CastingDown:
                    EventMgr.Publish(GameEvent.GameStart);
                    break;
                case GameState.Settlement or GameState.Failed:
                    EventMgr.Publish(GameEvent.GameSettle, new SettlePayload
                    {
                        IsWin = GModel.IsWin,
                        Score = GModel.Score,
                        CaughtCount = GModel.CaughtCount,
                        FishList = new List<CaughtFish>(GModel.Caught),
                    });
                    break;
            }
        }

        /// <summary>重开一局。</summary>
        public void RestartGame()
        {
            // 被抓住的鱼先还回对象池，再清空钩子和全部存活鱼
            _fishCtrl.RecycleCaught(_hookCtrl.Hook.Caught);
            _hookCtrl.ResetDive();
            _fishCtrl.RecycleAll();

            GModel.Reset(_cfg.maxHp, _cfg.maxCatch, _cfg.maxDepth);
            _bgCtrl.ResetScroll();
            _state = GameState.Ready;

            EventMgr.Publish(GameEvent.StateChanged, _state);
        }
    }
}