using UnityEngine;
using StaticDrift.Pooling;
using System.Collections.Generic;
using StaticDrift.Enemies.Data;

namespace StaticDrift.Enemies
{
    /// <summary>
    /// Single spawn loop: each timer tick spawns at most one thing — either a drone (random roll) or one asteroid.
    /// Asteroid kind (rock/ice/etc.) is picked inside that one spawn and does not add parallel spawners or extra ticks.
    /// </summary>
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
        public class AsteroidKindSprites
        {
            public Sprite Rock;
            public Sprite Ice;
            public Sprite Obsidian;
            public Sprite Copper;
        }

        [System.Serializable]
        public class AsteroidStats
        {
            public float LargeHealth = 3f;
            public float MediumHealth = 2f;
            public float SmallHealth = 1f;
            [Tooltip("Asteroid.Initialize uses X as uniform scale (sets X=Y=Z). Z is ignored; edit X only.")]
            public Vector3 LargeScale = new Vector3(3.4f, 3.4f, 3.4f);
            [Tooltip("Asteroid.Initialize uses X as uniform scale (sets X=Y=Z). Z is ignored; edit X only.")]
            public Vector3 MediumScale = new Vector3(1.76f, 1.76f, 1.76f);
            [Tooltip("Asteroid.Initialize uses X as uniform scale (sets X=Y=Z). Z is ignored; edit X only.")]
            public Vector3 SmallScale = new Vector3(0.784f, 0.784f, 0.784f);
            public float LargeSpinMin = 12f;
            public float LargeSpinMax = 38f;
            public float MediumSpinMin = 35f;
            public float MediumSpinMax = 90f;
            public float SmallSpinMin = 80f;
            public float SmallSpinMax = 180f;
        }

        [System.Serializable]
        public class DroneSpawnType
        {
            public string PoolId = "Enemy_Drone";
            public EnemyData Data;
            [Range(0f, 1f)] public float BaseChance = 0.10f;
        }

        [SerializeField] private ObjectPooler _pooler;
        [SerializeField] private Camera _camera;
        [SerializeField] private AsteroidPools _asteroidPools = new AsteroidPools();
        [SerializeField] private AsteroidStats _asteroidStats = new AsteroidStats();
        [SerializeField] private float _spawnInterval = 2.85f;
        [SerializeField] private float _spawnEdgeMargin = 1f;
        [SerializeField] private float _screenWrapMargin = 0.8f;
        [SerializeField] private float _minSpeed = 1.8f;
        [SerializeField] private float _maxSpeed = 4.5f;
        [SerializeField] private float _spawnWeightLarge = 0.45f;
        [SerializeField] private float _spawnWeightMedium = 0.35f;
        [SerializeField] private float _spawnWeightSmall = 0.20f;
        [SerializeField] private float _minSpawnInterval = 0.4f;
        [SerializeField] private List<DroneSpawnType> _droneSpawnTypes = new List<DroneSpawnType>();
        [SerializeField] private float _droneChancePerWave = 0.02f;
        [SerializeField] private int _eliteWaveEvery = 4;
        [SerializeField] private float _eliteHealthMultiplier = 2.1f;
        [Tooltip("Elite waves multiply spawn interval by this (closer to 1 = less extra spawn pressure).")]
        [SerializeField] private float _eliteSpawnIntervalMultiplier = 0.92f;
        [SerializeField] private float _waveOneSpawnIntervalMultiplier = 1.7f;
        [SerializeField] private float _waveOneSpeedMultiplier = 0.78f;
        [SerializeField] private bool _disableDronesOnWaveOne = true;
        [Header("Early Wave Tuning")]
        [Tooltip("For early waves, bias asteroid sizes away from Large (which splits into many pieces).")]
        [SerializeField] private int _earlyWaveWeightThreshold = 3;
        [SerializeField] private float _earlyWaveLargeWeightMultiplier = 0.75f;
        [SerializeField] private float _earlyWaveMediumWeightMultiplier = 0.9f;
        [SerializeField] private float _earlyWaveSmallWeightMultiplier = 1.25f;
        [Header("Asteroid kinds")]
        [Tooltip("Visual/stats only. Spawn rate is still one asteroid per tick above; there is no separate spawner per kind.")]
        [SerializeField] private AsteroidKindSprites _kindSprites;
        [SerializeField] private float _obsidianHealthMultiplier = 5.5f;
        [SerializeField] private float _copperHealthMultiplier = 1.65f;
        [Tooltip("Per wave after 1, added chance (capped) to spawn Ice/Obsidian/Copper instead of Rock.")]
        [SerializeField] private float _specialAsteroidChancePerWave = 0.04f;
        [SerializeField] private float _specialAsteroidMaxChance = 0.38f;

        [Header("Spawn pacing")]
        [Tooltip("Per wave, base spawn interval is multiplied by this^waveIndex. Closer to 1 = slower ramp (fewer asteroids in late waves).")]
        [SerializeField] [Range(0.88f, 1f)] private float _spawnIntervalDecayPerWave = 0.965f;

        private float _spawnTimer;
        private float _effectiveSpawnInterval;
        private float _effectiveMinSpeed;
        private float _effectiveMaxSpeed;
        private float _effectiveDroneChance;
        private bool _spawningEnabled = true;
        private bool _isEliteWave;
        private int _configuredWaveNumber = 1;
        private Transform _playerTarget;

        public Camera ActiveCamera => _camera != null ? _camera : Camera.main;
        public bool IsEliteWave => _isEliteWave;

        public bool IsEliteWaveNumber(int waveIndex)
        {
            int w = Mathf.Max(1, waveIndex);
            return _eliteWaveEvery > 0 && (w % _eliteWaveEvery == 0);
        }

        private void Awake()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
            }

            // Migration guard: if older scenes still use 1/1/1 HP,
            // automatically move to the new breakpoint-friendly defaults.
            if (_asteroidStats != null
                && _asteroidStats.LargeHealth <= 1.01f
                && _asteroidStats.MediumHealth <= 1.01f
                && _asteroidStats.SmallHealth <= 1.01f)
            {
                _asteroidStats.LargeHealth = 3f;
                _asteroidStats.MediumHealth = 2f;
                _asteroidStats.SmallHealth = 1f;
            }

            _effectiveSpawnInterval = _spawnInterval;
            _effectiveMinSpeed = _minSpeed;
            _effectiveMaxSpeed = _maxSpeed;
            _effectiveDroneChance = 0.1f;

            EnsureKindSprites();
        }

        /// <summary>Inspector: right‑click the component → use this if scales/health never match code changes (scene overrides script defaults).</summary>
        [ContextMenu("Reset Asteroid Stats To Script Defaults")]
        private void ContextResetAsteroidStatsToScriptDefaults()
        {
            _asteroidStats = new AsteroidStats();
        }

        private void EnsureKindSprites()
        {
            if (_kindSprites == null)
            {
                _kindSprites = new AsteroidKindSprites();
            }

            if (_kindSprites.Rock == null)
            {
                _kindSprites.Rock = Resources.Load<Sprite>("Gameplay/Asteroids/AsteroidRock");
            }

            if (_kindSprites.Ice == null)
            {
                _kindSprites.Ice = Resources.Load<Sprite>("Gameplay/Asteroids/AsteroidIce");
            }

            if (_kindSprites.Obsidian == null)
            {
                _kindSprites.Obsidian = Resources.Load<Sprite>("Gameplay/Asteroids/AsteroidObsidian");
            }

            if (_kindSprites.Copper == null)
            {
                _kindSprites.Copper = Resources.Load<Sprite>("Gameplay/Asteroids/AsteroidCopper");
            }
        }

        private void Update()
        {
            if (_pooler == null)
            {
                return;
            }

            if (!_spawningEnabled)
            {
                return;
            }

            _spawnTimer += Time.deltaTime;
            if (_spawnTimer < _effectiveSpawnInterval)
            {
                return;
            }

            _spawnTimer = 0f;
            Vector3 droneSpawnPosition = GetOffCameraSpawnPosition(
                ClampSpawnOutwardPadding(_spawnEdgeMargin * 0.35f));
            if (ShouldSpawnDroneThisTick() && SpawnDrone(droneSpawnPosition))
            {
                return;
            }

            SpawnRandomAsteroid(default(Vector2), false);
        }

        public void SetSpawningEnabled(bool enabled)
        {
            _spawningEnabled = enabled;
            if (!enabled)
            {
                _spawnTimer = 0f;
            }
        }

        public void ClearActiveAsteroids()
        {
            Asteroid[] asteroids = FindObjectsByType<Asteroid>(FindObjectsSortMode.None);
            int count = asteroids != null ? asteroids.Length : 0;
            for (int i = 0; i < count; i++)
            {
                Asteroid asteroid = asteroids[i];
                if (asteroid == null)
                {
                    continue;
                }

                if (_pooler != null)
                {
                    _pooler.Despawn(asteroid.gameObject);
                }
                else
                {
                    asteroid.gameObject.SetActive(false);
                }
            }
        }

        public void ConfigureForWave(int waveIndex)
        {
            int wave = Mathf.Max(1, waveIndex);
            _configuredWaveNumber = wave;
            float progress = wave - 1;

            float interval = _spawnInterval * Mathf.Pow(_spawnIntervalDecayPerWave, progress);
            _effectiveSpawnInterval = Mathf.Max(_minSpawnInterval, interval);

            _effectiveMinSpeed = _minSpeed + (0.18f * progress);
            _effectiveMaxSpeed = _maxSpeed + (0.24f * progress);
            _isEliteWave = _eliteWaveEvery > 0 && (wave % _eliteWaveEvery == 0);
            _effectiveDroneChance = Mathf.Clamp01(0.1f + _droneChancePerWave * progress);

            if (wave == 1)
            {
                _effectiveSpawnInterval *= Mathf.Max(1f, _waveOneSpawnIntervalMultiplier);
                _effectiveMinSpeed *= Mathf.Clamp(_waveOneSpeedMultiplier, 0.4f, 1f);
                _effectiveMaxSpeed *= Mathf.Clamp(_waveOneSpeedMultiplier, 0.4f, 1f);
                if (_disableDronesOnWaveOne)
                {
                    _effectiveDroneChance = 0f;
                }
            }

            if (_isEliteWave)
            {
                _effectiveSpawnInterval = Mathf.Max(_minSpawnInterval, _effectiveSpawnInterval * _eliteSpawnIntervalMultiplier);
            }
        }

        public void HandleAsteroidDestroyed(Asteroid.AsteroidSize destroyedSize, Vector2 origin, Vector2 parentVelocity, Asteroid.AsteroidKind kind)
        {
            if (destroyedSize == Asteroid.AsteroidSize.Large)
            {
                SpawnSplitChildren(Asteroid.AsteroidSize.Medium, 2, origin, parentVelocity, kind);
                return;
            }

            if (destroyedSize == Asteroid.AsteroidSize.Medium)
            {
                SpawnSplitChildren(Asteroid.AsteroidSize.Small, 3, origin, parentVelocity, kind);
            }
        }

        private void SpawnSplitChildren(Asteroid.AsteroidSize childSize, int count, Vector2 origin, Vector2 parentVelocity, Asteroid.AsteroidKind kind)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 jitter = Random.insideUnitCircle * 0.35f;
                Vector2 spawnPos = origin + jitter;
                SpawnAsteroid(childSize, spawnPos, parentVelocity, true, 1f, kind);
            }
        }

        private void SpawnRandomAsteroid(Vector2 parentVelocity, bool fromSplit)
        {
            Asteroid.AsteroidSize size = ChooseRandomSize();
            Asteroid.AsteroidKind kind = ChooseRandomKind();
            float healthMultiplier = 1f;
            if (_isEliteWave && size == Asteroid.AsteroidSize.Large)
            {
                healthMultiplier = _eliteHealthMultiplier;
            }

            float outward = ClampSpawnOutwardPadding(GetOutwardSpawnPaddingForSize(size));
            Vector2 spawnPosition = GetOffCameraSpawnPosition(outward);
            SpawnAsteroid(size, spawnPosition, parentVelocity, fromSplit, healthMultiplier, kind);
        }

        private void SpawnAsteroid(
            Asteroid.AsteroidSize size,
            Vector2 spawnPosition,
            Vector2 parentVelocity,
            bool fromSplit,
            float healthMultiplier,
            Asteroid.AsteroidKind kind)
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

            Vector2 direction;
            if (fromSplit)
            {
                direction = (Random.insideUnitCircle + parentVelocity.normalized * 0.5f).normalized;
            }
            else
            {
                direction = GetInwardDirectionFromSpawn(spawnPosition);
                if (direction.sqrMagnitude < 0.0001f)
                {
                    direction = Random.insideUnitCircle.normalized;
                }
                else
                {
                    direction = direction.normalized;
                    float jitterRad = Random.Range(-24f, 24f) * Mathf.Deg2Rad;
                    float cos = Mathf.Cos(jitterRad);
                    float sin = Mathf.Sin(jitterRad);
                    direction = new Vector2(
                        direction.x * cos - direction.y * sin,
                        direction.x * sin + direction.y * cos).normalized;
                }
            }

            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Random.insideUnitCircle.normalized;
            }

            float speed = Random.Range(_effectiveMinSpeed, _effectiveMaxSpeed);
            Vector2 velocity = direction * speed;
            if (fromSplit)
            {
                velocity += parentVelocity * 0.35f;
            }

            float spin = GetRandomSpin(size);
            float totalHealth = GetHealth(size) * Mathf.Max(1f, healthMultiplier) * GetKindHealthMultiplier(kind);
            asteroid.Initialize(
                size,
                kind,
                totalHealth,
                GetSpriteForKind(kind),
                _screenWrapMargin,
                velocity,
                spin,
                GetScale(size),
                _pooler,
                this);
        }

        private Asteroid.AsteroidKind ChooseRandomKind()
        {
            if (_configuredWaveNumber <= 1)
            {
                return Asteroid.AsteroidKind.Rock;
            }

            float extra = Mathf.Min(_specialAsteroidMaxChance, (_configuredWaveNumber - 1) * _specialAsteroidChancePerWave);
            if (Random.value >= extra)
            {
                return Asteroid.AsteroidKind.Rock;
            }

            float t = Random.value * 3f;
            if (t < 1f)
            {
                return Asteroid.AsteroidKind.Ice;
            }

            if (t < 2f)
            {
                return Asteroid.AsteroidKind.Obsidian;
            }

            return Asteroid.AsteroidKind.Copper;
        }

        private float GetKindHealthMultiplier(Asteroid.AsteroidKind kind)
        {
            switch (kind)
            {
                case Asteroid.AsteroidKind.Obsidian:
                    return Mathf.Max(1f, _obsidianHealthMultiplier);
                case Asteroid.AsteroidKind.Copper:
                    return Mathf.Max(1f, _copperHealthMultiplier);
                default:
                    return 1f;
            }
        }

        private Sprite GetSpriteForKind(Asteroid.AsteroidKind kind)
        {
            if (_kindSprites == null)
            {
                return null;
            }

            switch (kind)
            {
                case Asteroid.AsteroidKind.Ice:
                    return _kindSprites.Ice;
                case Asteroid.AsteroidKind.Obsidian:
                    return _kindSprites.Obsidian;
                case Asteroid.AsteroidKind.Copper:
                    return _kindSprites.Copper;
                default:
                    return _kindSprites.Rock;
            }
        }

        private bool ShouldSpawnDroneThisTick()
        {
            int count = _droneSpawnTypes != null ? _droneSpawnTypes.Count : 0;
            if (count == 0)
            {
                return false;
            }

            return Random.value < _effectiveDroneChance;
        }

        private bool SpawnDrone(Vector3 spawnPosition)
        {
            int count = _droneSpawnTypes != null ? _droneSpawnTypes.Count : 0;
            if (count == 0)
            {
                return false;
            }

            int chosen = Random.Range(0, count);
            DroneSpawnType config = _droneSpawnTypes[chosen];
            if (config == null || string.IsNullOrEmpty(config.PoolId) || config.Data == null)
            {
                return false;
            }

            if (Random.value > config.BaseChance)
            {
                return false;
            }

            GameObject instance = _pooler.SpawnById(config.PoolId, spawnPosition, Quaternion.identity);
            if (instance == null)
            {
                return false;
            }

            Enemy enemy = instance.GetComponent<Enemy>();
            if (enemy == null)
            {
                _pooler.Despawn(instance);
                return false;
            }

            Transform target = ResolvePlayerTarget();
            enemy.Initialize(config.Data, _pooler, target);
            return true;
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
            float largeWeight = _spawnWeightLarge;
            float mediumWeight = _spawnWeightMedium;
            float smallWeight = _spawnWeightSmall;
            if (_isEliteWave)
            {
                largeWeight += 0.18f;
                mediumWeight += 0.06f;
            }

            if (!_isEliteWave && _configuredWaveNumber <= Mathf.Max(1, _earlyWaveWeightThreshold))
            {
                largeWeight *= Mathf.Max(0f, _earlyWaveLargeWeightMultiplier);
                mediumWeight *= Mathf.Max(0f, _earlyWaveMediumWeightMultiplier);
                smallWeight *= Mathf.Max(0f, _earlyWaveSmallWeightMultiplier);
            }

            float total = largeWeight + mediumWeight + smallWeight;
            if (total <= 0f)
            {
                return Asteroid.AsteroidSize.Large;
            }

            float roll = Random.value * total;
            if (roll < largeWeight)
            {
                return Asteroid.AsteroidSize.Large;
            }

            roll -= largeWeight;
            if (roll < mediumWeight)
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

        /// <summary>
        /// Orthographic: spawn along a random screen edge, just outside the visible rect but still inside the
        /// wrap threshold (see Asteroid.WrapAtScreenEdges). The old circular spawn used a radius past that
        /// threshold, so asteroids wrapped to the opposite side almost immediately.
        /// </summary>
        private Vector3 GetOffCameraSpawnPosition(float outwardFromVisible)
        {
            Camera cam = _camera != null ? _camera : Camera.main;
            if (cam == null)
            {
                return Vector3.zero;
            }

            if (!cam.orthographic)
            {
                float dist = 15f;
                Vector2 dir = Random.insideUnitCircle.normalized;
                Vector3 center = cam.transform.position + cam.transform.forward * dist;
                float span = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * dist * 2f;
                return center + new Vector3(dir.x, dir.y, 0f) * (span * 0.5f + outwardFromVisible);
            }

            outwardFromVisible = Mathf.Max(0.05f, outwardFromVisible);
            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            Vector3 origin = cam.transform.position;
            origin.z = 0f;
            Vector2 c = new Vector2(origin.x, origin.y);

            int side = Random.Range(0, 4);
            float edgeT = Random.Range(0f, 1f);
            Vector2 p;
            switch (side)
            {
                case 0:
                    p = new Vector2(c.x + halfW + outwardFromVisible, Mathf.Lerp(c.y - halfH, c.y + halfH, edgeT));
                    break;
                case 1:
                    p = new Vector2(c.x - halfW - outwardFromVisible, Mathf.Lerp(c.y - halfH, c.y + halfH, edgeT));
                    break;
                case 2:
                    p = new Vector2(Mathf.Lerp(c.x - halfW, c.x + halfW, edgeT), c.y + halfH + outwardFromVisible);
                    break;
                default:
                    p = new Vector2(Mathf.Lerp(c.x - halfW, c.x + halfW, edgeT), c.y - halfH - outwardFromVisible);
                    break;
            }

            return new Vector3(p.x, p.y, 0f);
        }

        private float ClampSpawnOutwardPadding(float requested)
        {
            float maxPad = Mathf.Max(0.12f, _screenWrapMargin * 0.82f);
            return Mathf.Clamp(requested, 0.08f, maxPad);
        }

        private float GetOutwardSpawnPaddingForSize(Asteroid.AsteroidSize size)
        {
            Vector3 scale = GetScale(size);
            float asteroidRadius = Mathf.Max(scale.x, scale.y) * 0.5f;
            return _spawnEdgeMargin * 0.28f + asteroidRadius * 0.2f;
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

        private Transform ResolvePlayerTarget()
        {
            if (_playerTarget != null)
            {
                return _playerTarget;
            }

            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                _playerTarget = player.transform;
            }

            return _playerTarget;
        }
    }
}
