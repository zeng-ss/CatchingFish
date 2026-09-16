using Config;
using Tool;
using UnityEngine;

namespace Controller
{
    /// <summary>
    /// 鱼的工厂（控制层）：对象池的薄封装。
    /// **不接触任何 View 类型**——它只管从池子里取/还 GameObject。
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

        /// <summary>从池子里取一个鱼对象并激活。池子取不到时返回 false。</summary>
        public bool Rent(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName)) return false;

            EnsurePool(prefabName);
            GameObject go = PoolMgr.Pop(prefabName);
            if (go == null) return false;

            go.transform.SetParent(_root, false);
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

            EnsurePool(prefabName);
            PoolMgr.Push(prefabName, go);
        }

        // ------------------------------------------------------------------

        private void EnsurePool(string prefabName)
        {
            if (PoolMgr.HasPool(prefabName)) return;

            int warm = _cfg != null ? Mathf.Max(1, _cfg.poolWarmCount) : 3;
            PoolMgr.CreatePool(prefabName, warm, FishConfig.GetPrefabPath(prefabName));
        }
    }
}
