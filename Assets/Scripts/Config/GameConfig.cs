using UnityEngine;

namespace Config
{
    /// <summary>
    /// 全局玩法参数（数值策划表）。
    ///
    /// 速度单位统一为"世界单位 / 秒"，与相机正交尺寸同一量纲，调参所见即所得。
    /// 相机正交半高为 5，即屏幕上下各 5 个世界单位。
    /// </summary>
    [CreateAssetMenu(menuName = "Fish/GameConfig", fileName = "Game Config")]
    public class GameConfig : ScriptableObject
    {
        private const string ResourcePath = "config/Game Config";

        private static GameConfig _cached;

        [Header("鱼钩 · 横向")]
        [Tooltip("鱼钩横向最大速度（世界单位/秒）")]
        public float hookMoveSpeed = 7f;

        [Tooltip("鱼钩左右可移动范围 = 屏幕半宽 * 该比例")]
        public float hookXLimitRatio = 0.86f;

        [Tooltip("满仓加速返回鱼钩回中的速度")]
        public float hookReturnSpeed = 9f;

        [Tooltip("自动测量钩子碰撞半径失败时的兜底值")]
        public float hookFallbackRadius = 0.25f;

        [Header("鱼钩 · 纵向（DOTween 分段）")]
        [Tooltip("抛钩起点高度（屏幕偏上）")]
        public float hookStartY = 3.6f;

        [Tooltip("常规下潜/上浮时鱼钩停在的高度（屏幕中间）")]
        public float hookMiddleY = 0f;

        [Tooltip("触底冲刺的终点高度（接近屏幕底部）")]
        public float hookBottomY = -3.8f;

        [Header("深度分段")]
        [Tooltip("最大下潜深度，等于世界总共需要滚动的距离 + 鱼钩自己走过的距离")]
        public float maxDepth = 26f;

        [Tooltip("还剩这么多深度时背景停住、鱼钩继续往屏幕底部探（≈ 3/5 个背景贴图高度）")]
        public float finalDiveDepth = 6f;

        [Header("速度")]
        [Tooltip("下潜速度（世界单位/秒），同时也是常规段背景滚动的速度")]
        public float descendSpeed = 3.5f;

        [Tooltip("上浮速度")]
        public float ascendSpeed = 3f;

        [Tooltip("满仓后的加速返回速度")]
        public float fastReturnSpeed = 16f;

        [Header("玩家数值")]
        public int maxHp = 100;

        [Tooltip("鱼钩最多能挂几条鱼")]
        public int maxCatch = 6;

        [Tooltip("两次扣血之间的最小间隔，防止一瞬间被秒")]
        public float hurtCooldown = 0.35f;

        [Header("挂鱼")]
        [Tooltip("挂在钩上的鱼之间的纵向间距")]
        public float catchSlotSpacing = 0.5f;

        [Tooltip("挂在钩上的鱼相对原尺寸的缩放")]
        public float catchSlotScale = 0.7f;

        [Header("刷鱼")]
        [Tooltip("起始刷鱼间隔")]
        public float spawnInterval = 0.8f;

        [Tooltip("最深处的刷鱼间隔（越深越密）")]
        public float spawnIntervalMin = 0.45f;

        [Tooltip("生成在屏幕外的最近距离")]
        public float spawnMarginMin = 2f;

        [Tooltip("生成在屏幕外的最远距离（在最近和最远之间随机，避免鱼排成一条线）")]
        public float spawnMarginMax = 5f;

        [Tooltip("完全移出屏幕后，再往外走这么远才回收（避免鱼在边缘反复生成/回收）")]
        public float despawnMargin = 3f;

        [Tooltip("同屏最多存活多少条鱼（不含已抓住的）")]
        public int maxFishAlive = 14;

        [Tooltip("每种鱼的预热对象池数量")]
        public int poolWarmCount = 3;

        [Header("表现")]
        [Tooltip("1 世界单位显示为多少米（仅影响 HUD 文案）")]
        public float depthPerMeter = 1f;

        /// <summary>抛钩段深度：鱼钩从起点走到屏幕中间所走的距离。</summary>
        public float CastDepth => Mathf.Max(0.01f, hookStartY - hookMiddleY);

        /// <summary>触底冲刺的起始深度。</summary>
        public float FinalDiveStartDepth => Mathf.Max(CastDepth, maxDepth - finalDiveDepth);

        /// <summary>
        /// 给定深度，世界需要滚动的距离
        /// </summary>
        public float WorldScrollAt(float depth)
        {
            return Mathf.Max(0f, Mathf.Clamp(depth, CastDepth, FinalDiveStartDepth) - CastDepth);
        }

        /// <summary>背景此刻是否在滚动。用来决定新鱼从屏幕哪一侧进场。</summary>
        public bool IsWorldScrolling(float depth)
        {
            return depth > CastDepth && depth < FinalDiveStartDepth;
        }

        public static GameConfig Get()
        {
            if (_cached != null) return _cached;
            _cached = Resources.Load<GameConfig>(ResourcePath);
            return _cached;
        }
    }
}
