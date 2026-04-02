using System.Collections.Generic;
using UnityEngine;

namespace StaticDrift.Pooling
{
    public class ObjectPooler : MonoBehaviour
    {
        [System.Serializable]
        private class Pool
        {
            public string Id;
            public GameObject Prefab;
            public int InitialSize = 16;
        }

        [SerializeField] private List<Pool> _pools = new List<Pool>();

        private readonly Dictionary<string, Queue<GameObject>> _poolById =
            new Dictionary<string, Queue<GameObject>>();

        private readonly Dictionary<GameObject, string> _idByPrefab =
            new Dictionary<GameObject, string>();

        private readonly Dictionary<GameObject, GameObject> _prefabByInstance =
            new Dictionary<GameObject, GameObject>(256);

        private void Awake()
        {
            InitializePools();
        }

        private void InitializePools()
        {
            int count = _pools.Count;
            for (int i = 0; i < count; i++)
            {
                Pool pool = _pools[i];
                if (pool == null || pool.Prefab == null || string.IsNullOrEmpty(pool.Id))
                {
                    continue;
                }

                if (!_poolById.ContainsKey(pool.Id))
                {
                    _poolById.Add(pool.Id, new Queue<GameObject>(pool.InitialSize));
                }

                if (!_idByPrefab.ContainsKey(pool.Prefab))
                {
                    _idByPrefab.Add(pool.Prefab, pool.Id);
                }

                Queue<GameObject> queue = _poolById[pool.Id];

                for (int j = 0; j < pool.InitialSize; j++)
                {
                    GameObject instance = Instantiate(pool.Prefab, transform);
                    RegisterInstance(instance, pool.Prefab);
                    instance.SetActive(false);
                    queue.Enqueue(instance);
                }
            }
        }

        public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (prefab == null)
            {
                return null;
            }

            string id;
            if (!_idByPrefab.TryGetValue(prefab, out id))
            {
                return null;
            }

            return SpawnById(id, position, rotation);
        }

        public GameObject SpawnById(string id, Vector3 position, Quaternion rotation)
        {
            if (string.IsNullOrEmpty(id))
            {
                return null;
            }

            Queue<GameObject> queue;
            if (!_poolById.TryGetValue(id, out queue))
            {
                return null;
            }

            GameObject instance = queue.Count > 0 ? queue.Dequeue() : null;
            if (instance == null)
            {
                Pool pool = GetPoolConfig(id);
                if (pool == null || pool.Prefab == null)
                {
                    return null;
                }

                instance = Instantiate(pool.Prefab, transform);
                RegisterInstance(instance, pool.Prefab);
            }

            instance.transform.SetPositionAndRotation(position, rotation);
            instance.SetActive(true);
            return instance;
        }

        public void Despawn(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            string id;
            GameObject prefabKey = GetOriginalPrefab(instance);
            if (prefabKey == null || !_idByPrefab.TryGetValue(prefabKey, out id))
            {
                instance.SetActive(false);
                return;
            }

            Queue<GameObject> queue;
            if (!_poolById.TryGetValue(id, out queue))
            {
                instance.SetActive(false);
                return;
            }

            instance.SetActive(false);
            queue.Enqueue(instance);
        }

        private Pool GetPoolConfig(string id)
        {
            int count = _pools.Count;
            for (int i = 0; i < count; i++)
            {
                Pool pool = _pools[i];
                if (pool != null && pool.Id == id)
                {
                    return pool;
                }
            }

            return null;
        }

        private GameObject GetOriginalPrefab(GameObject instance)
        {
            if (instance == null)
            {
                return null;
            }

            GameObject prefab;
            if (_prefabByInstance.TryGetValue(instance, out prefab))
            {
                return prefab;
            }

            return null;
        }

        private void RegisterInstance(GameObject instance, GameObject prefab)
        {
            if (instance == null || prefab == null)
            {
                return;
            }

            if (_prefabByInstance.ContainsKey(instance))
            {
                _prefabByInstance[instance] = prefab;
            }
            else
            {
                _prefabByInstance.Add(instance, prefab);
            }
        }
    }
}
