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

        private readonly HookView _view;
        private readonly Camera _cam;
        private readonly GameConfig _cfg;
        private readonly Hook _hook = new();

        private float _visualY;
        private float _hurtTimer;
        private Tween _yTween;
        private VerticalPhase _phase = VerticalPhase.None;

        public HookController(HookView view, Camera cam)
        {
            _view = view;
            _cam = cam;
            _cfg = GameConfig.Get();

            if (_view == null)
            {
                Debug.LogError("[HookController] HookView 为空，鱼钩不会显示也不会移动。");
                return;
            }

            // 必须在移动鱼钩之前先把竿尖锚点量出来（只靠 Awake 的先后顺序不可靠）
            _view.Prepare();
            _hook.Radius = _view.GetColliderRadius(_cfg.hookFallbackRadius);
            DOTween.Init();
            ResetDive();
        }

        public Hook Hook => _hook;

        /// <summary>鱼钩根节点的 Transform。</summary>
        public Transform Root => _view != null ? _view.transform : null;

        /// <summary>GameMgr 销毁时调用，避免补间残留。</summary>
        public void Dispose()
        {
            _yTween?.Kill();
            _yTween = null;
        }

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
        /// 每帧的横向控制
        /// </summary>
        public void TickHorizontal(float dt, bool canControl, InputController input)
        {
            float limit = ViewportUtil.HalfWidth(_cam) * _cfg.hookXLimitRatio;
            if (canControl)
            {
                if (input is { PointerActive: true })
                {
                    float target = Mathf.Clamp(input.PointerWorldX, -limit, limit);
                    _hook.X = Mathf.MoveTowards(_hook.X, target, _cfg.hookMoveSpeed * dt);
                }
            }
            else
                _hook.X = Mathf.MoveTowards(_hook.X, 0f, _cfg.hookReturnSpeed * dt);

            ApplyTransform();
        }

        /// 把当前深度同步给鱼钩的纵向表现。
        /// <param name="descending">true = 下潜，false = 上浮</param>
        public void ApplyDepth(float depth, bool descending, float speed)
        {
            VerticalPhase phase = ResolvePhase(depth, descending);
            if (phase == _phase) return;

            _phase = phase;
            EnterPhase(phase, depth, speed);
        }

        #region 碰撞

        /// <summary>钩子的世界包围盒，供碰撞判定使用。</summary>
        public Bounds GetInteractBounds() => _view.ColliderBounds;

        /// <summary>
        /// 下潜阶段：碰到鱼扣血。
        /// </summary>
        public void CheckHurt(GameModel model, IReadOnlyList<Fish> fishList, float dt)
        {
            if (_hurtTimer > 0f) _hurtTimer -= dt;
            if (model == null || model.IsDead || fishList == null) return;

            Vector2 center = GetInteractBounds().center;
            foreach (var fish in fishList)
            {
                if (fish.IsCaught || fish.HasHitHook) continue;

                if (!MathUtil.CircleIntersectsBounds(center, _hook.Radius, fish.WorldBounds)) continue;

                fish.HasHitHook = true;
                if (_hurtTimer > 0) continue;
                int real = model.ApplyDamage(fish.Data.damage);
                if (real <= 0) continue;
                _hurtTimer = _cfg.hurtCooldown;
                PlayHurtFeedback();
                EventMgr.Publish(GameEvent.FishHurt, real);
                if (model.IsDead) return;
            }
        }

        /// <summary>上浮阶段：碰到鱼就抓住挂到钩上。</summary>
        public void CheckCatch(GameModel model, IReadOnlyList<Fish> fishList)
        {
            if (model == null || model.IsInventoryFull || fishList == null) return;

            Vector2 center = GetInteractBounds().center;
            // 倒序遍历：抓住的鱼会马上被标记，后面的逻辑不再需要它们
            for (int i = fishList.Count - 1; i >= 0; i--)
            {
                Fish fish = fishList[i];
                if (fish == null || fish.IsCaught || fish.Data == null) continue;
                if (!MathUtil.CircleIntersectsBounds(center, _hook.Radius, fish.WorldBounds)) continue;

                model.AddCaught(fish.Data);
                AttachCaught(fish);
            }
        }

        /// <summary>把一条鱼挂到钩上（表现层换父节点，逻辑层记进 Hook.Caught）。</summary>
        public void AttachCaught(Fish fish)
        {
            if (fish == null)
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
        public void PlayHurtFeedback() => _view?.PlayHurtFeedback();

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

        private static float TimeFor(float distance, float speed)
        {
            return Mathf.Max(0.08f, distance / Mathf.Max(0.01f, speed));
        }

        private void TweenY(float targetY, float duration, Ease ease)
        {
            _yTween?.Kill();
            _yTween = DOTween.To(() => _visualY, v => _visualY = v, targetY, duration).SetEase(ease);
        }

        private void ApplyTransform()
        {
            Transform root = Root;
            if (root == null) return;

            Vector3 p = root.position;
            root.position = new Vector3(_hook.X, _visualY, p.z);

            _hook.Y = _visualY;
            // 用实测的钩子包围盒刷新碰撞半径
            _hook.Radius = _view.GetColliderRadius(_cfg.hookFallbackRadius);
        }
    }
}