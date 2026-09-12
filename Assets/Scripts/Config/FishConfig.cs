using System.Collections.Generic;
using UnityEngine;

namespace Config
{
    /// <summary>
    /// 鱼的配置表（ScriptableObject 数据驱动）。
    /// 通过 Resources 加载，运行时只读，配合权重随机实现"深海出大鱼"的难度曲线。
    /// </summary>
    [CreateAssetMenu(menuName = "Fish/FishConfig", fileName = "Fish Config")]
    public class FishConfig : ScriptableObject
    {
        public const string ResourcePath = "config/Fish Config";
        public const string PrefabFolder = "Prefab/Fish/";

        private static FishConfig _cached;

        [SerializeField] private List<FishData> fishData = new();

        // ---- 运行时索引（不参与序列化，Editor 改表后由 OnValidate 失效）----
        private readonly Dictionary<FishType, FishData> _typeIndex = new();
        private readonly List<FishData> _rolled = new();
        private readonly List<int> _weights = new();
        private bool _indexed;

        /// <summary>全部鱼种配置。</summary>
        public IReadOnlyList<FishData> All
        {
            get
            {
                EnsureIndex();
                return fishData;
            }
        }

        public int Count => fishData != null ? fishData.Count : 0;

        /// <summary>按 Resources 相对路径拿到某条鱼预制体的加载路径。</summary>
        public static string GetPrefabPath(string prefabName)
        {
            return PrefabFolder + prefabName;
        }

        /// <summary>取配置（带缓存），控制器直接调用即可，不需要外部注入。</summary>
        public static FishConfig Get()
        {
            if (_cached == null)
            {
                _cached = LoadOrDefault();
            }

            return _cached;
        }

        /// <summary>
        /// 从 Resources 读配置。
        /// 读不到或表为空时退回代码内默认鱼表（<see cref="DefaultGameData"/>），
        /// 保证工程 clone 下来不生成任何资产也能直接跑。
        /// </summary>
        public static FishConfig LoadOrDefault()
        {
            FishConfig cfg = Resources.Load<FishConfig>(ResourcePath);
            if (cfg != null && cfg.Count > 0)
            {
                return cfg;
            }

            Debug.LogWarning($"[FishConfig] Resources/{ResourcePath} 缺失或为空，改用代码内默认鱼表。" +
                             "可在 Unity 里执行菜单 工具/捕鱼/1. 生成配置资源 落成资产，方便在 Inspector 里调参。");

            cfg = CreateInstance<FishConfig>();
            cfg.SetData(DefaultGameData.CreateFishTable());
            return cfg;
        }

        public FishData Get(FishType type)
        {
            EnsureIndex();
            return _typeIndex.TryGetValue(type, out FishData data) ? data : null;
        }

        public FishData GetByPrefabName(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName))
            {
                return null;
            }

            EnsureIndex();
            for (int i = 0; i < fishData.Count; i++)
            {
                if (fishData[i] != null && fishData[i].prefabName == prefabName)
                {
                    return fishData[i];
                }
            }

            return null;
        }

        /// <summary>
        /// 按"当前深度 + 权重"挑一条鱼。
        /// 只考虑 minDepth &lt;= depth &lt;= maxDepth 的鱼；一条都没有时退回全部权重。
        /// </summary>
        /// <param name="depth">当前下潜深度（世界单位）。</param>
        /// <param name="roll">[0,1) 的随机数，由外部注入以便复现。</param>
        public FishData PickByDepth(float depth, float roll)
        {
            EnsureIndex();
            if (fishData == null || fishData.Count == 0)
            {
                return null;
            }

            _rolled.Clear();
            _weights.Clear();

            for (int i = 0; i < fishData.Count; i++)
            {
                FishData d = fishData[i];
                if (d == null || d.spawnWeight <= 0 || string.IsNullOrEmpty(d.prefabName))
                {
                    continue;
                }

                if (depth < d.minDepth || depth > d.maxDepth)
                {
                    continue;
                }

                _rolled.Add(d);
                _weights.Add(d.spawnWeight);
            }

            if (_rolled.Count == 0)
            {
                // 深度区间没配好时的兜底：忽略深度限制
                for (int i = 0; i < fishData.Count; i++)
                {
                    FishData d = fishData[i];
                    if (d == null || d.spawnWeight <= 0 || string.IsNullOrEmpty(d.prefabName))
                    {
                        continue;
                    }

                    _rolled.Add(d);
                    _weights.Add(d.spawnWeight);
                }
            }

            int index = Tool.MathUtil.PickWeighted(_weights, roll);
            return index < 0 ? null : _rolled[index];
        }

        /// <summary>Editor 生成器改完表后调用，重建索引。</summary>
        public void Rebuild()
        {
            _indexed = false;
            EnsureIndex();
        }

        /// <summary>供 Editor 生成器写表使用。</summary>
        public void SetData(List<FishData> data)
        {
            fishData = data ?? new List<FishData>();
            Rebuild();
        }

        private void EnsureIndex()
        {
            if (_indexed)
            {
                return;
            }

            if (fishData == null)
            {
                fishData = new List<FishData>();
            }

            _typeIndex.Clear();
            for (int i = 0; i < fishData.Count; i++)
            {
                FishData d = fishData[i];
                if (d == null)
                {
                    continue;
                }

                _typeIndex[d.type] = d;
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

        [Tooltip("碰撞半径；<= 0 时用渲染包围盒自动计算（推荐留 0）")]
        public float radius;

        [Tooltip("生成时的缩放倍率（模型本身大小差异大时用它归一化）")] public float scale = 1f;

        [Tooltip("仅用于 UI 展示的颜色")] public Color color = Color.white;

        [Tooltip("出现的最小深度（世界单位）")] public float minDepth;

        [Tooltip("出现的最大深度（世界单位）")] public float maxDepth;

        [Tooltip("生成权重，越大越常见")] public int spawnWeight;
    }
}