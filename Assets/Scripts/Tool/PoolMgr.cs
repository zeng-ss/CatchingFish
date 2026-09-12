using System.Collections.Generic;
using UnityEngine;

namespace Tool
{
    public static class PoolMgr
    {
        private class Pool
        {
            private readonly Transform root;
            private readonly string _path;
            private readonly string _poolName;
            private readonly Queue<GameObject> _objs = new();

            public Pool(string poolName, int size, string path)
            {
                root = new GameObject(poolName).transform;
                _path = path;
                _poolName = poolName;
                Create(size, path);
            }

            private void Create(int count, string path)
            {
                for (int i = 0; i < count; i++)
                {
                    var go = ResourcesMgr.Load<GameObject>(path, root);
                    if (go == null)
                    {
                        // 资源路径写错时不继续刷屏报错，交由 ResourcesMgr 输出一次日志
                        return;
                    }

                    go.name = _poolName;
                    go.SetActive(false);
                    go.transform.localPosition = Vector3.zero;
                    go.transform.localRotation = Quaternion.identity;
                    _objs.Enqueue(go);
                }
            }

            public void Push(GameObject obj)
            {
                if (obj == null)
                {
                    Debug.LogError("[PoolMgr] 入队物体为空");
                    return;
                }

                if (obj.name != _poolName)
                {
                    Debug.LogError("[PoolMgr] 入错了队");
                    return;
                }

                if (_objs.Contains(obj))
                {
                    Debug.LogWarning($"[PoolMgr] 对象重复入池: {obj.name}");
                    return;
                }

                obj.SetActive(false);
                obj.transform.localPosition = Vector3.zero;
                obj.transform.localRotation = Quaternion.identity;
                _objs.Enqueue(obj);
            }

            public GameObject Pop()
            {
                GameObject go;
                if (_objs.Count > 0)
                {
                    go = _objs.Dequeue();
                    go.SetActive(true);
                    return go;
                }

                Debug.Log("[PoolMgr] 池子空了，动态补一个");
                go = ResourcesMgr.Load<GameObject>(_path, root);
                if (go == null)
                {
                    return null;
                }

                go.name = _poolName;
                go.SetActive(true);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                return go;
            }

            public void ClearPool()
            {
                foreach (var obj in _objs)
                {
                    Object.Destroy(obj);
                }

                _objs.Clear();
            }
        }

        private static readonly Dictionary<string, Pool> PoolsDict = new();

        public static bool HasPool(string name)
        {
            return PoolsDict.ContainsKey(name);
        }

        public static void CreatePool(string name, int count, string path)
        {
            if (PoolsDict.ContainsKey(name))
            {
                Debug.LogWarning($"[PoolMgr] 池子已存在，跳过重复创建：{name}");
                return;
            }

            Pool pool = new Pool(name, count, path);
            PoolsDict.Add(name, pool);
        }

        public static void Push(string name, GameObject obj)
        {
            if (PoolsDict.TryGetValue(name, out var pool))
            {
                pool.Push(obj);
                return;
            }

            Debug.LogError("[PoolMgr] 池子不存在！");
        }

        public static GameObject Pop(string name)
        {
            if (PoolsDict.TryGetValue(name, out var pool))
            {
                return pool.Pop();
            }

            Debug.LogError("[PoolMgr] 池子不存在！");
            return null;
        }

        public static void Clear()
        {
            foreach (var pool in PoolsDict.Values)
            {
                pool.ClearPool();
            }

            PoolsDict.Clear();
        }
    }
}