using Controller;
using Core;
using DG.Tweening;
using UnityEngine;

namespace View
{
    /// <summary>
    /// 【表现层】被勾住之后的"悬挂 + 转向 + 摆动"。
    ///
    /// 核心技巧：**"先转、再把嘴平移回钩子" 等价于 "绕嘴旋转"**。
    /// 嘴是子物体，旋转后它的世界位置会跟着变，这时把 (钩子位置 − 嘴位置) 补到鱼身上，
    /// 嘴就精确贴回钩子 —— 视觉上鱼就是绕着自己的嘴在摆，不需要额外的支点物体。
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(100)] // 晚于 HookView，保证读到本帧最新的钩子位置
    public class FishHang : MonoBehaviour
    {
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

        [Header("扇形展开")]
        [Tooltip("第 1 条挂在正下方；之后左右交替，每往后一条多偏这么多度。嘴是支点，倾角本身就把身体推开了")]
        [SerializeField]
        private float fanStep = 14f;

        /// <summary>当前挂在钩上的所有鱼，结算时一起散开。</summary>
        public static readonly System.Collections.Generic.List<FishHang> Hanging = new();

        private Animator _animator;
        private FishView _fish;
        private FishController _controller;

        private bool _hanging;
        private bool _settling; // 正在播结算散开动画，不再做每帧的"钉住"
        private int _score;
        private float _align;   // 0 → 1 的"转向朝上"进度，由 DOTween 驱动
        private Quaternion _catchRot;
        private Quaternion _hangRot;
        private Tween _alignTween;
        private float _angle;
        private float _angleVel;
        private float _lastHookX;
        private float _fanAngle;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _fish = GetComponent<FishView>();
        }

        private void Update()
        {
            if (_settling) return; // 结算动画期间交给 DOTween 播散开

            if (_controller == null)
            {
                _controller = GameMgr.Instance != null ? GameMgr.Instance.FishCtrl : null;
                if (_controller == null) return;
            }

            // 还没认领到槽位 / 数据已经回收 / 还没被抓住 —— 都不该挂在钩上
            FishRuntime state = _fish != null && _fish.Id >= 0 ? _controller.Get(_fish.Id) : null;
            if (state == null || !state.IsCaught)
            {
                if (_hanging) Release();
                return;
            }

            if (!_hanging) Catch(state);

            Hang(Time.deltaTime);
        }

        private void OnDisable() => Release();

        // ==================================================================

        /// <summary>抓住的瞬间记录起始姿态，并开始"平滑转向朝上"。</summary>
        private void Catch(FishRuntime state)
        {
            _animator.Play("Death");
            _hanging = true;
            _settling = false;
            _score = state.Data.score;

            if (!Hanging.Contains(this)) Hanging.Add(this);

            // 第几条决定扇形展开的倾角：第 1 条正下方，之后左右交替越偏越多。
            // 倾角乘上 _align，让它跟着转向动画一起张开，不会一上钩就弹到最终角度
            _fanAngle = FanAngle(state.CatchSlot, fanStep);

            _catchRot = transform.rotation;
            // 鼻子（transform.forward）朝上；用 FromToRotation 而不是写死角度，左右朝向的鱼都能转对
            _hangRot = Quaternion.FromToRotation(transform.forward, Vector3.up) * transform.rotation;

            _align = 0f;
            _alignTween?.Kill();
            _alignTween = DOTween.To(() => _align, v => _align = v, 1f, alignDuration).SetEase(Ease.OutQuad);

            _angle = 0f;
            _angleVel = 0f;
            Transform hook = Hook;
            _lastHookX = hook != null ? hook.position.x : transform.position.x;
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

            // 散开动画把鱼缩放成 0，回收前还原，免得下次出池是缩着的
            transform.localScale = Vector3.one;
        }

        /// <summary>每帧把鱼"钉"在钩子上：先转、再把嘴平移回去（见类注释）。</summary>
        private void Hang(float dt)
        {
            Transform hook = Hook;
            if (hook == null || dt <= 0f) return;

            // ① 先用钩子的横向速度算摆动角
            float hookX = hook.position.x;
            float vx = (hookX - _lastHookX) / dt;
            _lastHookX = hookX;

            float target = Mathf.Clamp(-vx * swingScale, -maxAngle, maxAngle); // 钩往右，鱼身往左甩
            _angle = Mathf.SmoothDamp(_angle, target, ref _angleVel, damp);

            // ② 姿态 = 平滑转向"朝上" × （扇形倾角 + 摆动角）
            transform.rotation =
                Quaternion.Euler(0f, 0f, _fanAngle * _align + _angle) * Quaternion.Slerp(_catchRot, _hangRot, _align);

            // ③ 把嘴挪回钩子上
            transform.position += hook.position - MouthPosition;
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
                // 先报一个飘字事件，再把自己还回对象池
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

                if (id >= 0) ctrl?.Release(id, gameObject);
            });
        }

        // ==================================================================

        /// <summary>
        /// 扇形展开角：第 1 条正下方，之后左右交替、越往后偏得越多。
        /// 0 → 0°，1 → -step（左），2 → +step（右），3 → -2·step，4 → +2·step …
        /// 绕 Z 轴正转会把下垂的身体推向 +X（右），所以往左取负号。
        /// （教程里的道具鱼也用这套规则，改这里两边一起变）
        /// </summary>
        public static float FanAngle(int slot, float step)
        {
            if (slot <= 0) return 0f;

            int side = slot % 2 == 1 ? -1 : 1;
            int distance = (slot + 1) / 2; // 1,1,2,2,3,3…
            return side * distance * step;
        }

        private static Transform Hook =>
            GameMgr.Instance != null ? GameMgr.Instance.HookRoot : null;

        /// <summary>鱼嘴的世界位置：优先用空物体，没配就按鼻子方向的偏移估算。</summary>
        private Vector3 MouthPosition =>
            Mouth != null ? Mouth.position : transform.position + transform.forward * headOffsetFallback;

        private Transform Mouth
        {
            get
            {
                if (_mouth == null)
                {
                    _mouth = transform.Find("mouth") ?? transform.Find("Mouth");
                }

                return _mouth;
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (Mouth == null) return;

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(Mouth.position, 0.08f);
        }
    }
}
