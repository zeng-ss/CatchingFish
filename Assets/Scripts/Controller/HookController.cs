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
    /// 鱼钩控制器
    /// 一趟下潜由"鱼钩自己在动"和"背景在动"两段拼成，DOTween 负责其中的鱼钩位移：
    ///
    ///   ① 抛钩 Casting        深度 0 → CastDepth            背景不动，鱼钩落到屏幕中间
    ///   ② 常规下潜 Cruising    深度 → FinalDiveStartDepth    鱼钩停中间，背景滚动
    ///   ③ 触底冲刺 FinalDive   深度 → maxDepth                背景停住，鱼钩继续往屏幕底部探
    ///   ④ 收线起钩 PullUp      反向                           背景不动，鱼钩先回到中间
    ///   ⑤ 常规上浮 Cruising    反向                           背景往回滚
    ///   ⑥ 收钩出水 ReelingOut  深度 → 0                       鱼钩收回抛钩起点
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

        private readonly List<FishRuntime> _caught = new();
        private float _x;
        private float _visualY;

        private float _hurtTimer;
        private Tween _yTween;
        private VerticalPhase _phase = VerticalPhase.None;

        private readonly FishController _fishCtrl;

        /// <param name="model">数据层，控制层持有它（Controller → Model）。</param>
        /// <param name="fishCtrl">同层控制层，用来按 id 查鱼的数据（Controller → Controller）。</param>
        /// <param name="planeZ">鱼钩所在的世界 Z 平面。</param>
        public HookController(GameModel model, FishController fishCtrl, Camera cam, float planeZ)
        {
            _model = model;
            _fishCtrl = fishCtrl;
            _cam = cam;
            _planeZ = planeZ;
            _cfg = GameConfig.Get();
            DOTween.Init();
            ResetDive();
        }

        // 挂在钩上的鱼，按抓取顺序排列
        public IReadOnlyList<FishRuntime> Caught => _caught;

        // 鱼钩当前的世界坐标
        public Vector3 Position => new(_x, _visualY, _planeZ);

        // 回到起点
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

        // isDown true = 下潜，false = 上浮
        public void ApplyDepth(float depth, bool isDown, float speed)
        {
            // 获取纵向分段
            VerticalPhase phase = ResolvePhase(depth, isDown);
            if (phase == _phase) return;
            _phase = phase;
            EnterPhase(phase, depth, speed);
        }

        #region 碰撞

        public bool OnHookTouchedFish(int fishId)
        {
            if (_model == null) return false;
            FishRuntime fish = _fishCtrl?.Get(fishId);
            if (fish?.Data == null || fish.IsCaught) return false;
            switch (GameMgr.Instance.State)
            {
                case GameState.CastingDown:
                    HandleHurt(fish);
                    return false; // 只扣血，不挂
                case GameState.ReelingUp:
                    HandleCatch(fish);
                    return true; // 抓住才挂
            }

            return false;
        }

        /// <summary>
        /// 撞鱼扣血
        /// </summary>
        private void HandleHurt(FishRuntime fish)
        {
            if (fish.HasHitHook || _model.IsDead) return;
            if (_hurtTimer > 0f) return;
            int real = _model.ApplyDamage(fish.Data.damage);
            if (real <= 0) return;
            fish.HasHitHook = true;
            _hurtTimer = _cfg.hurtCooldown;

            // 表现层订阅这个事件做受击闪烁（闪烁时长也取 hurtCooldown，节奏天然一致）
            EventMgr.Publish(GameEvent.FishHurt, real);
        }

        /// <summary>
        /// 碰鱼抓取
        /// </summary>
        private void HandleCatch(FishRuntime fish)
        {
            if (_model.IsInventoryFull) return;
            _model.AddCaught(fish.Data);
            fish.IsCaught = true;
            fish.CatchSlot = _caught.Count;   // 第几条：表现层靠它决定扇形展开的倾角
            _caught.Add(fish);
        }

        public void TickHurtCoolTime(float dt)
        {
            if (_hurtTimer > 0f) _hurtTimer -= dt;
        }

        #endregion

        // 纵向分段
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
                    TweenY(_cfg.hookMiddleY, (_cfg.CastDepth - depth) / speed, Ease.Linear);
                    break;

                // ②⑤ 常规段：鱼钩停在屏幕中间，位移交给背景
                case VerticalPhase.Cruising:
                    TweenY(_cfg.hookMiddleY, 0.12f, Ease.OutQuad);
                    break;

                // ③ 触底冲刺：背景停住，鱼钩继续往屏幕底部探
                case VerticalPhase.FinalDive:
                    TweenY(_cfg.hookBottomY, (_cfg.maxDepth - depth) / speed, Ease.Linear);
                    break;

                // ④ 收线起钩：背景不动，鱼钩先回到屏幕中间
                case VerticalPhase.PullUp:
                    TweenY(_cfg.hookMiddleY, (depth - _cfg.FinalDiveStartDepth) / speed, Ease.Linear);
                    break;

                // ⑥ 收钩出水：鱼钩回到抛钩起点
                case VerticalPhase.ReelingOut:
                    TweenY(_cfg.hookStartY, depth / speed, Ease.Linear);
                    break;
            }
        }

        private void TweenY(float targetY, float duration, Ease ease)
        {
            _yTween?.Kill();
            _yTween = DOTween.To(() => _visualY, v => _visualY = v, targetY, duration).SetEase(ease);
        }
    }
}