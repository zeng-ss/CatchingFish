using Entities;
using Tool;
using UnityEngine;

namespace View
{
    /// <summary>
    /// 单条鱼的表现层：只负责"长什么样、朝哪边、被抓时的反馈"，
    /// </summary>
    [DisallowMultipleComponent]
    public class FishView : MonoBehaviour
    {
        [Header("被抓反馈")][SerializeField] private float catchPunch = 0.25f;
        [Tooltip("缩放下沉的快慢")] [SerializeField] private float punchDamp = 8f;

        private Renderer[] _renderers;
        private Fish _entity;
        private Quaternion _faceRight, _faceLeft;
        private int _appliedDirection;
        private Vector3 _baseScale = Vector3.one;
        private float _scaleMultiplier = 1f;
        private float _punchTimer;

        public Bounds WorldBounds => ViewportUtil.ComputeWorldBounds(_renderers, transform.position);

        private void Awake()
        {
            CacheRefs();
        }

        private void CacheRefs()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _baseScale = transform.localScale;
        }

        /// <summary>
        /// 初始化 / 复用时的重置。
        /// </summary>
        public void Init()
        {
            if (_renderers == null || _renderers.Length == 0) CacheRefs();

            Quaternion right = Quaternion.Euler(0f, 90f, 0f);
            _faceRight = right;
            _faceLeft = right * Quaternion.Euler(0f, 180f, 0f);

            _appliedDirection = 0;
            _punchTimer = 0f;
            _scaleMultiplier = 1f;
            ApplyScale();
        }

        /// <summary>设置缩放倍率（配置表里的 FishData.scale）。</summary>
        public void SetScale(float multiplier)
        {
            _scaleMultiplier = multiplier;
            ApplyScale();
        }

        private void ApplyScale()
        {
            float punch = _punchTimer > 0f ? 1f + catchPunch * _punchTimer : 1f;
            transform.localScale = _baseScale * (_scaleMultiplier * punch);
        }

        /// <summary>绑定逻辑实体。</summary>
        public void Bind(Fish fish)
        {
            _entity = fish;
            _entity.View = this;
        }

        public void Unbind()
        {
            if (_entity != null && _entity.View == this) _entity.View = null;
            _entity = null;
        }

        /// <summary>按游动方向翻转身体。方向没变时不做任何事（省掉每帧写 Transform）。</summary>
        public void SetDirection(int direction)
        {
            int dir = direction >= 0 ? 1 : -1;
            if (_appliedDirection == dir) return;

            _appliedDirection = dir;
            transform.localRotation = dir > 0 ? _faceRight : _faceLeft;
        }

        /// <summary>抓住瞬间的反馈。</summary>
        public void PlayCatchPunch()
        {
            _punchTimer = 1f;
        }

        private void Update()
        {
            if (_punchTimer <= 0f) return;
            _punchTimer = Mathf.Max(0f, _punchTimer - Time.deltaTime * punchDamp);
            ApplyScale();
        }
    }
}