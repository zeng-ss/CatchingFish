using Config;
using Controller;
using Core;
using UnityEngine;

namespace View
{
    /// <summary>
    /// 【表现层】鱼钩。
    ///
    /// View → Controller：控制器算出鱼钩该在哪（`HookController.Position`），
    /// 这里每帧把它写进 Transform；碰撞则由 Unity 的 Trigger 回调转交给控制器判定。
    /// </summary>
    [DisallowMultipleComponent]
    public class HookView : MonoBehaviour
    {
        [Header("受击闪烁")] [Tooltip("每秒闪烁多少次（一次=亮+灭）")] [SerializeField]
        private float blinkFrequency = 8f;

        [Header("命中判定")] [Tooltip("钩头：只有撞到它附近的鱼才算命中。留空自动取根节点上的 Collider")]
        [SerializeField]
        private Collider headCollider;

        [Tooltip("钩头包围盒额外放宽的余量（米），让擦到也算命中")] [SerializeField]
        private float headTolerance = 0.15f;

        private HookController _controller;
        private Renderer[] _renderers;
        private float _blinkTimer; // > 0 表示还在闪烁（持续到受击冷却结束）

        public void Bind(HookController controller) => _controller = controller;

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(true);
            if (headCollider == null) headCollider = GetComponent<Collider>();
        }

        private void OnEnable() => EventMgr.Subscribe(GameEvent.FishHurt, OnFishHurt);

        private void OnDisable()
        {
            EventMgr.Unsubscribe(GameEvent.FishHurt, OnFishHurt);
            SetVisible(true); // 别把钩子留在"隐"的状态
        }

        private void LateUpdate()
        {
            if (_controller == null) return;

            Vector3 p = _controller.Position;
            transform.position = new Vector3(p.x, p.y, transform.position.z);

            TickBlink(Time.deltaTime);
        }

        /// <summary>
        /// 闪烁时长直接取 `hurtCooldown`：闪完的那一帧正好是控制层可以再次受击的时刻，
        /// 两边节奏天然同步，不需要额外对表。
        /// </summary>
        private void OnFishHurt(object payload)
        {
            _blinkTimer = Mathf.Max(0.05f, GameConfig.Get().hurtCooldown);
        }

        /// <summary>方波翻转：前半周期可见、后半周期不可见；计时结束强制恢复可见。</summary>
        private void TickBlink(float dt)
        {
            if (_blinkTimer <= 0f) return;

            _blinkTimer -= dt;
            if (_blinkTimer <= 0f)
            {
                SetVisible(true);
                return;
            }

            float period = 1f / Mathf.Max(0.01f, blinkFrequency);
            SetVisible(Mathf.Repeat(_blinkTimer, period) > period * 0.5f);
        }

        private void SetVisible(bool visible)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                _renderers[i].enabled = visible;
            }
        }

        // ------------------------------------------------------------------
        // 碰撞：物理引擎只负责"通知"，判定规则留给控制层
        // ------------------------------------------------------------------

        private void OnTriggerEnter(Collider other)
        {
            if (_controller == null) return;

            FishView fish = other.GetComponent<FishView>();
            if (fish == null || fish.Id < 0) return;

            // 钩子上还挂着鱼线这类细长装饰物，它们身上的碰撞体同样会参与触发；
            // 不过一遍钩头复核，就会出现"没看见鱼却抓到了"
            if (!IsNearHead(other)) return;

            _controller.OnHookTouchedFish(fish.Id);
        }

        private bool IsNearHead(Collider fish)
        {
            Bounds head = headCollider.bounds;
            head.Expand(headTolerance * 2f); // Expand 收的是"总尺寸"，乘 2 才是单边余量

            return head.Intersects(fish.bounds);
        }
    }
}
