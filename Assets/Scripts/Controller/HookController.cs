using System.Collections.Generic;
using Config;
using Core;
using DG.Tweening;
using Model;
using Tool;
using UnityEngine;
using View;

namespace Controller
{
    /// <summary>
    /// 鱼钩控制器（控制层）。**自己持有鱼钩的全部数据**（坐标、半径、渔获），
    /// 不再有单独的 Hook 实体——数据只在控制层。
    /// 表现层 <c>HookView</c> 每帧来"拉"坐标，并在事件触发时播放反馈。
    ///
    /// 一趟下潜由"鱼钩自己在动"和"背景在动"两段拼成，DOTween 负责其中的鱼钩位移：
    ///
    ///   ① 抛钩 Casting        深度 0 → CastDepth            背景不动，鱼钩落到屏幕中间
    ///   ② 常规下潜 Cruising    深度 → FinalDiveStartDepth    鱼钩停中间，背景滚动
    ///   ③ 触底冲刺 FinalDive   深度 → maxDepth                背景停住，鱼钩继续往屏幕底部探
    ///   ④ 收线起钩 PullUp      反向                           背景不动，鱼钩先回到中间
    ///   ⑤ 常规上浮 Cruising    反向                           背景往回滚
    ///   ⑥ 收钩出水 ReelingOut  深度 → 0                       鱼钩收回抛钩起点
    ///
    /// 分段由深度直接推导，不需要单独维护状态机；补间时长 = 该段剩余深度 ÷ 当前速度，
    /// 所以鱼钩探到底的那一刻深度也刚好到最大值。
    /// </summary>
    public class HookController
    {
        private enum VerticalPhase
        {
            None,
            Casting,
            Cruising,
            FinalDive,
            PullUp,
            ReelingOut,
        }

        private readonly GameModel _model;
        private readonly Camera _cam;
        private readonly GameConfig _cfg;
        private readonly float _planeZ;

        // ---- 鱼钩数据（原 Hook 实体的内容，现在归控制层持有）----
        private readonly List<FishRuntime> _caught = new();
        private float _x;
        private float _visualY;
        private float _radius;

        private float _hurtTimer;
        private Tween _yTween;
        private VerticalPhase _phase = VerticalPhase.None;

        /// <param name="model">数据层，控制层持有它（Controller → Model）。</param>
        /// <param name="hookRadius">钩子碰撞半径，由组合根从表现层实测后传进来。</param>
        /// <param name="planeZ">鱼钩所在的世界 Z 平面。</param>
        public HookController(GameModel model, Camera cam, float hookRadius, float planeZ)
        {
            _model = model;
            _cam = cam;
            _planeZ = planeZ;
            _cfg = GameConfig.Get();

            _radius = hookRadius;

            DOTween.Init();
            ResetDive();
        }

        /// <summary>挂在钩上的鱼，按抓取顺序排列。</summary>
        public IReadOnlyList<FishRuntime> Caught => _caught;

        /// <summary>鱼钩当前的世界坐标。表现层每帧来"拉"，控制层不主动写 Transform。</summary>
        public Vector3 Position => new Vector3(_x, _visualY, _planeZ);

        /// <summary>碰撞半径（世界单位）。</summary>
        public float Radius => _radius;

        /// <summary>释放补间，由总控在销毁时调用。</summary>
        public void Dispose()
        {
            _yTween?.Kill();
            _yTween = null;
        }

        // ==================================================================
        // 对外接口
        // ==================================================================

        /// <summary>回到起点：杀掉纵向补间，鱼钩回到抛钩高度、横向居中、清空渔获。</summary>
        public void ResetDive()
        {
            _yTween?.Kill();
            _yTween = null;
            _phase = VerticalPhase.None;
            _hurtTimer = 0f;

            _visualY = _cfg.hookStartY;
            _x = 0f;
            _caught.Clear();
        }

        /// <summary>每帧的横向控制。长按拖动时追指针；不可操作时自动回中。</summary>
        public void TickHorizontal(float dt, bool canControl, InputController input)
        {
            if (canControl)
            {
                if (input is { PointerActive: true })
                {
                    float limit = ViewportUtil.HalfWidth(_cam) * _cfg.hookXLimitRatio;
                    float target = Mathf.Clamp(input.PointerWorldX, -limit, limit);
                    _x = Mathf.MoveTowards(_x, target, _cfg.hookMoveSpeed * dt);
                }
            }
            else
            {
                _x = Mathf.MoveTowards(_x, 0f, _cfg.hookReturnSpeed * dt);
            }
        }

        /// <summary>把当前深度同步给鱼钩的纵向表现，只在"跨段"的那一帧触发一次 DOTween。</summary>
        /// <param name="descending">true = 下潜，false = 上浮。</param>
        /// <param name="speed">当前速度，用来算补间时长。</param>
        public void ApplyDepth(float depth, bool descending, float speed)
        {
            VerticalPhase phase = ResolvePhase(depth, descending);
            if (phase == _phase) return;

            _phase = phase;
            EnterPhase(phase, depth, speed);
        }

        #region 碰撞

        /// <summary>钩子的世界包围盒，供碰撞判定使用。</summary>
        public Bounds GetInteractBounds() => new Bounds(Position, Vector3.one * (_radius * 2f));

        /// <summary>
        /// 下潜阶段：碰到鱼扣血。
        /// 同一条鱼一次下潜只生效一次，再加一个全局受击冷却，避免贴着大鱼被瞬间打空。
        /// </summary>
        public void CheckHurt(IReadOnlyList<FishRuntime> fishList, float dt)
        {
            if (_hurtTimer > 0f) _hurtTimer -= dt;
            if (_model == null || _model.IsDead || fishList == null) return;

            Vector2 center = Position;
            foreach (FishRuntime fish in fishList)
            {
                if (fish?.Data == null || fish.IsCaught || fish.HasHitHook) continue;
                if (!MathUtil.CircleIntersectsBounds(center, _radius, fish.WorldBounds)) continue;

                fish.HasHitHook = true;
                if (_hurtTimer > 0f) continue;

                int real = _model.ApplyDamage(fish.Data.damage);
                if (real <= 0) continue;

                _hurtTimer = _cfg.hurtCooldown;

                // 表现层订阅这个事件播放受击反馈，控制层不认识任何 View
                EventMgr.Publish(GameEvent.FishHurt, real);

                if (_model.IsDead) return;
            }
        }

        /// <summary>上浮阶段：碰到鱼就抓住挂到钩上。</summary>
        public void CheckCatch(IReadOnlyList<FishRuntime> fishList)
        {
            if (_model == null || _model.IsInventoryFull || fishList == null) return;

            Vector2 center = Position;
            // 倒序遍历：抓住的鱼会马上被标记，后面的逻辑不再需要它们
            for (int i = fishList.Count - 1; i >= 0; i--)
            {
                FishRuntime fish = fishList[i];
                if (fish?.Data == null || fish.IsCaught) continue;
                if (!MathUtil.CircleIntersectsBounds(center, _radius, fish.WorldBounds)) continue;

                _model.AddCaught(fish.Data);
                AttachCaught(fish);
            }
        }

        /// <summary>
        /// 把一条鱼记进钩上的渔获（逻辑）。
        /// 视觉上的"挂到钩子下面"由表现层订阅 FishCaught 事件自己完成。
        /// </summary>
        public void AttachCaught(FishRuntime fish)
        {
            if (fish == null) return;

            fish.IsCaught = true;
            _caught.Add(fish);

            // 只报一个数量，表现层自己看 IsCaught 决定挂到钩下——
            // 控制层不持有、也不传递任何 View 引用
            EventMgr.Publish(GameEvent.FishCaught, _caught.Count);
        }

        #endregion

        // ==================================================================
        // 纵向分段
        // ==================================================================

        private VerticalPhase ResolvePhase(float depth, bool descending)
        {
            if (descending)
            {
                if (depth < _cfg.CastDepth) return VerticalPhase.Casting;
                return depth >= _cfg.FinalDiveStartDepth ? VerticalPhase.FinalDive : VerticalPhase.Cruising;
            }

            if (depth > _cfg.FinalDiveStartDepth) return VerticalPhase.PullUp;
            return depth > _cfg.CastDepth ? VerticalPhase.Cruising : VerticalPhase.ReelingOut;
        }

        private void EnterPhase(VerticalPhase phase, float depth, float speed)
        {
            switch (phase)
            {
                // ① 抛钩：背景先不动，鱼钩自己落向屏幕中间
                case VerticalPhase.Casting:
                    TweenY(_cfg.hookMiddleY, TimeFor(_cfg.CastDepth - depth, speed), Ease.Linear);
                    break;

                // ②⑤ 常规段：鱼钩停在屏幕中间，位移交给背景
                case VerticalPhase.Cruising:
                    TweenY(_cfg.hookMiddleY, 0.12f, Ease.OutQuad);
                    break;

                // ③ 触底冲刺：背景停住，鱼钩继续往屏幕底部探
                case VerticalPhase.FinalDive:
                    TweenY(_cfg.hookBottomY, TimeFor(_cfg.maxDepth - depth, speed), Ease.Linear);
                    break;

                // ④ 收线起钩：背景不动，鱼钩先回到屏幕中间
                case VerticalPhase.PullUp:
                    TweenY(_cfg.hookMiddleY, TimeFor(depth - _cfg.FinalDiveStartDepth, speed), Ease.Linear);
                    break;

                // ⑥ 收钩出水：鱼钩回到抛钩起点
                case VerticalPhase.ReelingOut:
                    TweenY(_cfg.hookStartY, TimeFor(depth, speed), Ease.Linear);
                    break;
            }
        }

        private static float TimeFor(float distance, float speed) =>
            Mathf.Max(0.08f, distance / Mathf.Max(0.01f, speed));

        /// <summary>
        /// 补间的是"一个 float"而不是 DOMoveY：
        /// DOMoveY 会把整条 position 都锁成补间创建时的值，会和每帧改的 X 打架。
        /// </summary>
        private void TweenY(float targetY, float duration, Ease ease)
        {
            _yTween?.Kill();
            _yTween = DOTween.To(() => _visualY, v => _visualY = v, targetY, duration).SetEase(ease);
        }
    }
}
