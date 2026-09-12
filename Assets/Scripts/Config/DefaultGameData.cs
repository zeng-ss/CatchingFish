using System.Collections.Generic;
using UnityEngine;

namespace Config
{
    /// <summary>
    /// 默认数值表的唯一数据源。
    ///
    /// 放在运行时代码里的原因：即使 Resources/config 下的资产被删掉或没生成，
    /// 游戏依然能完整跑起来（见 <see cref="FishConfig.LoadOrDefault"/>）。
    /// Editor 的"生成配置资源"菜单也用同一份数据，避免两处硬编码走偏。
    /// </summary>
    public static class DefaultGameData
    {
        /// <summary>
        /// 默认鱼表。
        /// 设计思路：越深越危险越值钱——浅水放低伤小鱼练手，
        /// 深水放高伤高分的刺猬鱼/鳄鱼制造"要不要再往下一点"的取舍。
        /// </summary>
        public static List<FishData> CreateFishTable()
        {
            return new List<FishData>
            {
                Make(FishType.小鱼, "青蛙", "Frog", 10, 6, 2.4f, 26, 0f, 26f, new Color(0.55f, 0.85f, 0.45f)),
                Make(FishType.正常鱼, "鲤鱼", "Carp", 20, 10, 1.8f, 24, 0f, 26f, new Color(0.98f, 0.72f, 0.32f)),
                Make(FishType.快速鱼, "翠鸟", "Kingfisher", 30, 8, 4.5f, 12, 4f, 26f, new Color(0.30f, 0.65f, 0.95f)),
                Make(FishType.带鱼, "河狸", "Beaver", 40, 20, 1.4f, 10, 6f, 26f, new Color(0.65f, 0.45f, 0.30f)),
                Make(FishType.正常鱼, "金龙鱼", "Arowana", 45, 14, 2.6f, 12, 8f, 26f, new Color(0.95f, 0.55f, 0.25f)),
                Make(FishType.正常鱼, "海牛", "Manatee", 35, 26, 0.9f, 8, 8f, 26f, new Color(0.60f, 0.65f, 0.75f)),
                Make(FishType.带鱼, "天鹅", "Swan", 55, 22, 1.2f, 8, 10f, 26f, new Color(0.95f, 0.95f, 0.95f)),
                Make(FishType.刺猬鱼, "鳄龟", "SnappingTurtle", 65, 32, 1.0f, 7, 12f, 26f, new Color(0.45f, 0.55f, 0.35f)),
                Make(FishType.刺猬鱼, "鳄鱼", "Crocodile", 90, 45, 1.6f, 5, 16f, 26f, new Color(0.35f, 0.60f, 0.35f)),
            };
        }

        private static FishData Make(
            FishType type,
            string displayName,
            string prefabName,
            int score,
            int damage,
            float moveSpeed,
            int weight,
            float minDepth,
            float maxDepth,
            Color color)
        {
            return new FishData
            {
                type = type,
                displayName = displayName,
                prefabName = prefabName,
                score = score,
                damage = damage,
                moveSpeed = moveSpeed,
                radius = 0f,
                scale = 1f,
                color = color,
                minDepth = minDepth,
                maxDepth = maxDepth,
                spawnWeight = weight,
            };
        }
    }
}
