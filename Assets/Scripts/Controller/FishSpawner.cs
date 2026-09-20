using Config;
using Tool;
using UnityEngine;

namespace Controller
{
    /// <summary>
    /// 【控制层】鱼的工厂：对象池的薄封装。
    /// 只管从池子里取 / 还 GameObject，不接触任何 View 类型，也不管鱼怎么动。
    /// </summary>
    public class FishSpawner
    {
        private readonly Transform _root;
        private readonly GameConfig _cfg;

        public FishSpawner(Transform fishRoot)
        {
            _root = fishRoot;
            _cfg = GameConfig.Get();
            Config = FishConfig.Get();
        }

        public FishConfig Config { get; }

        /// <summary>从池子里取一个鱼对象、摆到指定世界坐标并激活。池子取不到时返回 false。</summary>
        public bool Rent(string prefabName, Vector3 worldPosition)
        {
            if (string.IsNullOrEmpty(prefabName)) return false;
            if (!PoolMgr.HasPool(prefabName))
            {
                PoolMgr.CreatePool(prefabName, _cfg.poolWarmCount, FishConfig.GetPrefabPath(prefabName));
            }

            GameObject go = PoolMgr.Pop(prefabName);
            if (go == null) return false;
            go.transform.SetParent(_root, false);
            // 出生位置写一次就够：之后的纵向位移由父物体（BG 滚动）带着走
            go.transform.position = worldPosition;
            return true;
        }

        /// <summary>把鱼对象还回池子。</summary>
        public void Recycle(GameObject go, string prefabName)
        {
            if (go == null) return;
            if (string.IsNullOrEmpty(prefabName))
            {
                go.SetActive(false);
                return;
            }

            PoolMgr.Push(prefabName, go);
        }
    }
}