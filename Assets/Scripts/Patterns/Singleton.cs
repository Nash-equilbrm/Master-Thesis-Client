using UnityEngine;

namespace Thesis.Patterns
{
    public class Singleton<T> : MonoBehaviour where T : Singleton<T>
    {
        private static T instance;

        public static T Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = FindAnyObjectByType<T>();
                    if (instance == null)
                        Debug.Log($"No {typeof(T).Name} Singleton Instance");
                }
                return instance;
            }
        }

        public static bool HasInstance => instance != null;

        protected virtual void Awake()
        {
            CheckInstance();
        }

        protected bool CheckInstance()
        {
            if (instance == null)
            {
                instance = (T)(object)this;
                DontDestroyOnLoad(this);
                return true;
            }
            if (instance == this)
            {
                DontDestroyOnLoad(this);
                return true;
            }
            Destroy(gameObject);
            return false;
        }
    }
}
