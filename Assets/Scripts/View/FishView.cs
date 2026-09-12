using Entities;
using Tool;
using UnityEngine;

namespace View
{
    /// <summary>
    /// 单条鱼的表现层：只负责"长什么样、朝哪边、被抓时的反馈"，
    /// 不包含任何玩法规则（扣血、计分都在 Controller / Model）。
    /// </summary>
    [DisallowMultipleComponent]
    public class FishView : MonoBehaviour
    {
        [Header("朝向")]
        [Tooltip("朝右时的本地欧拉角。Quirky 系列模型绕 X 轴 -90° 立起来，所以左右朝向用 Y 轴 ±90° 表达")]
        [SerializeField]
        private Vector3 faceRightEuler = new Vector3(0f, -90f, 0f);

        [Tooltip("如果发现鱼游动方向与身体朝向相反，勾上这个即可整体翻转")]
        [SerializeField]
        private bool invertFacing;

        [Header("被抓反馈")]
        [Tooltip("抓住瞬间的缩放脉冲强度")]
        [SerializeField]
        private float catchPunch = 0.25f;

        [Tooltip("缩放下沉的快慢")]
        [SerializeField]
        private float punchDamp = 8f;

        private Renderer[] _renderers;
        private Fish _entity;
        private Quaternion _faceRight;
        private Quaternion _faceLeft;
        private int _appliedDirection;
        private Vector3 _baseScale = Vector3.one;
        private float _scaleMultiplier = 1f;
        private float _punchTimer;

        /// <summary>绑定的逻辑实体。</summary>
        public Fish Entity => _entity;

        public bool IsBound => _entity != null;

        /// <summary>作者配置的"朝右"欧拉角，供生成器给同类预制体做朝向基准。</summary>
        public Vector3 FaceRightEuler => faceRightEuler;

        /// <summary>世界空间包围盒（碰撞判定用，自动跟随蒙皮动画）。</summary>
        public Bounds WorldBounds => ViewportUtil.ComputeWorldBounds(_renderers, transform.position);

        private void Awake()
        {
            CacheRefs();
        }

        /// <summary>供 Editor 烘焙朝向使用（把场景里美术摆好的旋转写回预制体）。</summary>
        public void SetFaceRightEuler(Vector3 euler)
        {
            faceRightEuler = euler;
        }

        private void CacheRefs()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            _baseScale = transform.localScale;
        }

        /// <summary>
        /// 初始化 / 复用时的重置。
        /// </summary>
        /// <param name="faceRightOverride">外部实测的朝向基准（可选）。</param>
        public void Init(Quaternion? faceRightOverride = null)
        {
            if (_renderers == null || _renderers.Length == 0)
            {
                CacheRefs();
            }

            Quaternion right = faceRightOverride ?? Quaternion.Euler(faceRightEuler);
            if (invertFacing)
            {
                right *= Quaternion.Euler(0f, 180f, 0f);
            }

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
            _scaleMultiplier = Mathf.Max(0.01f, multiplier);
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
            if (_entity != null)
            {
                _entity.View = this;
            }
        }

        public void Unbind()
        {
            if (_entity != null && _entity.View == this)
            {
                _entity.View = null;
            }

            _entity = null;
        }

        /// <summary>按游动方向翻转身体。方向没变时不做任何事（省掉每帧写 Transform）。</summary>
        public void SetDirection(int direction)
        {
            int dir = direction >= 0 ? 1 : -1;
            if (_appliedDirection == dir)
            {
                return;
            }

            _appliedDirection = dir;
            transform.localRotation = dir > 0 ? _faceRight : _faceLeft;
        }

        /// <summary>抓住瞬间的反馈（缩放脉冲）。</summary>
        public void PlayCatchPunch()
        {
            _punchTimer = 1f;
        }

        private void Update()
        {
            if (_punchTimer <= 0f)
            {
                return;
            }

            _punchTimer = Mathf.Max(0f, _punchTimer - Time.deltaTime * punchDamp);
            ApplyScale();
        }
    }
}
