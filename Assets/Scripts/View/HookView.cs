using Tool;
using UnityEngine;

namespace View
{
    /// <summary>
    /// 鱼钩表现层：钩子本体包围盒 + 鱼线拉伸 + 挂鱼。
    ///
    /// 鱼线是一条被拉伸的 Cube、并且是鱼钩的子物体，所以鱼钩纵向移动时必须
    /// 把线重新拉到竿尖，否则线头会跟着钩子一起往下跑、看起来是断的。
    /// 竿尖锚点是世界坐标，必须在鱼钩被挪动**之前**量出来，所以初始化走 Prepare()，
    /// 由 HookController 在移动鱼钩前显式调用（不能只靠 Awake 的先后顺序）。
    /// </summary>
    [DisallowMultipleComponent]
    public class HookView : MonoBehaviour
    {
        [Header("引用（留空自动在子物体里按名字查找）")] [SerializeField]
        private Transform rope;

        [Tooltip("钩子本体，用它算碰撞包围盒")] [SerializeField]
        private Transform bob;

        [Header("鱼线")] [Tooltip("竿尖的世界高度。留 <= -1000 表示自动读取美术摆好的鱼线顶端")] [SerializeField]
        private float rodTipWorldYOverride = -9999f;

        [Header("受击反馈")] [SerializeField] private float shakeStrength = 0.12f;

        [SerializeField] private float shakeDamp = 12f;

        private Renderer[] _bobRenderers;
        private SpriteRenderer _bobSprite;
        private Vector3 _bobBaseLocalPos;
        private float _shakeTimer;
        private Color _bobBaseColor = Color.white;

        private float _ropeMeshLocalHeight = 1f;
        private Vector3 _ropeBaseScale = Vector3.one;
        private Vector3 _ropeBaseLocalPos;
        private float _rodTipWorldY = 6.6f;
        private bool _prepared;

        /// <summary>
        /// 只做一次的初始化。
        /// 必须由 Awake 或 HookController 在**移动鱼钩之前**调用，否则竿尖高度会被量成错的值。
        /// </summary>
        public void Prepare()
        {
            if (_prepared)
            {
                return;
            }

            _prepared = true;

            // 没接线就按名字兜底，尽量让框架在没摆好的场景里也能跑
            if (rope == null)
            {
                rope = transform.Find("Cube");
            }

            if (bob == null)
            {
                bob = transform.Find("Sphere");
            }

            CacheRefs();
            CacheRope();
        }

        private void Awake()
        {
            Prepare();
        }

        private void CacheRefs()
        {
            if (bob == null)
            {
                return;
            }

            _bobRenderers = bob.GetComponentsInChildren<Renderer>(true);
            _bobBaseLocalPos = bob.localPosition;
            _bobSprite = bob.GetComponentInChildren<SpriteRenderer>(true);
            if (_bobSprite != null)
            {
                _bobBaseColor = _bobSprite.color;
            }
        }

        private void CacheRope()
        {
            if (rope == null)
            {
                return;
            }

            _ropeBaseScale = rope.localScale;
            _ropeBaseLocalPos = rope.localPosition;

            Renderer ropeRenderer = rope.GetComponentInChildren<Renderer>();
            if (ropeRenderer != null)
            {
                // 模型本身（缩放为 1 时）的高度，用来把线拉到任意长度
                _ropeMeshLocalHeight = Mathf.Max(0.0001f, ropeRenderer.localBounds.size.y);
            }

            if (rodTipWorldYOverride > -1000f)
            {
                _rodTipWorldY = rodTipWorldYOverride;
            }
            else if (ropeRenderer != null)
            {
                // 此刻鱼钩还在美术摆的位置上，鱼线顶端就是竿尖
                _rodTipWorldY = ropeRenderer.bounds.max.y;
            }
        }

        /// <summary>钩子的世界包围盒（碰撞判定用）。</summary>
        public Bounds ColliderBounds => ViewportUtil.ComputeWorldBounds(
            _bobRenderers,
            bob != null ? bob.position : transform.position);

        /// <summary>钩子的碰撞半径。</summary>
        public float GetColliderRadius(float fallback)
        {
            if (_bobRenderers == null || _bobRenderers.Length == 0)
            {
                return fallback;
            }

            Bounds b = ColliderBounds;
            float radius = Mathf.Max(b.extents.x, b.extents.y);
            return radius > 0.0001f ? radius : fallback;
        }

        /// <summary>把鱼挂到钩子下面（保持世界位置和朝向，只缩小一点）。</summary>
        public void AttachFish(Transform fish, int index, float scaleMultiplier)
        {
            if (fish == null)
            {
                return;
            }

            Vector3 worldScale = fish.lossyScale;

            // worldPositionStays = true：鱼停在它被抓住的位置，跟着钩子一起走
            fish.SetParent(transform, true);

            // 再按需缩小；除以父节点缩放，钩子根节点不是 1 倍缩放时也不会错
            Vector3 parentScale = transform.lossyScale;
            float factor = Mathf.Max(0.01f, scaleMultiplier);
            fish.localScale = new Vector3(
                worldScale.x * factor / SafeScale(parentScale.x),
                worldScale.y * factor / SafeScale(parentScale.y),
                worldScale.z * factor / SafeScale(parentScale.z));
        }

        private static float SafeScale(float value)
        {
            return Mathf.Approximately(value, 0f) ? 1f : value;
        }

        /// <summary>受击反馈。</summary>
        public void PlayHurtFeedback()
        {
            _shakeTimer = 1f;
        }

        private void LateUpdate()
        {
            RefreshRope();
            RefreshShake();
        }

        /// <summary>让鱼线从钩子一直拉到竿尖。X/Z 保持美术摆好的值，只改 Y 和纵向缩放。</summary>
        private void RefreshRope()
        {
            if (rope == null || rope.parent == null)
            {
                return;
            }

            Transform parent = rope.parent;
            float px = rope.position.x;
            float pz = rope.position.z;

            // 底端以"钩子本体"为准：钩子根节点和钩子本体之间还有一段偏移，
            // 直接用根节点的话鱼线和钩子之间会露出一截空隙
            float bottomWorldY = bob != null ? bob.position.y : transform.position.y;

            float localBottomY = parent.InverseTransformPoint(new Vector3(px, bottomWorldY, pz)).y;
            float localTopY = parent.InverseTransformPoint(new Vector3(px, _rodTipWorldY, pz)).y;
            float localLength = Mathf.Max(0.05f, localTopY - localBottomY);

            Vector3 scale = _ropeBaseScale;
            scale.y = _ropeBaseScale.y * localLength / _ropeMeshLocalHeight;
            rope.localScale = scale;

            Vector3 pos = _ropeBaseLocalPos;
            pos.y = (localBottomY + localTopY) * 0.5f;
            rope.localPosition = pos;
        }

        private void RefreshShake()
        {
            if (_shakeTimer <= 0f)
            {
                return;
            }

            _shakeTimer = Mathf.Max(0f, _shakeTimer - Time.deltaTime * shakeDamp);

            if (bob != null)
            {
                float offset = Random.Range(-1f, 1f) * shakeStrength * _shakeTimer;
                bob.localPosition = _bobBaseLocalPos + new Vector3(offset, 0f, 0f);
            }

            if (_bobSprite != null)
            {
                _bobSprite.color = Color.Lerp(_bobBaseColor, Color.red, _shakeTimer);
            }
        }
    }
}
