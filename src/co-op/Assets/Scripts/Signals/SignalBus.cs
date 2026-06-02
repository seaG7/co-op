using System;
using System.Collections.Generic;
using UnityEngine;

namespace Signals
{
    public sealed class SignalBus
    {
        private readonly Dictionary<Type, List<Delegate>> _subscribers = new();

        public void Fire<T>(T signal)
        {
            if (!_subscribers.TryGetValue(typeof(T), out var list)) return;

            var snapshot = list.ToArray();
            for (int i = 0; i < snapshot.Length; i++)
            {
                if (snapshot[i] is not Action<T> action) continue;
                try { action(signal); }
                catch (Exception ex)
                {
                    Debug.LogError($"[SignalBus] Handler for {typeof(T).Name} threw: {ex}");
                }
            }
        }

        public void Subscribe<T>(Action<T> handler)
        {
            if (handler == null) return;
            if (!_subscribers.TryGetValue(typeof(T), out var list))
            {
                list = new List<Delegate>();
                _subscribers[typeof(T)] = list;
            }
            list.Add(handler);
        }

        public void Unsubscribe<T>(Action<T> handler)
        {
            if (handler == null) return;
            if (!_subscribers.TryGetValue(typeof(T), out var list))
            {
                Debug.LogWarning($"[SignalBus] Unsubscribe<{typeof(T).Name}>: no subscribers registered.");
                return;
            }
            if (!list.Remove(handler))
                Debug.LogWarning($"[SignalBus] Unsubscribe<{typeof(T).Name}>: handler not found.");
        }

        public bool TryUnsubscribe<T>(Action<T> handler)
        {
            if (handler == null) return false;
            return _subscribers.TryGetValue(typeof(T), out var list) && list.Remove(handler);
        }
    }
}
