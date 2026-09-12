using System.Collections.Generic;
using UnityEngine;

namespace Tool
{
    public static class ResourcesMgr
    {
        private static readonly Dictionary<string, Object> Cache = new();

        public static T Load<T>(string path, Transform father = null) where T : Object
        {
            //path = "Resources/" + path;
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("[ResourcesMgr] 加载路径不能为空！");
                return null;
            }

            if (Cache.TryGetValue(path, out Object cachedAsset))
            {
                if (typeof(T) == typeof(GameObject))
                {
                    return Object.Instantiate(cachedAsset, father) as T;
                }

                return cachedAsset as T;
            }

            T asset = Resources.Load<T>(path);
            if (asset is null)
            {
                Debug.LogError($"[ResourcesMgr] 加载资源失败！路径: {path}，类型: {typeof(T).Name}");
                return null;
            }

            if (typeof(T) == typeof(GameObject))
            {
                return Object.Instantiate(asset, father);
            }

            Cache.Add(path, asset);
            return asset;
        }

        public static void ClearCache()
        {
            Cache.Clear();
            Resources.UnloadUnusedAssets();
        }
    }
}