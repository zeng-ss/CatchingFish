using System.Collections.Generic;
using Config;
using Core;
using DG.Tweening;
using Entities;
using Model;
using Tool;
using UnityEngine;
using View;

namespace Controller
{
    /// <summary>
    /// 鱼钩控制器。挂在 hook 物体上，自己取同物体上的 HookView。
    ///
    /// 一趟下潜由"鱼钩自己在动"和"背景在动"两段拼成，DOTween 负责其中的鱼钩位移：
    ///
    ///   ① 抛钩（Casting）    深度 0 → CastDepth
    ///      背景不动，鱼钩 DOTween 落到屏幕中间（OutQuad 收尾，像真的甩出去）
    ///   ② 常规下潜（Cruising） CastDepth → FinalDiveStartDepth
    ///      鱼钩停在屏幕中间，背景负责滚动 —— 这一段鱼从屏幕下方进场
    ///   ③ 触底冲刺（FinalDive）FinalDiveStartDepth → maxDepth
    ///      背景停住，鱼钩 DOTween 继续往屏幕底部探（InQuad 加速），营造"要到底了"的压迫感。
    ///      补间时长 = 剩余深度 / 下潜速度，所以鱼钩刚好探到底时深度也刚好到最大。
    ///
    /// 上浮完全对称：④ 收线起钩（背景不动，鱼钩先回到中间）→ ⑤ 常规上浮（背景往回滚）
    /// → ⑥ 收钩出水（鱼钩回到抛钩起点）。
    ///
    /// 分段由深度直接推导（ResolvePhase），不需要单独维护一个状态机，
    /// 而且因为"世界滚动量"和"鱼钩高度"都是深度的纯函数，上浮方向天然对称。
    /// </summary>
    public class HookController : MonoBehaviour
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

        [Header("表现层（留空自动取同物体上的 HookView）")]
        [SerializeField]
        private HookView _view;

        private GameConfig _cfg;
        private Camera _cam;
        private Hook _hook;

        private float _xLimit = 8f;
        private float _visualY;
        private float _hurtTimer;
        private Tween _yTween;
        private VerticalPhase _phase = VerticalPhase.None;

        public Hook Hook => _hook;

        /// <summary>鱼钩根节点的 Transform。</summary>
        public Transform Root => _view != null ? _view.transform : transform;

        private void Awake()
        {
            _cfg = GameConfig.Get();
            _cam = Camera.main;

            // 必须在移动鱼钩之前先把竿尖锚点量出来（只靠 Awake 的先后顺序不可靠）
            _view?.Prepare();

            _hook = new Hook();
            _hook.Radius = _view != null
                ? _view.GetColliderRadius(_cfg.hookFallbackRadius)
                : _cfg.hookFallbackRadius;

            DOTween.Init();

            ResetDive();
        }

        private void OnDestroy()
        {
            _yTween?.Kill();
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
            _hook.X = 0f;
            _hook.Y = _cfg.hookStartY;
            _hook.ClearCaught();

            ApplyTransform();
        }

        /// <summary>
        /// 每帧的横向控制。长按并拖动鼠标时鱼钩以有限速度追指针；
        /// 不可操作时（满仓加速返回）自动回中。
        /// </summary>
        public void TickHorizontal(float dt, bool canControl, InputController input)
        {
            _xLimit = ViewportUtil.HalfWidth(_cam) * _cfg.hookXLimitRatio;

            if (canControl)
            {
                if (input != null && input.PointerActive)
                {
                    float target = Mathf.Clamp(input.PointerWorldX, -_xLimit, _xLimit);
                    _hook.X = Mathf.MoveTowards(_hook.X, target, _cfg.hookMoveSpeed * dt);
                }
            }
            else
            {
                _hook.X = Mathf.MoveTowards(_hook.X, 0f, _cfg.hookReturnSpeed * dt);
            }

            ApplyTransform();
        }

        /// <summary>
        /// 把当前深度同步给鱼钩的纵向表现。
        /// 只在"跨段"的那一帧触发一次 DOTween，不会每帧打断补间。
        /// </summary>
        /// <param name="descending">true = 下潜，false = 上浮（决定分段怎么解读）。</param>
        /// <param name="speed">当前的下潜/上浮速度，用来算补间时长。</param>
        public void ApplyDepth(float depth, bool descending, float speed)
        {
            VerticalPhase phase = ResolvePhase(depth, descending);
            if (phase == _phase)
            {
                return;
            }

            _phase = phase;
            EnterPhase(phase, depth, speed);
        }

        /// <summary>钩子的世界包围盒，供碰撞判定使用。</summary>
        public Bounds GetInteractBounds()
        {
            if (_view != null)
            {
                return _view.ColliderBounds;
            }

            return new Bounds(new Vector3(_hook.X, _visualY, 0f), Vector3.one * 0.5f);
        }

        /// <summary>
        /// 下潜阶段：碰到鱼扣血。
        /// 同一条鱼一次下潜只生效一次，再加一个全局受击冷却，避免贴着大鱼被瞬间打空。
        /// </summary>
        public void CheckHurt(GameModel model, IReadOnlyList<Fish> fishList, float dt)
        {
            if (_hurtTimer > 0f)
            {
                _hurtTimer -= dt;
            }

            if (model == null || model.IsDead || fishList == null)
            {
                return;
            }

            Bounds bounds = GetInteractBounds();
            Vector2 center = bounds.center;
            bool canHurt = _hurtTimer <= 0f;

            for (int i = 0; i < fishList.Count; i++)
            {
                Fish fish = fishList[i];
                if (fish == null || fish.IsCaught || fish.HasHitHook || fish.Data == null)
                {
                    continue;
                }

                if (!MathUtil.CircleIntersectsBounds(center, _hook.Radius, fish.WorldBounds))
                {
                    continue;
                }

                fish.HasHitHook = true;

                if (!canHurt || fish.Data.damage <= 0)
                {
                    continue;
                }

                int real = model.ApplyDamage(fish.Data.damage);
                if (real <= 0)
                {
                    continue;
                }

                _hurtTimer = _cfg.hurtCooldown;
                PlayHurtFeedback();
                EventMgr.Publish(GameEvent.FishHurt, real);

                if (model.IsDead)
                {
                    return;
                }
            }
        }

        /// <summary>上浮阶段：碰到鱼就抓住挂到钩上。</summary>
        public void CheckCatch(GameModel model, IReadOnlyList<Fish> fishList)
        {
            if (model == null || model.IsInventoryFull || fishList == null)
            {
                return;
            }

            Bounds bounds = GetInteractBounds();
            Vector2 center = bounds.center;

            // 倒序遍历：抓住的鱼会马上被标记，后面的逻辑不再需要它们
            for (int i = fishList.Count - 1; i >= 0; i--)
            {
                Fish fish = fishList[i];
                if (fish == null || fish.IsCaught || fish.Data == null)
                {
                    continue;
                }

                if (!MathUtil.CircleIntersectsBounds(center, _hook.Radius, fish.WorldBounds))
                {
                    continue;
                }

                AttachCaught(fish);
                EventMgr.Publish(GameEvent.FishCaught, fish.Data);

                if (model.AddCaught(fish.Data))
                {
                    return;
                }
            }
        }

        /// <summary>把一条鱼挂到钩上（表现层换父节点，逻辑层记进 Hook.Caught）。</summary>
        public void AttachCaught(Fish fish)
        {
            if (fish == null || _hook == null)
            {
                return;
            }

            fish.IsCaught = true;

            int slot = _hook.CaughtCount;
            _hook.Caught.Add(fish);

            if (_view != null && fish.View != null)
            {
                _view.AttachFish(fish.View.transform, slot, _cfg.catchSlotScale);
                fish.View.PlayCatchPunch();
            }
        }

        /// <summary>受击表现。</summary>
        public void PlayHurtFeedback()
        {
            if (_view != null)
            {
                _view.PlayHurtFeedback();
            }
        }

        // ==================================================================
        // 纵向分段
        // ==================================================================

        private VerticalPhase ResolvePhase(float depth, bool descending)
        {
            if (descending)
            {
                if (depth < _cfg.CastDepth)
                {
                    return VerticalPhase.Casting;
                }

                return depth >= _cfg.FinalDiveStartDepth
                    ? VerticalPhase.FinalDive
                    : VerticalPhase.Cruising;
            }

            if (depth > _cfg.FinalDiveStartDepth)
            {
                return VerticalPhase.PullUp;
            }

            return depth > _cfg.CastDepth
                ? VerticalPhase.Cruising
                : VerticalPhase.ReelingOut;
        }

        private void EnterPhase(VerticalPhase phase, float depth, float speed)
        {
            switch (phase)
            {
                // ① 抛钩：背景先不动，鱼钩自己落向屏幕中间
                case VerticalPhase.Casting:
                    TweenY(_cfg.hookMiddleY, TimeFor(_cfg.CastDepth - depth, speed), Ease.OutQuad);
                    break;

                // ②⑤ 常规段：鱼钩停在屏幕中间，位移交给背景
                case VerticalPhase.Cruising:
                    TweenY(_cfg.hookMiddleY, 0.12f, Ease.OutQuad);
                    break;

                // ③ 触底冲刺：背景停住，鱼钩继续往屏幕底部探
                case VerticalPhase.FinalDive:
                    TweenY(_cfg.hookBottomY, TimeFor(_cfg.maxDepth - depth, speed), Ease.InQuad);
                    break;

                // ④ 收线起钩：背景不动，鱼钩先回到屏幕中间
                case VerticalPhase.PullUp:
                    TweenY(_cfg.hookMiddleY, TimeFor(depth - _cfg.FinalDiveStartDepth, speed), Ease.OutQuad);
                    break;

                // ⑥ 收钩出水：鱼钩回到抛钩起点
                case VerticalPhase.ReelingOut:
                    TweenY(_cfg.hookStartY, TimeFor(depth, speed), Ease.InQuad);
                    break;
            }
        }

        private static float TimeFor(float distance, float speed)
        {
            return Mathf.Max(0.08f, distance / Mathf.Max(0.01f, speed));
        }

        /// <summary>
        /// 用 DOTween 补间"一个 float"，而不是直接 DOMoveY。
        /// 因为 DOMoveY 会把整条 position 都锁成创建时的值，会和我们每帧改的 X 打架。
        /// </summary>
        private void TweenY(float targetY, float duration, Ease ease)
        {
            _yTween?.Kill();
            _yTween = DOTween.To(() => _visualY, v => _visualY = v, targetY, duration).SetEase(ease);
        }

        private void ApplyTransform()
        {
            Transform root = Root;
            if (root == null)
            {
                return;
            }

            Vector3 p = root.position;
            root.position = new Vector3(_hook.X, _visualY, p.z);

            _hook.Y = _visualY;

            // 用实测的钩子包围盒刷新碰撞半径（模型缩放改了也能自适应）
            if (_view != null)
            {
                _hook.Radius = _view.GetColliderRadius(_cfg.hookFallbackRadius);
            }
        }
    }
}
