using System.Collections.Generic;
using Config;
using Controller;
using Model;
using Tool;
using UnityEngine;

namespace Core
{
    /// <summary>
    /// 游戏总控。只做三件事：
    ///   1) 驱动状态机（准备 → 下潜 → 上浮 → 加速返回 → 结算 / 失败）；
    ///   2) 按帧驱动各控制器；
    ///   3) 把 Model 的变化广播给 View。
    ///
    /// 每个 Controller 都挂在自己的物体上、各自在 Awake 里完成自初始化，
    /// 所以这里没有任何 Find / AddComponent / Init 转发的样板代码——只在 Inspector 里拖引用。
    /// 碰撞判定这类"鱼钩碰到鱼"的逻辑归 HookController，不在这里堆。
    /// </summary>
    public class GameMgr : MonoSingleton<GameMgr>
    {
        [Header("控制器（各自挂在自己的物体上）")] [SerializeField]
        private BGController _bgCtrl;

        [SerializeField] private HookController _hookCtrl;

        [SerializeField] private FishController _fishCtrl;

        private GameConfig _cfg;
        private GameModel _model;
        private InputController _input;

        private GameState _state = GameState.Ready;

        // 广播用脏检查缓存：只在数值真的变了才发事件，省掉一堆手动 Publish 调用
        private int _lastHp = int.MinValue;
        private int _lastScore = int.MinValue;
        private int _lastCaught = int.MinValue;
        private float _lastDepthRatio = -1f;

        public GameModel Data => _model;
        public GameState State => _state;
        public GameConfig Config => _cfg;

        protected override void Awake()
        {
            base.Awake();

            if (Instance != this)
            {
                return;
            }

            _cfg = GameConfig.Get();
            _model = new GameModel();
            _input = new InputController();
        }

        private void Start()
        {
            // Start 一定晚于所有 Awake，这时各控制器都已经自初始化完毕
            _input.Init(Camera.main);
            RestartGame();
        }

        protected override void OnDestroy()
        {
            // 先清事件（静态订阅不清理会跨场景泄漏），再交给基类重置单例引用
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

            _fishCtrl.Tick(dt, _state, _model.Depth, ScrollDir);
            BroadcastIfChanged();
        }

        /// <summary>背景是否在滚动：+1 世界上移（下潜），-1 世界下移（上浮），0 静止。</summary>
        private int ScrollDir
        {
            get
            {
                if (!_cfg.IsWorldScrolling(_model.Depth))
                {
                    return 0;
                }

                if (_state == GameState.ReelingUp || _state == GameState.FastReturn)
                {
                    return -1;
                }

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
                    if (_input.PressedThisFrame)
                    {
                        ChangeState(GameState.CastingDown);
                    }

                    break;

                case GameState.CastingDown:
                    Advance(dt, _cfg.descendSpeed, true);
                    _hookCtrl.CheckHurt(_model, _fishCtrl.Actives, dt);

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
                    _hookCtrl.CheckCatch(_model, _fishCtrl.Actives);

                    if (_model.IsInventoryFull)
                    {
                        // 满仓：不能再操作了，加速收线回原点并结算
                        ChangeState(GameState.FastReturn);
                    }
                    else if (_model.Depth <= 0f)
                    {
                        ChangeState(GameState.Settlement);
                    }

                    break;

                case GameState.FastReturn:
                    Advance(dt, _cfg.fastReturnSpeed, false);

                    if (_model.Depth <= 0f)
                    {
                        ChangeState(GameState.Settlement);
                    }

                    break;

                default:
                    // 结算界面除了按钮，也支持直接按 R 再来一局
                    if (Input.GetKeyDown(KeyCode.R))
                    {
                        RestartGame();
                    }

                    break;
            }
        }

        /// <summary>推进深度，并把深度同步给背景滚动和鱼钩纵向表现。</summary>
        private void Advance(float dt, float speed, bool descending)
        {
            _model.SetDepth(_model.Depth + (descending ? 1f : -1f) * speed * dt);
            _bgCtrl.SetScroll(_cfg.WorldScrollAt(_model.Depth));
            _hookCtrl.ApplyDepth(_model.Depth, descending, speed);
        }

        private void ChangeState(GameState next)
        {
            if (_state == next)
            {
                return;
            }

            _state = next;
            EventMgr.Publish(GameEvent.StateChanged, _state);

            if (next == GameState.CastingDown)
            {
                EventMgr.Publish(GameEvent.GameStart);
            }
            else if (next == GameState.Settlement || next == GameState.Failed)
            {
                EventMgr.Publish(GameEvent.GameSettle, new SettlePayload
                {
                    IsWin = _model.IsWin,
                    Score = _model.Score,
                    CaughtCount = _model.CaughtCount,
                    FishList = new List<CaughtFish>(_model.Caught),
                });
            }
        }

        /// <summary>重开一局。</summary>
        public void RestartGame()
        {
            // 被抓住的鱼先还回对象池，再清空钩子和全部存活鱼
            _fishCtrl.RecycleCaught(_hookCtrl.Hook.Caught);
            _hookCtrl.ResetDive();
            _fishCtrl.RecycleAll();

            _model.Reset(_cfg.maxHp, _cfg.maxCatch, _cfg.maxDepth);
            _bgCtrl.ResetScroll();

            _state = GameState.Ready;

            BroadcastAll();
            EventMgr.Publish(GameEvent.GameRestart);
            EventMgr.Publish(GameEvent.StateChanged, _state);
        }

        // ------------------------------------------------------------------
        // 数据广播（脏检查）
        // ------------------------------------------------------------------

        private void BroadcastAll()
        {
            _lastHp = int.MinValue;
            _lastScore = int.MinValue;
            _lastCaught = int.MinValue;
            _lastDepthRatio = -1f;
            BroadcastIfChanged();
        }

        private void BroadcastIfChanged()
        {
            if (_model.Hp != _lastHp)
            {
                _lastHp = _model.Hp;
                EventMgr.Publish(GameEvent.HpChanged, new HpPayload
                {
                    Current = _model.Hp,
                    Max = _model.MaxHp,
                });
            }

            if (_model.Score != _lastScore)
            {
                _lastScore = _model.Score;
                EventMgr.Publish(GameEvent.ScoreChanged, _model.Score);
            }

            if (_model.CaughtCount != _lastCaught)
            {
                _lastCaught = _model.CaughtCount;
                EventMgr.Publish(GameEvent.CaughtChanged, new CatchPayload
                {
                    Current = _model.CaughtCount,
                    Max = _model.MaxCatch,
                });
            }

            if (!Mathf.Approximately(_model.DepthRatio, _lastDepthRatio))
            {
                _lastDepthRatio = _model.DepthRatio;
                EventMgr.Publish(GameEvent.DepthChanged, _model.DepthRatio);
            }
        }
    }
}