using Config;
using Entities;
using Tool;
using UnityEngine;
using View;

namespace Controller
{
    public class FishSpawner
    {
        private readonly Transform _root;
        private readonly GameConfig _cfg;

        private int _nextId;

        public FishSpawner(Transform fishRoot)
        {
            _root = fishRoot;
            _cfg = GameConfig.Get();
            Config = FishConfig.Get();
        }

        public FishConfig Config { get; }

        /// <summary>从池子里取一条鱼放到指定世界坐标。池子取不到时返回 null</summary>
        public Fish Spawn(FishData data, Vector3 worldPosition, int direction)
        {
            if (data == null) return null;
            EnsurePool(data.prefabName);
            GameObject go = PoolMgr.Pop(data.prefabName);
            if (go == null) return null;

            Transform t = go.transform;
            t.SetParent(_root, false);
            t.position = worldPosition;

            FishView view = go.GetComponent<FishView>();
            view.Init();
            view.SetScale(data.scale <= 0f ? 1f : data.scale);

            Fish fish = new Fish();
            fish.ResetForSpawn(data, direction);

            view.Bind(fish);
            view.SetDirection(fish.Direction);

            return fish;
        }

        /// <summary>把鱼还回对象池。</summary>
        public void Recycle(Fish fish)
        {
            if (fish?.Data == null || fish.View == null) return;

            string poolName = fish.Data.prefabName;
            fish.IsCaught = false;
            GameObject go = fish.View.gameObject;
            fish.View.Unbind();

            EnsurePool(poolName);
            PoolMgr.Push(poolName, go);
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