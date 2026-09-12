using System.Collections.Generic;
using Config;
using UnityEditor;
using UnityEngine;

namespace GameEditor
{
    /// <summary>
    /// 配置资源生成器。
    /// 数值全部写在代码里，一键生成 Resources/config 下的两个 ScriptableObject，
    /// 好处是：数值可版本管理、可 code review，美术/策划改表也不会把资源改坏。
    /// </summary>
    public static class FishAssetGenerator
    {
        private const string ResourcesFolder = "Assets/Resources";
        private const string ConfigFolder = "Assets/Resources/config";
        private const string GameConfigPath = ConfigFolder + "/Game Config.asset";
        private const string FishConfigPath = ConfigFolder + "/Fish Config.asset";

        [MenuItem("工具/捕鱼/1. 生成配置资源", false, 1)]
        public static void GenerateAll()
        {
            EnsureFolder();

            GameConfig gameConfig = CreateOrLoadGameConfig();

            FishConfig fishConfig = AssetDatabase.LoadAssetAtPath<FishConfig>(FishConfigPath);
            if (fishConfig == null)
            {
                fishConfig = ScriptableObject.CreateInstance<FishConfig>();
                AssetDatabase.CreateAsset(fishConfig, FishConfigPath);
                fishConfig.SetData(BuildDefaultTable());
                EditorUtility.SetDirty(fishConfig);
                Debug.Log($"[捕鱼] 新建鱼配置表并写入 {fishConfig.Count} 条鱼。");
            }
            else if (fishConfig.Count == 0)
            {
                fishConfig.SetData(BuildDefaultTable());
                EditorUtility.SetDirty(fishConfig);
                Debug.Log($"[捕鱼] 鱼配置表为空，已写入 {fishConfig.Count} 条默认鱼。");
            }
            else
            {
                Debug.Log($"[捕鱼] 鱼配置表已有 {fishConfig.Count} 条鱼，保持不变。" +
                          "需要恢复默认数值请用 工具/捕鱼/1b. 重建鱼配置表。");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[捕鱼] 配置就绪：GameConfig={gameConfig.name}，最大深度 {gameConfig.maxDepth}，血量 {gameConfig.maxHp}，渔获上限 {gameConfig.maxCatch}。");
        }

        [MenuItem("工具/捕鱼/1b. 重建鱼配置表（覆盖）", false, 2)]
        public static void RebuildFishTable()
        {
            EnsureFolder();

            FishConfig fishConfig = AssetDatabase.LoadAssetAtPath<FishConfig>(FishConfigPath);
            if (fishConfig == null)
            {
                fishConfig = ScriptableObject.CreateInstance<FishConfig>();
                AssetDatabase.CreateAsset(fishConfig, FishConfigPath);
            }

            fishConfig.SetData(BuildDefaultTable());
            EditorUtility.SetDirty(fishConfig);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[捕鱼] 鱼配置表已重建，共 {fishConfig.Count} 条。");
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(ResourcesFolder))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            if (!AssetDatabase.IsValidFolder(ConfigFolder))
            {
                AssetDatabase.CreateFolder(ResourcesFolder, "config");
            }
        }

        private static GameConfig CreateOrLoadGameConfig()
        {
            GameConfig cfg = AssetDatabase.LoadAssetAtPath<GameConfig>(GameConfigPath);
            if (cfg == null)
            {
                // CreateInstance 会执行字段初始值，因此默认数值直接来自 GameConfig.cs
                cfg = ScriptableObject.CreateInstance<GameConfig>();
                AssetDatabase.CreateAsset(cfg, GameConfigPath);
                Debug.Log("[捕鱼] 新建 Game Config.asset，使用 GameConfig.cs 里的默认数值。");
            }

            EditorUtility.SetDirty(cfg);
            return cfg;
        }

        /// <summary>
        /// 默认数值表来自运行时的 <see cref="DefaultGameData"/>，
        /// 保证"代码兜底"和"生成资产"两条路用的是同一份数据。
        /// </summary>
        private static List<FishData> BuildDefaultTable()
        {
            return DefaultGameData.CreateFishTable();
        }
    }
}
