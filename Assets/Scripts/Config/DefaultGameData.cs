using System.Collections.Generic;
using UnityEngine;

namespace Config
{
    public static class DefaultGameData
    {
        public static List<FishData> CreateFishTable()
        {
            return new List<FishData>
            {
                Make(FishType.小鱼, "青蛙", "Frog", 10, 6, 2.4f, 26, new Color(0.55f, 0.85f, 0.45f)),
                Make(FishType.正常鱼, "鲤鱼", "Carp", 20, 10, 1.8f, 24, new Color(0.98f, 0.72f, 0.32f)),
                Make(FishType.快速鱼, "翠鸟", "Kingfisher", 30, 8, 4.5f, 12, new Color(0.30f, 0.65f, 0.95f)),
                Make(FishType.带鱼, "河狸", "Beaver", 40, 20, 1.4f, 10, new Color(0.65f, 0.45f, 0.30f)),
                Make(FishType.正常鱼, "金龙鱼", "Arowana", 45, 14, 2.6f, 12, new Color(0.95f, 0.55f, 0.25f)),
                Make(FishType.正常鱼, "海牛", "Manatee", 35, 26, 0.9f, 8, new Color(0.60f, 0.65f, 0.75f)),
                Make(FishType.带鱼, "天鹅", "Swan", 55, 22, 1.2f, 8, new Color(0.95f, 0.95f, 0.95f)),
                Make(FishType.刺猬鱼, "鳄龟", "SnappingTurtle", 65, 32, 1.0f, 7, new Color(0.45f, 0.55f, 0.35f)),
                Make(FishType.刺猬鱼, "鳄鱼", "Crocodile", 90, 45, 1.6f, 5, new Color(0.35f, 0.60f, 0.35f)),
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
                scale = 1f,
                color = color,
                spawnWeight = weight,
            };
        }
    }
}