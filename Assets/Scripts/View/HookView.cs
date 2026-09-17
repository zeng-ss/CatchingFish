using Config;
using Controller;
using Core;
using UnityEngine;

namespace View
{
    [DisallowMultipleComponent]
    public class HookView : MonoBehaviour
    {
        [Header("受击闪烁")]
        [Tooltip("每秒闪烁多少次（一次=亮+灭）")]
        [SerializeField]
        private float blinkFrequency = 8f;

        [Header("命中判定")]
        [Tooltip("钩头：只有撞到它附近的鱼才算命中。留空自动取钩子根节点上的 Collider")]
        [SerializeField]
        private Collider headCollider;

        [Tooltip("钩头包围盒额外放宽的余量（米），让\"擦到\"也算命中")]
        [SerializeField]
        private float headTolerance = 0.15f;

        private HookController _controller;
        private Renderer[] _renderers;

        private float _blinkTimer;      // > 0 表示还在闪烁（持续到受击冷却结束）
        private float _cooldown;        // 本次闪烁的总时长

        public void Bind(HookController controller) => _controller = controller;

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);

            // 钩头默认取钩子根节点自己的 Collider（就是钩子弯钩那一块）
            if (headCollider == null) headCollider = GetComponent<Collider>();
        }

        private void OnEnable()
        {
            EventMgr.Subscribe(GameEvent.FishHurt, OnFishHurt);
        }

        private void OnDisable()
        {
            EventMgr.Unsubscribe(GameEvent.FishHurt, OnFishHurt);
            SetVisible(true);   // 别把钩子留在"隐"的状态
        }

        /// <summary>
        /// 受到伤害：开始显隐闪烁，直到受击冷却结束。
        /// 时长直接取 GameConfig.hurtCooldown —— 闪烁结束的那一帧正好就是
        /// 控制层可以再次受击的时刻，两边的节奏天然同步。
        /// </summary>
        private void OnFishHurt(object payload)
        {
            _cooldown = Mathf.Max(0.05f, GameConfig.Get().hurtCooldown);
            _blinkTimer = _cooldown;
        }

        private void LateUpdate()
        {
            if (_controller != null)
            {
                Vector3 p = _controller.Position;
                transform.position = new Vector3(p.x, p.y, transform.position.z);
            }

            TickBlink(Time.deltaTime);
        }

        /// <summary>显隐显隐……方波翻转。计时结束强制恢复可见。</summary>
        private void TickBlink(float dt)
        {
            if (_blinkTimer <= 0f) return;

            _blinkTimer -= dt;
            if (_blinkTimer <= 0f)
            {
                SetVisible(true);
                return;
            }

            // 方波：前半周期可见、后半周期不可见
            float period = 1f / Mathf.Max(0.01f, blinkFrequency);
            SetVisible(Mathf.Repeat(_blinkTimer, period) > period * 0.5f);
        }

        private void SetVisible(bool visible)
        {
            if (_renderers == null) return;

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] != null) _renderers[i].enabled = visible;
            }
        }

        // ------------------------------------------------------------------

        private void OnTriggerEnter(Collider other)
        {
            if (_controller == null || other == null) return;

            FishView fish = other.GetComponent<FishView>();
            if (fish == null || fish.Id < 0) return;

            // 物理引擎只认"谁身上有 Collider"，不认"看起来像不像鱼钩"。
            // 钩子上挂着的鱼线/装饰物只要带了 Collider，它们盖到的鱼也会发来 Trigger ——
            // 于是会出现"没看见鱼却抓到了"。这里再用钩头的包围盒复核一次。
            if (!IsNearHead(other)) return;

            _controller.OnHookTouchedFish(fish.Id);
        }

        /// <summary>
        /// 这次碰撞是不是真的发生在钩头附近。
        /// 留空 headCollider 时不做额外过滤，直接交给物理引擎。
        /// </summary>
        private bool IsNearHead(Collider fish)
        {
            if (headCollider == null) return true;

            Bounds head = headCollider.bounds;
            head.Expand(headTolerance * 2f); // Expand 收的是"总尺寸"，乘 2 才是单边余量
            return head.Intersects(fish.bounds);
        }
    }
}
