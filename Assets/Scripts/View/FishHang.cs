using Controller;
using Core;
using DG.Tweening;
using UnityEngine;

namespace View
{
    /// <summary>
    /// 被勾住后的"悬挂 + 平滑转向 + 摆动"表现。**不创建任何 pivot 父物体。**
    ///
    /// 核心技巧：**"先转、再把嘴平移回钩子" == "绕嘴旋转"**。
    /// 嘴是鱼的子物体，旋转后它的世界位置会跟着变；这时把 (钩子位置 - 嘴位置) 补到鱼身上，
    /// 嘴就精确贴回钩子，视觉上鱼就是绕着自己的嘴在摆 —— 不需要额外的支点物体。
    ///
    /// 每帧三步：
    ///   ① 用钩子横向速度算出摆角（钟摆）
    ///   ② 姿态 = 平滑转向"朝上" × 摆动角
    ///   ③ 把嘴平移到钩子上
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)] // 晚于 HookView 执行，保证读到本帧最新的钩子位置
    public class FishHang : MonoBehaviour
    {
        private Animator _animator;

        [Header("嘴部")] [Tooltip("鱼嘴位置的空物体。留空会按名字在子物体里找 mouth / Mouth")] [SerializeField]
        private Transform _mouth;

        [Tooltip("没配嘴部空物体时，按这个偏移量估算（从轴心沿鼻子方向）")] [SerializeField]
        private float headOffsetFallback = 0.45f;

        [Header("转向")] [Tooltip("从被抓住的姿态平滑转到\"朝上\"的时长")] [SerializeField]
        private float alignDuration = 0.25f;

        [Header("摆动")] [Tooltip("钩子横向速度 → 摆角的换算系数，越大摆得越夸张")] [SerializeField]
        private float swingScale = 3.5f;

        [Tooltip("最大摆角（度）")] [SerializeField] private float maxAngle = 55f;

        [Tooltip("摆动阻尼时间，越大越黏、越小越跟手")] [SerializeField]
        private float damp = 0.22f;

        [Header("扇形展开")] [Tooltip("第 1 条挂在正下方；之后左右交替，每往后一条多偏这么多度。因为嘴是支点，倾角本身就把身体推开了")] [SerializeField]
        private float fanStep = 14f;

        private FishView _fish;
        private FishController _controller;

        /// <summary>当前挂在钩上的所有鱼。结算时要让它们一起散开，所以在这里登记。</summary>
        public static readonly System.Collections.Generic.List<FishHang> Hanging = new();

        private bool _hanging;
        private bool _settling; // 正在播结算散开动画，不再做每帧的"钉住"
        private int _score;
        private float _align; // 0 → 1 的"转向朝上"进度，由 DOTween 驱动
        private Quaternion _catchRot; // 抓住那一刻的朝向
        private Quaternion _hangRot; // 目标朝向：鼻子朝上
        private Tween _alignTween;

        private float _angle;
        private float _angleVel;
        private float _lastHookX;
        private float _fanAngle; // 这一条的初始悬挂倾角（扇形展开用）

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _fish = GetComponent<FishView>();
        }

        private void Update()
        {
            // 结算动画期间不再接管位置，交给 DOTween 播散开
            if (_settling) return;

            if (_controller == null)
            {
                _controller = GameMgr.Instance != null ? GameMgr.Instance.FishCtrl : null;
                if (_controller == null) return;
            }

            if (_fish == null) return;

            // id 还没认领（刚出池）时先不管
            if (_fish.Id < 0)
            {
                if (_hanging) Release();
                return;
            }

            FishRuntime state = _controller.Get(_fish.Id);

            // 鱼被回收了
            if (state == null)
            {
                if (_hanging) Release();
                return;
            }

            if (!state.IsCaught)
            {
                if (_hanging) Release();
                return;
            }

            if (!_hanging) Catch(state);

            Hang(Time.deltaTime);
        }

        private void OnDisable() => Release();

        // ------------------------------------------------------------------

        /// <summary>抓住的瞬间记录起始姿态，并开始"平滑转向朝上"。</summary>
        private void Catch(FishRuntime state)
        {
            _animator.Play("Death");
            _hanging = true;
            _settling = false;
            _score = state.Data?.score ?? 0;

            if (!Hanging.Contains(this)) Hanging.Add(this);

            // 第几条决定扇形展开的倾角：第 1 条正下方，之后左右交替越偏越多
            _fanAngle = FanAngle(state.CatchSlot, fanStep);

            _catchRot = transform.rotation;
            // 鼻子（transform.forward）朝上。用 FromToRotation 而不是写死角度，
            // 左右朝向的鱼都能正确转过来
            _hangRot = Quaternion.FromToRotation(transform.forward, Vector3.up) * transform.rotation;

            _align = 0f;
            _alignTween?.Kill();
            _alignTween = DOTween.To(() => _align, v => _align = v, 1f, alignDuration)
                .SetEase(Ease.OutQuad);

            _angle = 0f;
            _angleVel = 0f;
            _lastHookX = HookPosition.x;
        }

        private void Release()
        {
            _hanging = false;
            _settling = false;
            Hanging.Remove(this);
            _alignTween?.Kill();
            _alignTween = null;
            _align = 0f;
            _angle = 0f;
            _angleVel = 0f;

            // 散开动画会把鱼缩放成 0，回收前还原，免得下次从池子里出来是缩着的
            transform.localScale = Vector3.one;
        }

        // ==================================================================
        // 结算：散开 + 缩小到 0 → 回收 → 冒出飘字
        // ==================================================================

        /// <summary>让所有还挂在钩上的鱼一起散开并缩小。</summary>
        public static void DisperseAll(float duration, float radius)
        {
            for (int i = Hanging.Count - 1; i >= 0; i--)
            {
                FishHang fish = Hanging[i];
                if (fish != null) fish.Disperse(duration, radius);
            }
        }

        /// <summary>往一个随机方向散开，同时缩小到 0；缩完回收并报一个飘字事件。</summary>
        private void Disperse(float duration, float radius)
        {
            _settling = true;
            _alignTween?.Kill();
            _alignTween = null;

            Vector2 dir = Random.insideUnitCircle.normalized;
            Vector3 target = transform.position + new Vector3(dir.x, dir.y, 0f) * radius;

            Sequence seq = DOTween.Sequence();
            seq.Join(transform.DOMove(target, duration).SetEase(Ease.OutQuad));
            seq.Join(transform.DOScale(Vector3.zero, duration).SetEase(Ease.InQuad));
            seq.OnComplete(() =>
            {
                // 缩小完毕：先在原地冒出 +分数，再把自己还回对象池
                EventMgr.Publish(GameEvent.SettleFishBurst, new FishBurstPayload
                {
                    WorldPos = transform.position,
                    Score = _score,
                });

                int id = _fish != null ? _fish.Id : -1;
                FishController ctrl = _controller;
                Hanging.Remove(this);
                _settling = false;
                _hanging = false;

                if (ctrl != null && id >= 0) ctrl.Release(id, gameObject);
            });
        }

        /// <summary>每帧把鱼"钉"在钩子上：先转、再把嘴平移回去。</summary>
        private void Hang(float dt)
        {
            Transform hook = GameMgr.Instance != null ? GameMgr.Instance.HookRoot : null;
            if (hook == null || dt <= 0f) return;

            // ① 先算摆动角再应用，否则姿态会晚一帧
            float hookX = hook.position.x;
            float vx = (hookX - _lastHookX) / dt;
            _lastHookX = hookX;

            // 钩子往右跑，鱼身往左甩 —— 取负号
            float target = Mathf.Clamp(-vx * swingScale, -maxAngle, maxAngle);
            _angle = Mathf.SmoothDamp(_angle, target, ref _angleVel, damp);

            // ② 姿态 = 平滑转向"朝上" × （扇形倾角 + 摆动角）。
            //    扇形角乘 _align，让它跟着转向动画一起张开，不会一上钩就弹到最终角度
            Quaternion facing = Quaternion.Slerp(_catchRot, _hangRot, _align);
            transform.rotation = Quaternion.Euler(0f, 0f, _fanAngle * _align + _angle) * facing;

            // ③ 把嘴挪回钩子上。
            //    嘴是子物体，旋转后世界位置变了，这一步等价于"绕嘴旋转"——
            //    所以不需要建 pivot 父物体。
            transform.position += hook.position - MouthPosition;
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// 扇形展开角：第 1 条正下方，之后左右交替、越往后偏得越多。
        /// 0 → 0°，1 → -step（左），2 → +step（右），3 → -2·step，4 → +2·step …
        /// 绕 Z 轴正转会把下垂的身体推向 +X（右），所以往左取负号。
        ///
        /// 公开是为了让教程里的道具鱼（TutorialFishView）复用同一套角度规则，
        /// 改这里两边的观感会一起变。
        /// </summary>
        public static float FanAngle(int slot, float step)
        {
            if (slot <= 0) return 0f;

            int side = (slot % 2 == 1) ? -1 : 1;
            int distance = (slot + 1) / 2; // 1,1,2,2,3,3…
            return side * distance * step;
        }

        private Vector3 HookPosition =>
            GameMgr.Instance != null && GameMgr.Instance.HookRoot != null
                ? GameMgr.Instance.HookRoot.position
                : transform.position;

        /// <summary>鱼嘴的世界位置：优先用空物体，没配就按鼻子方向的偏移估算。</summary>
        private Vector3 MouthPosition
        {
            get
            {
                Transform mouth = Mouth;
                return mouth != null
                    ? mouth.position
                    : transform.position + transform.forward * headOffsetFallback;
            }
        }

        private Transform Mouth
        {
            get
            {
                if (_mouth != null) return _mouth;

                _mouth = transform.Find("mouth");
                if (_mouth == null) _mouth = transform.Find("Mouth");

                return _mouth;
            }
        }

        private void OnDrawGizmosSelected()
        {
            Transform mouth = Mouth;
            if (mouth == null) return;

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(mouth.position, 0.08f);
        }
    }
}