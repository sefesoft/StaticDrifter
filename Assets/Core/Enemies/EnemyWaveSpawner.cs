using UnityEngine;
using StaticDrift.Pooling;

namespace StaticDrift.Enemies
{
    public class EnemyWaveSpawner : MonoBehaviour
    {
        [System.Serializable]
        public class AsteroidPools
        {
            public string LargePoolId = "Asteroid_Large";
            public string MediumPoolId = "Asteroid_Medium";
            public string SmallPoolId = "Asteroid_Small";
        }

        [System.Serializable]
        public class AsteroidStats
        {
            public float LargeHealth = 1f;
            public float MediumHealth = 1f;
            public float SmallHealth = 1f;
            public Vector3 LargeScale = new Vector3(3.4f, 3.4f, 1f);
            public Vector3 MediumScale = new Vector3(2.2f, 2.2f, 1f);
            public Vector3 SmallScale = new Vector3(1.4f, 1.4f, 1f);
            public float LargeSpinMin = 12f;
            public float LargeSpinMax = 38f;
            public float MediumSpinMin = 35f;
            public float MediumSpinMax = 90f;
            public float SmallSpinMin = 80f;
            public float SmallSpinMax = 180f;
        }

        [SerializeField] private ObjectPooler _pooler;
        [SerializeField] private Camera _camera;
        [SerializeField] private AsteroidPools _asteroidPools = new AsteroidPools();
        [SerializeField] private AsteroidStats _asteroidStats = new AsteroidStats();
        [SerializeField] private float _spawnInterval = 1.2f;
        [SerializeField] private float _spawnEdgeMargin = 1f;
        [SerializeField] private float _screenWrapMargin = 0.8f;
        [SerializeField] private float _minSpeed = 1.8f;
        [SerializeField] private float _maxSpeed = 4.5f;
        [SerializeField] private float _spawnWeightLarge = 0.45f;
        [SerializeField] private float _spawnWeightMedium = 0.35f;
        [SerializeField] private float _spawnWeightSmall = 0.20f;

        private float _spawnTimer;

        public Camera ActiveCamera => _camera != null ? _camera : Camera.main;

        private void Awake()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
            }
        }

        private void Update()
        {
            if (_pooler == null)
            {
                return;
            }

            _spawnTimer += Time.deltaTime;
            if (_spawnTimer < _spawnInterval)
            {
                return;
            }

            _spawnTimer = 0f;
            SpawnRandomAsteroid(GetOffCameraSpawnPosition(_spawnEdgeMargin), default(Vector2), false);
        }

        public void HandleAsteroidDestroyed(Asteroid.AsteroidSize destroyedSize, Vector2 origin, Vector2 parentVelocity)
        {
            if (destroyedSize == Asteroid.AsteroidSize.Large)
            {
                SpawnSplitChildren(Asteroid.AsteroidSize.Medium, 2, origin, parentVelocity);
                return;
            }

            if (destroyedSize == Asteroid.AsteroidSize.Medium)
            {
                SpawnSplitChildren(Asteroid.AsteroidSize.Small, 3, origin, parentVelocity);
            }
        }

        private void SpawnSplitChildren(Asteroid.AsteroidSize childSize, int count, Vector2 origin, Vector2 parentVelocity)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 jitter = Random.insideUnitCircle * 0.35f;
                Vector2 spawnPos = origin + jitter;
                SpawnAsteroid(childSize, spawnPos, parentVelocity, true);
            }
        }

        private void SpawnRandomAsteroid(Vector3 spawnPosition, Vector2 parentVelocity, bool fromSplit)
        {
            Asteroid.AsteroidSize size = ChooseRandomSize();
            SpawnAsteroid(size, spawnPosition, parentVelocity, fromSplit);
        }

        private void SpawnAsteroid(Asteroid.AsteroidSize size, Vector2 spawnPosition, Vector2 parentVelocity, bool fromSplit)
        {
            string poolId = GetPoolId(size);
            if (string.IsNullOrEmpty(poolId))
            {
                return;
            }

            GameObject instance = _pooler.SpawnById(poolId, spawnPosition, Quaternion.identity);
            if (instance == null)
            {
                Debug.LogWarning("EnemyWaveSpawner: SpawnById failed for asteroid pool '" + poolId + "'.");
                return;
            }

            Asteroid asteroid = instance.GetComponent<Asteroid>();
            if (asteroid == null)
            {
                Debug.LogWarning("EnemyWaveSpawner: Pooled object in '" + poolId + "' has no Asteroid component.");
                _pooler.Despawn(instance);
                return;
            }

            Vector2 direction = fromSplit
                ? (Random.insideUnitCircle + parentVelocity.normalized * 0.5f).normalized
                : GetInwardDirectionFromSpawn(spawnPosition);
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Random.insideUnitCircle.normalized;
            }

            float speed = Random.Range(_minSpeed, _maxSpeed);
            Vector2 velocity = direction * speed;
            if (fromSplit)
            {
                velocity += parentVelocity * 0.35f;
            }

            float spin = GetRandomSpin(size);
            asteroid.Initialize(
                size,
                GetHealth(size),
                _screenWrapMargin,
                velocity,
                spin,
                GetScale(size),
                _pooler,
                this);
        }

        private float GetRandomSpin(Asteroid.AsteroidSize size)
        {
            float minAbs;
            float maxAbs;
            if (size == Asteroid.AsteroidSize.Large)
            {
                minAbs = Mathf.Abs(_asteroidStats.LargeSpinMin);
                maxAbs = Mathf.Abs(_asteroidStats.LargeSpinMax);
            }
            else if (size == Asteroid.AsteroidSize.Medium)
            {
                minAbs = Mathf.Abs(_asteroidStats.MediumSpinMin);
                maxAbs = Mathf.Abs(_asteroidStats.MediumSpinMax);
            }
            else
            {
                minAbs = Mathf.Abs(_asteroidStats.SmallSpinMin);
                maxAbs = Mathf.Abs(_asteroidStats.SmallSpinMax);
            }

            if (maxAbs < minAbs)
            {
                float tmp = minAbs;
                minAbs = maxAbs;
                maxAbs = tmp;
            }

            float value = Random.Range(minAbs, maxAbs);
            float sign = Random.value < 0.5f ? -1f : 1f;
            return value * sign;
        }

        private Asteroid.AsteroidSize ChooseRandomSize()
        {
            float total = _spawnWeightLarge + _spawnWeightMedium + _spawnWeightSmall;
            if (total <= 0f)
            {
                return Asteroid.AsteroidSize.Large;
            }

            float roll = Random.value * total;
            if (roll < _spawnWeightLarge)
            {
                return Asteroid.AsteroidSize.Large;
            }

            roll -= _spawnWeightLarge;
            if (roll < _spawnWeightMedium)
            {
                return Asteroid.AsteroidSize.Medium;
            }

            return Asteroid.AsteroidSize.Small;
        }

        private string GetPoolId(Asteroid.AsteroidSize size)
        {
            if (size == Asteroid.AsteroidSize.Large)
            {
                return _asteroidPools.LargePoolId;
            }

            if (size == Asteroid.AsteroidSize.Medium)
            {
                return _asteroidPools.MediumPoolId;
            }

            return _asteroidPools.SmallPoolId;
        }

        private float GetHealth(Asteroid.AsteroidSize size)
        {
            if (size == Asteroid.AsteroidSize.Large)
            {
                return _asteroidStats.LargeHealth;
            }

            if (size == Asteroid.AsteroidSize.Medium)
            {
                return _asteroidStats.MediumHealth;
            }

            return _asteroidStats.SmallHealth;
        }

        private Vector3 GetScale(Asteroid.AsteroidSize size)
        {
            if (size == Asteroid.AsteroidSize.Large)
            {
                return _asteroidStats.LargeScale;
            }

            if (size == Asteroid.AsteroidSize.Medium)
            {
                return _asteroidStats.MediumScale;
            }

            return _asteroidStats.SmallScale;
        }

        private Vector3 GetOffCameraSpawnPosition(float margin)
        {
            Camera cam = _camera != null ? _camera : Camera.main;
            if (cam == null)
            {
                return Vector3.zero;
            }

            if (!cam.orthographic)
            {
                // Perspective: spawn in front of camera at distance
                float dist = 15f;
                Vector2 dir = Random.insideUnitCircle.normalized;
                Vector3 center = cam.transform.position + cam.transform.forward * dist;
                float span = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * dist * 2f;
                return center + new Vector3(dir.x, dir.y, 0f) * (span * 0.5f + margin);
            }

            float halfH = cam.orthographicSize + margin;
            float halfW = halfH * cam.aspect;
            float radius = Mathf.Sqrt(halfW * halfW + halfH * halfH);
            Vector2 d = Random.insideUnitCircle.normalized;
            Vector3 origin = cam.transform.position;
            origin.z = 0f;
            return origin + new Vector3(d.x * radius, d.y * radius, 0f);
        }

        private Vector2 GetInwardDirectionFromSpawn(Vector2 spawnPosition)
        {
            Camera cam = ActiveCamera;
            if (cam == null)
            {
                return Random.insideUnitCircle.normalized;
            }

            Vector2 center = cam.transform.position;
            Vector2 inward = center - spawnPosition;
            if (inward.sqrMagnitude < 0.0001f)
            {
                return Random.insideUnitCircle.normalized;
            }

            return inward.normalized;
        }
    }
}
