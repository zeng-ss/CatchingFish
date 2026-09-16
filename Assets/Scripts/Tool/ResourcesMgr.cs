using System.Collections.Generic;
using UnityEngine;

namespace Tool
{
    public static class ResourcesMgr
    {
        private static readonly Dictionary<string, Object> Cache = new();

        public static T Load<T>(string path, Transform father = null) where T : Object
        {
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("[ResourcesMgr] 加载路径不能为空！");
                return null;
            }

            // 缓存命中
            if (Cache.TryGetValue(path, out Object cachedAsset))
            {
                return HandleAsset<T>(cachedAsset, father);
            }

            // 首次加载
            T asset = Resources.Load<T>(path);
            if (asset is null)
            {
                Debug.LogError($"[ResourcesMgr] 加载资源失败！路径: {path}，类型: {typeof(T).Name}");
                return null;
            }

            Cache.Add(path, asset); // 原始资源先入缓存
            return HandleAsset<T>(asset, father);
        }

        // 统一处理：GameObject 实例化，其他直接返回
        private static T HandleAsset<T>(Object asset, Transform father) where T : Object
        {
            if (typeof(T) == typeof(GameObject))
            {
                return Object.Instantiate(asset, father) as T;
            }

            return asset as T;
        }

        public static void ClearCache()
        {
            Cache.Clear();
            Resources.UnloadUnusedAssets();
        }
    }
}