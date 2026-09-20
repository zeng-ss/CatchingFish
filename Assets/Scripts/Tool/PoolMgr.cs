using System.Collections.Generic;
using UnityEngine;

namespace Tool
{
    /// <summary>
    /// 【工具层】对象池。每种鱼一个池，由 `FishSpawner` 懒创建。
    /// 池子空了会自动补一个新实例，所以"同屏鱼很多"时不会出现刷不出鱼的情况。
    /// </summary>
    public static class PoolMgr
    {
        private class Pool
        {
            private readonly Transform _root;
            private readonly string _path;
            private readonly string _poolName;
            private readonly Queue<GameObject> _objs = new();

            public Pool(string poolName, int size, string path)
            {
                _root = new GameObject(poolName).transform;
                _path = path;
                _poolName = poolName;

                // 预热：先造好一批躺在池子里
                for (int i = 0; i < size; i++)
                {
                    CreateOne(pooled: true);
                }
            }

            public void Push(GameObject obj)
            {
                if (obj == null || obj.name != _poolName || _objs.Contains(obj))
                {
                    return;
                }

                obj.SetActive(false);
                obj.transform.localPosition = Vector3.zero;
                obj.transform.localRotation = Quaternion.identity;
                _objs.Enqueue(obj);
            }

            public GameObject Pop()
            {
                if (_objs.Count > 0)
                {
                    GameObject pooled = _objs.Dequeue();
                    pooled.SetActive(true);
                    return pooled;
                }

                // 池子空了：现场补一个直接交付（不同屏鱼数突然变大时不会刷不出鱼）
                return CreateOne(pooled: false);
            }

            private GameObject CreateOne(bool pooled)
            {
                GameObject go = ResourcesMgr.Load<GameObject>(_path, _root);
                if (go == null)
                {
                    return null; // 路径写错时由 ResourcesMgr 输出一次日志，这里不重复刷屏
                }

                go.name = _poolName;
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.SetActive(!pooled);

                if (pooled)
                {
                    _objs.Enqueue(go);
                }

                return go;
            }
        }

        private static readonly Dictionary<string, Pool> Pools = new();

        public static bool HasPool(string name) => Pools.ContainsKey(name);

        public static void CreatePool(string name, int count, string path)
        {
            if (Pools.ContainsKey(name))
            {
                return;
            }

            Pools.Add(name, new Pool(name, count, path));
        }

        public static GameObject Pop(string name)
        {
            return Pools.TryGetValue(name, out Pool pool) ? pool.Pop() : null;
        }

        public static void Push(string name, GameObject obj)
        {
            if (Pools.TryGetValue(name, out Pool pool))
            {
                pool.Push(obj);
            }
        }
    }
}
