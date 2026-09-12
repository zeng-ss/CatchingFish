using System;
using System.Collections.Generic;
using UnityEngine;

namespace Core
{
    public static class EventMgr
    {
        private static readonly Dictionary<GameEvent, Action<object>> Channels = new();

        public static void Subscribe(GameEvent e, Action<object> handler)
        {
            if (handler == null)
            {
                return;
            }

            if (Channels.TryGetValue(e, out Action<object> d))
            {
                Channels[e] = d + handler;
            }
            else
            {
                Channels[e] = handler;
            }
        }

        public static void Unsubscribe(GameEvent e, Action<object> handler)
        {
            if (handler == null || !Channels.TryGetValue(e, out Action<object> d))
            {
                return;
            }

            d -= handler;
            if (d == null)
            {
                Channels.Remove(e);
            }
            else
            {
                Channels[e] = d;
            }
        }

        public static void Publish(GameEvent e, object payload = null)
        {
            if (!Channels.TryGetValue(e, out Action<object> d) || d == null)
            {
                return;
            }

            try
            {
                d.Invoke(payload);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[EventMgr] 分发 {e} 时异常：{ex}");
            }
        }

        /// <summary>整局结束/销毁时调用，防止静态订阅跨场景泄漏。</summary>
        public static void Clear()
        {
            Channels.Clear();
        }
    }
}
