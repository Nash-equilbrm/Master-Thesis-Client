using System;
using System.Collections.Generic;
using UnityEngine;

namespace Thesis.Patterns
{
    public class ObjectPooling : Singleton<ObjectPooling>
    {
        [Serializable]
        public class ObjectPool
        {
            [Tooltip("Pool's name"), SerializeField]
            private string name;
            public string Name => name;

            [Tooltip("The object to instantiate"), SerializeField]
            private GameObject prefab;

            [Tooltip("The pool of instantiated objects"), SerializeField]
            private List<GameObject> pool = new List<GameObject>();

            public GameObject Get(Vector3 position = default, Vector3 rotation = default, Transform parent = null)
            {
                for (int i = 0; i < pool.Count; i++)
                {
                    if (!pool[i].activeInHierarchy)
                    {
                        pool[i].transform.SetParent(parent);
                        pool[i].transform.position = position;
                        pool[i].transform.rotation = Quaternion.Euler(rotation);
                        pool[i].SetActive(true);
                        return pool[i];
                    }
                }

                var obj = UnityEngine.Object.Instantiate(prefab, position, Quaternion.Euler(rotation));
                obj.transform.SetParent(parent);
                pool.Add(obj);
                return obj;
            }

            public void DestroyUnused()
            {
                for (int i = pool.Count - 1; i >= 0; i--)
                {
                    if (!pool[i].activeInHierarchy)
                    {
                        UnityEngine.Object.Destroy(pool[i]);
                        pool.RemoveAt(i);
                    }
                }
            }

            public void DestroyAll()
            {
                foreach (var obj in pool)
                    UnityEngine.Object.Destroy(obj);
                pool.Clear();
            }

            public void Prepare(int count)
            {
                for (int i = 0; i < count; i++)
                {
                    var obj = UnityEngine.Object.Instantiate(prefab);
                    obj.SetActive(false);
                    pool.Add(obj);
                }
            }
        }

        private static Transform _inactiveObjects;
        private static Transform InactiveObjects
        {
            get
            {
                if (!_inactiveObjects)
                    _inactiveObjects = new GameObject("Pool").transform;
                return _inactiveObjects;
            }
        }

        public List<ObjectPool> pools = new List<ObjectPool>();

        public ObjectPool GetPool(string poolName)
        {
            foreach (var pool in pools)
                if (pool.Name == poolName)
                    return pool;
            return null;
        }

        public static void Remove(GameObject obj)
        {
            obj.SetActive(false);
            obj.transform.SetParent(InactiveObjects);
        }
    }
}
