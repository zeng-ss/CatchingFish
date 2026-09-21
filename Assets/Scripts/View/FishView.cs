using Controller;
using Core;
using UnityEngine;

namespace View
{
    /// <summary>
    /// 【表现层】一条鱼。
    ///
    /// 依赖方向 View → Controller，而且**只依赖 `IFishStateSource` 这个接口**：
    ///   ① 自己被启用时认领一个槽位 id（`Register`）；
    ///   ② 之后每帧按 id 把状态**拉**回来贴到 Transform 上（`Get`）；
    ///   ③ 控制层说这条鱼没了（返回 null），自己回对象池（`Release`）。
    /// </summary>
    [DisallowMultipleComponent]
    public class FishView : MonoBehaviour
    {
        /// <summary>控制层分配的槽位 id；Trigger 回调时用它告诉控制层碰到了哪条鱼。</summary>
        public int Id => _id;

        private IFishStateSource _fish;
        private int _id = -1;
        private Quaternion _faceRight, _faceLeft;
        private int _appliedDirection;

        private void Awake()
        {
            // 位置每帧由这里手写，物理必须让路
            Rigidbody body = GetComponent<Rigidbody>() ?? gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            // 被勾住后的"绕嘴摆动"表现，在这里自动补上，免得去改 9 个预制体
            if (GetComponent<FishHang>() == null) gameObject.AddComponent<FishHang>();

            _faceRight = Quaternion.Euler(0f, 90f, 0f);
            _faceLeft = _faceRight * Quaternion.Euler(0f, 180f, 0f);
        }

        private void OnEnable()
        {
            _appliedDirection = 0;
            _id = -1;
            _fish = GameMgr.Instance != null ? GameMgr.Instance.FishCtrl : null;
        }

        private void OnDisable()
        {
            if (_id >= 0) _fish?.Unregister(_id);
            _id = -1;
            _fish = null;
        }

        private void Update()
        {
            if (_fish == null) return;

            // 槽位要在 Update 里领而不是 OnEnable：对象池预热时 Instantiate 会同步触发 OnEnable，
            // 那一刻还没有待认领的鱼，在 OnEnable 里领会把别的鱼的数据抢过来
            if (_id < 0) _id = _fish.Register(transform.position, MeasureSize());
            if (_id < 0) return;

            FishRuntime state = _fish.Get(_id);
            if (state == null)
            {
                _fish.Release(_id, gameObject); // 控制层已回收，自己回池
                return;
            }

            if (state.IsCaught) return; // 挂在钩上的鱼由 FishHang 接管

            // **只写 X**：纵向留给父节点（BG 滚动）带着走；
            // 整条 position 都写会把鱼钉死在屏幕上，背景就带不动它了
            Vector3 pos = transform.position;
            pos.x = state.Position.x;
            transform.position = pos;

            SetDirection(state.Direction);
        }

        /// <summary>包围盒尺寸直接取 Collider —— 模型缩放、蒙皮动画都不用管。</summary>
        private Vector3 MeasureSize()
        {
            Collider c = GetComponentInChildren<Collider>();
            return c != null ? c.bounds.size : Vector3.one;
        }

        private void SetDirection(int direction)
        {
            int dir = direction >= 0 ? 1 : -1;
            if (_appliedDirection == dir) return;

            _appliedDirection = dir;
            transform.localRotation = dir > 0 ? _faceRight : _faceLeft;
        }
    }
}