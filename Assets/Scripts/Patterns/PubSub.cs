using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Thesis.Patterns
{
    public class PubSub : Singleton<PubSub>
    {
        private Dictionary<EventID, Action<object>> _listeners = new Dictionary<EventID, Action<object>>();

        public void Register(EventID id, Action<object> action)
        {
            if (action == null) return;
            if (_listeners.ContainsKey(id))
            {
                if (!_listeners[id].GetInvocationList().Contains(action))
                    _listeners[id] += action;
            }
            else
            {
                _listeners.Add(id, _ => { });
                _listeners[id] += action;
            }
        }

        public void Unregister(EventID id, Action<object> action)
        {
            if (_listeners.ContainsKey(id) && action != null)
                if (_listeners[id].GetInvocationList().Contains(action))
                    _listeners[id] -= action;
        }

        public void UnregisterAll(EventID id)
        {
            if (_listeners.ContainsKey(id))
                _listeners.Remove(id);
        }

        public void Broadcast(EventID id, object data)
        {
            if (_listeners.ContainsKey(id))
                _listeners[id].Invoke(data);
        }
    }

    public static class PubSubExtension
    {
        public static void Register(this MonoBehaviour listener, EventID id, Action<object> action)
        {
            if (PubSub.HasInstance) PubSub.Instance.Register(id, action);
        }

        public static void Unregister(this MonoBehaviour listener, EventID id, Action<object> action)
        {
            if (PubSub.HasInstance) PubSub.Instance.Unregister(id, action);
        }

        public static void UnregisterAll(this MonoBehaviour listener, EventID id)
        {
            if (PubSub.HasInstance) PubSub.Instance.UnregisterAll(id);
        }

        public static void Broadcast(this MonoBehaviour listener, EventID id)
        {
            if (PubSub.HasInstance) PubSub.Instance.Broadcast(id, null);
        }

        public static void Broadcast(this MonoBehaviour listener, EventID id, object data)
        {
            if (PubSub.HasInstance) PubSub.Instance.Broadcast(id, data);
        }
    }
}
