using Controller;
using Core;
using UnityEngine;

namespace View
{
    [DisallowMultipleComponent]
    public class FishView : MonoBehaviour
    {
        private FishController _controller;
        private int _id = -1;

        /// <summary>控制层分配的数据槽 id；Trigger 回调时用它告诉控制层碰到了哪条鱼。</summary>
        public int Id => _id;

        private Quaternion _faceRight, _faceLeft;
        private int _appliedDirection;

        private void Awake()
        {
            if (GetComponent<Rigidbody>() == null)
            {
                Rigidbody rb = gameObject.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                rb.useGravity = false;
            }

            // 被勾住后的"以头部为支点摆动"表现；在这里自动补上，免得去改 9 个预制体
            if (GetComponent<FishHang>() == null) gameObject.AddComponent<FishHang>();

            Quaternion right = Quaternion.Euler(0f, 90f, 0f);
            _faceRight = right;
            _faceLeft = right * Quaternion.Euler(0f, 180f, 0f);
        }

        private void OnEnable()
        {
            _appliedDirection = 0;
            _id = -1;
            _controller = GameMgr.Instance != null ? GameMgr.Instance.FishCtrl : null;
        }

        private void OnDisable()
        {
            if (_controller != null && _id >= 0) _controller.Unregister(_id);
            _id = -1;
            _controller = null;
        }

        /// <summary>
        /// 每帧从控制层"拉"状态。鱼的游动、回收全部由控制层在纯数据上算，
        /// 表现层只负责把结果贴到 Transform 上。
        /// </summary>
        private void Update()
        {
            if (_controller == null) return;

            if (_id < 0)
                _id = _controller.Register(transform.position, MeasureSize());

            if (_id < 0) return;
            FishRuntime state = _controller.Get(_id);
            if (state == null)
            {
                _controller.Release(_id, gameObject);
                return;
            }

            if (!state.IsCaught)
            {
                Vector3 pos = transform.position;
                pos.x = state.Position.x;
                transform.position = pos;
                SetDirection(state.Direction);
            }
        }

        /// <summary>
        /// 包围盒尺寸直接取 Collider
        /// </summary>
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