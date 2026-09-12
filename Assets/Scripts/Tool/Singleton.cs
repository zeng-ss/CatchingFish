using UnityEngine;

namespace Tool
{
    /// <summary>
    /// MonoBehaviour 单例基类。
    /// 刻意不做 DontDestroyOnLoad：本游戏只有一个场景，切场景即释放，
    /// 这样静态引用不会残留到下局游戏（静态事件忘记反注册是 Unity 里最常见的坑）。
    /// </summary>
    public abstract class MonoSingleton<T> : MonoBehaviour where T : MonoSingleton<T>
    {
        private static T _instance;

        /// <summary>全局唯一实例，可能为 null（未挂载 / 已销毁）。</summary>
        public static T Instance => _instance;

        public static bool Exists => _instance != null;

        protected virtual void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Debug.LogWarning($"[Singleton] {typeof(T).Name} 已存在，销毁重复实例：{name}", this);
                // 只销毁组件本身，避免误删同物体上的其它控制器
                Destroy(this);
                return;
            }

            _instance = (T)this;
        }

        protected virtual void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }
    }
}
