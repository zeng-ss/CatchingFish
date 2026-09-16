using Controller;
using Core;
using Tool;
using UnityEngine;

namespace View
{
    /// <summary>
    /// 一条鱼的表现层：**不持有数据，也不持有控制层的运行数据**。
    ///
    /// 它只做两件事：
    ///   1) 出场时把自己"注册"到 FishController（报上初始位置和包围盒尺寸）；
    ///   2) 每帧从 FishController 按 id "拉"最新状态并应用到 Transform。
    ///
    /// 于是依赖方向是单向的 View → Controller：控制层完全不知道这些对象的存在。
    /// </summary>
    [DisallowMultipleComponent]
    public class FishView : MonoBehaviour
    {
        [Header("被抓反馈")] [SerializeField] private float catchPunch = 0.25f;
        [Tooltip("缩放下沉的快慢")] [SerializeField] private float punchDamp = 8f;

        private FishController _controller; // View → Controller（单向）
        private int _id = -1;
        private bool _wasCaught;

        private Renderer[] _renderers;
        private Quaternion _faceRight, _faceLeft;
        private int _appliedDirection;
        private Vector3 _baseScale = Vector3.one;
        private float _scaleMultiplier = 1f;
        private float _punchTimer;

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _baseScale = transform.localScale;

            Quaternion right = Quaternion.Euler(0f, 90f, 0f);
            _faceRight = right;
            _faceLeft = right * Quaternion.Euler(0f, 180f, 0f);
        }

        /// <summary>从对象池取出、被启用时注册给控制层。</summary>
        private void OnEnable()
        {
            _wasCaught = false;
            _appliedDirection = 0;
            _punchTimer = 0f;

            _controller = GameMgr.Instance != null ? GameMgr.Instance.FishCtrl : null;
            if (_controller == null) return;

            // 尺寸由表现层实测后报给控制层（方向 View → Controller）
            _id = _controller.Register(transform.position,
                ViewportUtil.ComputeWorldBounds(_renderers, transform.position).size);
        }

        private void OnDisable()
        {
            _controller.Unregister(_id);
            _id = -1;
            _controller = null;
        }

        /// <summary>
        /// 每帧从控制层"拉"状态。鱼的游动、回收全部由控制层在纯数据上算，
        /// 表现层只负责把结果贴到 Transform 上。
        /// </summary>
        private void Update()
        {
            if (_controller == null || _id < 0) return;

            FishRuntime state = _controller.Get(_id);
            if (state == null) return;

            if (state.IsCaught)
            {
                if (!_wasCaught) OnCaught();
            }
            else
            {
                transform.position = state.Position;
                SetDirection(state.Direction);
            }

            _wasCaught = state.IsCaught;
            if (_punchTimer > 0f)
            {
                _punchTimer = Mathf.Max(0f, _punchTimer - Time.deltaTime * punchDamp);
                ApplyScale();
            }
        }

        /// <summary>被抓住：挂到钩子下面（自己动手，控制层不需要认识任何 View）。</summary>
        private void OnCaught()
        {
            Transform hook = GameMgr.Instance.HookView.transform;
            transform.SetParent(hook, true);
            _punchTimer = 1f;
            ApplyScale();
        }

        /// <summary>开局时由控制层选好鱼种后调用，设置只属于"表现"的缩放倍率。</summary>
        public void SetScale(float multiplier)
        {
            _scaleMultiplier = multiplier > 0f ? multiplier : 1f;
            ApplyScale();
        }

        private void SetDirection(int direction)
        {
            int dir = direction >= 0 ? 1 : -1;
            if (_appliedDirection == dir) return;

            _appliedDirection = dir;
            transform.localRotation = dir > 0 ? _faceRight : _faceLeft;
        }

        private void ApplyScale()
        {
            float punch = _punchTimer > 0f ? 1f + catchPunch * _punchTimer : 1f;
            transform.localScale = _baseScale * (_scaleMultiplier * punch);
        }
    }
}