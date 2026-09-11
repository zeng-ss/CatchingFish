using UnityEngine;

namespace Config
{
    [CreateAssetMenu(menuName = "Fish/FishConfig")]
    public class FishConfig : ScriptableObject
    {
        public string id;
        public string displayName;
        public int score;          // 分值
        public int damage;         // 下潜时碰到扣血
        public float moveSpeed;    // 左右游动速度
        public float size;
        public Color color;
        public float minDepth;
        public float maxDepth;
        public int spawnWeight;    // 生成权重
    }
}