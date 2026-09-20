using System.Collections.Generic;
using UnityEngine;

namespace Tool
{
    /// <summary>
    /// 【工具层】Resources 加载 + 缓存。
    /// 表里的资源只 `Resources.Load` 一次，之后都走缓存；
    /// 传了 parent 且资源是 GameObject 时顺便实例化并挂到 parent 下。
    /// </summary>
    public static class ResourcesMgr
    {
        private static readonly Dictionary<string, Object> Cache = new();

        public static T Load<T>(string path, Transform parent = null) where T : Object
        {
            if (string.IsNullOrEmpty(path))
            {
                Debug.LogError("[ResourcesMgr] 加载路径不能为空。");
                return null;
            }

            if (!Cache.TryGetValue(path, out Object asset))
            {
                asset = Resources.Load<T>(path);
                if (asset == null)
                {
                    Debug.LogError($"[ResourcesMgr] 加载失败：{path}（{typeof(T).Name}）");
                    return null;
                }

                Cache.Add(path, asset);
            }

            return asset is GameObject prefab ? Object.Instantiate(prefab, parent) as T : asset as T;
        }
    }
}
