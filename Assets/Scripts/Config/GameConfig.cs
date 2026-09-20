using UnityEngine;

namespace Config
{
    [CreateAssetMenu(menuName = "Fish/GameConfig", fileName = "Game Config")]
    public class GameConfig : ScriptableObject
    {
        private const string ResourcePath = "config/Game Config";

        private static GameConfig _cached;

        [Header("鱼钩 · 横向")] [Tooltip("鱼钩横向最大速度（世界单位/秒）")]
        public float hookMoveSpeed = 7f;

        [Tooltip("鱼钩左右可移动范围 = 屏幕半宽 * 该比例")] public float hookXLimitRatio = 0.86f;

        [Tooltip("满仓加速返回鱼钩回中的速度")] public float hookReturnSpeed = 9f;

        [Header("鱼钩 · 纵向（DOTween 分段）")] [Tooltip("抛钩起点高度")]
        public float hookStartY = 3.6f;

        [Tooltip("常规下潜/上浮时鱼钩停在的高度")] public float hookMiddleY;

        [Tooltip("触底冲刺的终点高度")] public float hookBottomY = -3.8f;

        [Header("深度分段")] [Tooltip("最大下潜深度")]
        public float maxDepth = 26f;

        [Tooltip("还剩这么多深度时背景停住 鱼钩继续往屏幕底部探")]
        public float finalDiveDepth = 6f;

        [Header("速度")] [Tooltip("下潜速度同时也是常规段背景滚动的速度")]
        public float descendSpeed = 3.5f;

        [Tooltip("上浮速度")] public float ascendSpeed = 3f;

        [Tooltip("满仓后的加速返回速度")] public float fastReturnSpeed = 16f;

        [Header("玩家数值")] public int maxHp = 100;

        [Tooltip("鱼钩最多能挂几条鱼")] public int maxCatch = 6;

        [Tooltip("两次扣血之间的最小间隔")] public float hurtCooldown = 0.35f;

        [Tooltip("挂在钩上的鱼的缩放")] public float catchSlotScale = 0.7f;

        [Header("刷鱼")] [Tooltip("起始刷鱼间隔")] public float spawnInterval = 0.55f;

        [Tooltip("最深处的刷鱼间隔")] public float spawnIntervalMin = 0.32f;

        [Tooltip("生成在屏幕外的最近距离")] public float spawnMarginMin = 2f;

        [Tooltip("生成在屏幕外的最远距离")] public float spawnMarginMax = 4f;

        [Tooltip("完全移出屏幕后，再往外走这么远才回收")] public float despawnMargin = 1f;

        [Tooltip("同屏最多有多少条『自由游动』的鱼（挂在钩上的不占名额）")] public int maxFishAlive = 26;

        [Tooltip("背景停住但已经下潜时，也从左右两侧补鱼（触底冲刺 / 收线起钩这两段）")]
        public bool spawnWhenStill = true;

        [Tooltip("在屏幕外待超过这么久还没进场就回收（防止横向游出范围的鱼永远占着名额）")]
        public float maxOutsideLife = 6f;

        [Tooltip("判断『同一条泳道』的额外纵向余量：两条鱼的半高之和再加这么多就算会撞上")] public float laneGap = 0.18f;

        [Tooltip("同泳道前后保持的最小横向间距")] public float minFishGap = 0.25f;

        [Tooltip("每种鱼的预热对象池数量")] public int poolWarmCount = 5;

        [Tooltip("1 世界单位显示为多少米")] public float depthPerMeter = 1f;

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

        // ==================================================================
        // 开场 / 收场镜头
        // ==================================================================

        [Header("开场 / 收场镜头")]
        [Tooltip("抛钩结束后，镜头下移到游玩高度的时长")]
        public float cameraMoveDuration = 0.6f;

        [Tooltip("结算时镜头加速回到开始画面的时长")]
        public float cameraBackDuration = 0.5f;

        public static GameConfig Get()
        {
            if (_cached != null) return _cached;
            _cached = Resources.Load<GameConfig>(ResourcePath);
            return _cached;
        }
    }
}