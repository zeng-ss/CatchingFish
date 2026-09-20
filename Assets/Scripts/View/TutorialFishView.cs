using UnityEngine;

namespace View
{
    /// <summary>
    /// 【表现层】教程布景里的"道具鱼"：只负责摆拍 —— 横向游动、被钩住后跟着钩子走。
    ///
    /// 它**不是游戏里的鱼**：Awake 里会把身上那套游戏组件（FishView / FishHang）关掉，
    /// 因为它们的运行时要靠 `GameMgr.Instance` 找鱼槽位，教程的鱼去注册就会污染玩家那一局的数据。
    ///
    /// 姿势与摆动沿用 <see cref="FishHang"/> 的规则（同一个 `FanAngle` + 同样的"绕嘴旋转"），
    /// 所以教程和实机的观感一致。
    /// </summary>
    [DisallowMultipleComponent]
    public class TutorialFishView : MonoBehaviour
    {
        [Tooltip("被钩住后，嘴到钩子的距离")] [SerializeField]
        private float mouthOffset = 0.35f;

        [Tooltip("扇形展开每一步偏多少度（和 FishHang 保持一致）")] [SerializeField]
        private float fanStep = 14f;

        private static readonly Quaternion FaceRight = Quaternion.Euler(0f, 90f, 0f);
        private static readonly Quaternion FaceLeft = FaceRight * Quaternion.Euler(0f, 180f, 0f);

        private Animator _animator;
        private float _planeZ; // 和鱼钩同一个 Z 平面：要挡在背景贴图前面（游戏里也是这么摆的）
        private int _direction = 1;
        private bool _moving;
        private bool _attached;

        private Transform _hook;
        private Quaternion _hangRot;
        private float _fanAngle;

        /// <summary>横向游速（世界单位/秒）。由 TutorialDirector 按教程节奏统一设定。</summary>
        public float SwimSpeed { get; set; } = 1.6f;

        private void Awake()
        {
            // 摘掉游戏侧的行为，只留"外观"
            if (GetComponent<FishView>() is { } gameplay) gameplay.enabled = false;
            if (GetComponent<FishHang>() is { } hang) hang.enabled = false;

            _animator = GetComponent<Animator>();
        }

        private void OnDisable()
        {
            _moving = false;
            _attached = false;
        }

        // ==================================================================
        // 给 TutorialDirector 用的接口
        // ==================================================================

        /// <summary>
        /// 出场：站到泳道 <paramref name="laneY"/> 上，从钩子的 <paramref name="side"/> 一侧游过来，
        /// <paramref name="travel"/> 秒之后正好经过钩子的横坐标 —— 导演靠这个把"相遇时刻"算准。
        /// </summary>
        /// <param name="side">+1 从右边来（往左游），-1 从左边来。</param>
        public void SwimIn(float laneY, float hookX, float planeZ, int side, float travel)
        {
            _planeZ = planeZ;
            _direction = side >= 0 ? -1 : 1;
            _attached = false;
            _moving = true;

            transform.localScale = Vector3.one;
            transform.localRotation = _direction > 0 ? FaceRight : FaceLeft;
            transform.position = new Vector3(hookX + side * SwimSpeed * Mathf.Max(0.01f, travel), laneY, _planeZ);

            gameObject.SetActive(true);
        }

        /// <summary>被钩住：开始跟着钩子走，并按扇形角展开。</summary>
        public void Attach(Transform hook, int slot)
        {
            _hook = hook;
            _moving = false;
            _attached = true;

            _fanAngle = FishHang.FanAngle(slot, fanStep);
            // 鼻子（transform.forward）朝上；用 FromToRotation 而不是写死角度，左右朝向的鱼都能转对
            _hangRot = Quaternion.FromToRotation(transform.forward, Vector3.up) * transform.rotation;

            PlayDeath();
        }

        /// <summary>收起来（重播前复位用）。</summary>
        public void Hide()
        {
            _moving = false;
            _attached = false;
            _hook = null;
            transform.localScale = Vector3.one;
            gameObject.SetActive(false);
        }

        // ==================================================================
        // 每帧表现
        // ==================================================================

        private void Update()
        {
            if (!_moving || _attached) return;

            Vector3 p = transform.position;
            p.x += _direction * SwimSpeed * Time.deltaTime;
            transform.position = p;
        }

        private void LateUpdate()
        {
            if (!_attached) return;

            // ① 姿态 = 扇形倾角 × （鼻子朝上的定姿）
            transform.rotation = Quaternion.Euler(0f, 0f, _fanAngle) * _hangRot;

            // ② 把嘴挪回钩子上 == 绕嘴旋转（嘴是子物体，旋转后世界位置变了），不需要额外 pivot
            transform.position += _hook.position - MouthPosition;
        }

        /// <summary>鱼嘴的世界位置（沿鼻子方向偏移）。</summary>
        private Vector3 MouthPosition => transform.position + transform.forward * mouthOffset;

        private void PlayDeath()
        {
            if (_animator == null || _animator.runtimeAnimatorController == null)
            {
                return;
            }

            int hash = Animator.StringToHash("Death");
            if (_animator.HasState(0, hash))
            {
                _animator.Play(hash);
            }
        }
    }
}
