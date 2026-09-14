using System.Collections.Generic;
using UnityEngine;

namespace Config
{
    [CreateAssetMenu(menuName = "Fish/FishConfig", fileName = "Fish Config")]
    public class FishConfig : ScriptableObject
    {
        private const string ResourcePath = "config/Fish Config";
        private const string PrefabFolder = "Prefab/Fish/";

        private static FishConfig _cached;

        [SerializeField] private List<FishData> fishData = new();

        // ---- 运行时索引 ----
        private readonly Dictionary<FishType, FishData> _fishDataDict = new();
        private readonly List<FishData> _rolled = new();
        private readonly List<int> _weights = new();
        private bool _indexed;

        public int Count => fishData?.Count ?? 0;

        public static string GetPrefabPath(string prefabName)
        {
            return PrefabFolder + prefabName;
        }

        // 取配置
        public static FishConfig Get()
        {
            if (_cached == null)
            {
                _cached = Resources.Load<FishConfig>(ResourcePath);
                if (_cached != null && _cached.Count > 0)
                {
                    return _cached;
                }

                Debug.LogWarning($"[FishConfig] Resources/{ResourcePath} 缺失或为空，改用代码内默认鱼表。" +
                                 "可在 Unity 里执行菜单 工具/捕鱼/1. 生成配置资源 落成资产，方便在 Inspector 里调参。");

                _cached = CreateInstance<FishConfig>();
                _cached.SetData(DefaultGameData.CreateFishTable());
            }

            return _cached;
        }

        public FishData Get(FishType type)
        {
            EnsureIndex();
            return _fishDataDict.GetValueOrDefault(type);
        }

        public FishData GetByPrefabName(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName))
            {
                return null;
            }

            EnsureIndex();
            foreach (var data in fishData)
            {
                if (data != null && data.prefabName == prefabName)
                {
                    return data;
                }
            }

            return null;
        }

        /// <summary>
        /// 按权重挑一条鱼。
        /// </summary>
        public FishData PickByDepth(float roll)
        {
            EnsureIndex();
            if (fishData == null || fishData.Count == 0)
            {
                return null;
            }

            _rolled.Clear();
            _weights.Clear();

            foreach (var data in fishData)
            {
                if (data is not { spawnWeight: > 0 } || string.IsNullOrEmpty(data.prefabName))
                {
                    continue;
                }

                _rolled.Add(data);
                _weights.Add(data.spawnWeight);
            }

            if (_rolled.Count == 0)
            {
                // 深度区间没配好时的兜底：忽略深度限制
                foreach (var data in fishData)
                {
                    if (data is not { spawnWeight: > 0 } || string.IsNullOrEmpty(data.prefabName))
                    {
                        continue;
                    }

                    _rolled.Add(data);
                    _weights.Add(data.spawnWeight);
                }
            }

            int index = Tool.MathUtil.PickWeighted(_weights, roll);
            return index < 0 ? null : _rolled[index];
        }

        /// <summary>供 Editor 生成器写表使用。</summary>
        public void SetData(List<FishData> data)
        {
            fishData = data ?? new List<FishData>();
            _indexed = false;
            EnsureIndex();
        }

        private void EnsureIndex()
        {
            if (_indexed)
            {
                return;
            }

            fishData ??= new List<FishData>();

            _fishDataDict.Clear();
            foreach (var data in fishData)
            {
                if (data == null)
                {
                    continue;
                }

                _fishDataDict[data.type] = data;
            }

            _indexed = true;
        }

        private void OnValidate()
        {
            _indexed = false;
        }
    }

    /// <summary>鱼种分类，决定它在玩法里的角色（危险 / 高价值 / 快速）。</summary>
    public enum FishType
    {
        刺猬鱼,
        带鱼,
        小鱼,
        鱼虾,
        正常鱼,
        快速鱼
    }

    /// <summary>单条鱼的数值配置。</summary>
    [System.Serializable]
    public class FishData
    {
        [Tooltip("鱼种分类")] public FishType type;

        [Tooltip("显示名（结算列表用）")] public string displayName;

        [Tooltip("Resources/Prefab/Fish 下的预制体名")]
        public string prefabName;

        [Tooltip("抓到后的得分")] public int score;

        [Tooltip("下潜时撞到扣的血量")] public int damage;

        [Tooltip("左右游动速度（世界单位/秒）")] public float moveSpeed;

        [Tooltip("生成时的缩放倍率（模型本身大小差异大时用它归一化）")] public float scale = 1f;

        [Tooltip("仅用于 UI 展示的颜色")] public Color color = Color.white;

        [Tooltip("生成权重，越大越常见")] public int spawnWeight;
    }
}