using UnityEngine;
using StaticDrift.Player;
using StaticDrift.Projectiles;
using System;
using System.Collections.Generic;

namespace StaticDrift.Enemies
{
    public enum BossCombatStyle
    {
        ChaseOnly = 0,
        ChaseAndGun = 1,
        ChaseAndSwarm = 2
    }

    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(PolygonCollider2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class BossShip : MonoBehaviour, IDamageable
    {
        [SerializeField] private BossCombatStyle _combatStyle = BossCombatStyle.ChaseOnly;
        [SerializeField] private float _moveSpeed = 1.8f;
        [SerializeField] private float _moveSpeedBonusPerLevel = 0.22f;
        [SerializeField] private float _maxMoveSpeed = 3.35f;
        [SerializeField] private float _turnSpeed = 4.2f;
        [SerializeField] private float _orbitAmount = 1.7f;
        [SerializeField] private float _contactDamage = 12f;
        [SerializeField] private float _contactInterval = 0.45f;
        [Tooltip("Optional; if empty, child named Gun is used when ChaseAndGun.")]
        [SerializeField] private Transform _gun;
        [Tooltip("Required for ChaseAndGun when firing.")]
        [SerializeField] private GameObject _bossProjectilePrefab;
        [SerializeField] private float _bossProjectileDamage = 9f;
        [SerializeField] private float _bossProjectileSpeed = 7.25f;
        [SerializeField] private float _bossProjectileSpawnOffset = 0.42f;
        [SerializeField] private float _baseFireInterval = 1.35f;
        [SerializeField] private float _fireIntervalReductionPerLevel = 0.085f;
        [SerializeField] private float _minFireInterval = 0.38f;
        [Header("Chase + swarm (insect)")]
        [SerializeField] private GameObject _mitePrefab;
        [SerializeField] private float _baseSwarmInterval = 4.1f;
        [SerializeField] private float _swarmIntervalReductionPerLevel = 0.32f;
        [SerializeField] private float _minSwarmInterval = 1.75f;
        [SerializeField] private float _initialSwarmDelay = 0.85f;
        [SerializeField] private int _baseMiteCount = 3;
        [SerializeField] private int _extraMitesPerLevel = 1;
        [SerializeField] private int _maxMitesPerSpawn = 6;
        [SerializeField] private float _miteSpawnRadius = 2.65f;
        [SerializeField] private float _miteVisualScale = 0.38f;
        [SerializeField] private float _miteMoveSpeed = 6.75f;
        [SerializeField] private float _miteMaxHealth = 9f;
        [SerializeField] private float _miteContactDamage = 5f;
        [SerializeField] private float _miteContactInterval = 0.38f;
        [SerializeField] private float _miteLifetimeSeconds = 6.5f;

        private Rigidbody2D _rigidbody2D;
        private SpriteRenderer _spriteRenderer;
        private Transform _target;
        private float _maxHealth;
        private float _currentHealth;
        private float _nextContactTime;
        private float _aliveTime;
        private bool _active;
        private static Sprite _proceduralBossSprite;

        private float _effectiveMoveSpeed;
        private float _nextFireTime;
        private float _fireInterval;
        private float _nextSwarmTime;
        private float _swarmInterval;
        private readonly List<BossMite> _activeMites = new List<BossMite>(12);
        private int _bossLevel = 1;

        public float MaxHealth => _maxHealth;
        public float CurrentHealth => _currentHealth;
        public float Health01 => _maxHealth > 0f ? Mathf.Clamp01(_currentHealth / _maxHealth) : 0f;
        public bool IsActiveBoss => _active && gameObject.activeSelf;
        public event Action Defeated;

        private void Awake()
        {
            _rigidbody2D = GetComponent<Rigidbody2D>();
            _spriteRenderer = GetComponent<SpriteRenderer>();

            _rigidbody2D.gravityScale = 0f;
            _rigidbody2D.bodyType = RigidbodyType2D.Kinematic;
            _rigidbody2D.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            if (_gun == null)
            {
                Transform found = transform.Find("Gun");
                if (found != null)
                {
                    _gun = found;
                }
            }

            Sprite visual = _spriteRenderer.sprite;
            if (visual == null)
            {
                visual = ResolveFallbackBossSprite();
                _spriteRenderer.sprite = visual;
            }

            _spriteRenderer.color = visual != null && visual == _proceduralBossSprite
                ? new Color(0.9f, 0.45f, 0.3f, 1f)
                : Color.white;
            _spriteRenderer.sortingOrder = 8;
            gameObject.SetActive(false);
        }

        public void Activate(Transform target, float health, Vector3 spawnPosition, int bossLevel)
        {
            DespawnAllMites();
            _target = target;
            _bossLevel = Mathf.Max(1, bossLevel);
            _maxHealth = Mathf.Max(1f, health);
            _currentHealth = _maxHealth;
            _nextContactTime = 0f;
            _aliveTime = 0f;
            _active = true;

            float rawSpeed = _moveSpeed + (_bossLevel - 1) * _moveSpeedBonusPerLevel;
            _effectiveMoveSpeed = Mathf.Min(_maxMoveSpeed, rawSpeed);

            if (_combatStyle == BossCombatStyle.ChaseAndGun)
            {
                _fireInterval = Mathf.Max(
                    _minFireInterval,
                    _baseFireInterval - (_bossLevel - 1) * _fireIntervalReductionPerLevel);
                _nextFireTime = Time.time + 0.35f;
            }

            if (_combatStyle == BossCombatStyle.ChaseAndSwarm)
            {
                _swarmInterval = Mathf.Max(
                    _minSwarmInterval,
                    _baseSwarmInterval - (_bossLevel - 1) * _swarmIntervalReductionPerLevel);
                _nextSwarmTime = Time.time + _initialSwarmDelay;
            }

            transform.position = spawnPosition;
            gameObject.SetActive(true);
        }

        public void Deactivate()
        {
            _active = false;
            DespawnAllMites();
            gameObject.SetActive(false);
        }

        public void UnregisterMite(BossMite mite)
        {
            _activeMites.Remove(mite);
        }

        private void RegisterMite(BossMite mite)
        {
            if (mite != null)
            {
                _activeMites.Add(mite);
            }
        }

        private void DespawnAllMites()
        {
            BossMite[] snapshot = _activeMites.ToArray();
            _activeMites.Clear();
            foreach (BossMite m in snapshot)
            {
                if (m != null)
                {
                    Destroy(m.gameObject);
                }
            }
        }

        private void OnDisable()
        {
            DespawnAllMites();
        }

        private void Update()
        {
            if (!_active)
            {
                return;
            }

            _aliveTime += Time.deltaTime;

            if (_combatStyle == BossCombatStyle.ChaseAndSwarm)
            {
                if (_mitePrefab != null && _target != null && Time.time >= _nextSwarmTime)
                {
                    SpawnMiteWave();
                    _nextSwarmTime = Time.time + _swarmInterval;
                }

                return;
            }

            if (_combatStyle != BossCombatStyle.ChaseAndGun || _bossProjectilePrefab == null || _gun == null)
            {
                return;
            }

            if (_target == null)
            {
                return;
            }

            if (Time.time < _nextFireTime)
            {
                return;
            }

            Vector2 origin = _gun.position;
            Vector2 toPlayer = (Vector2)_target.position - origin;
            if (toPlayer.sqrMagnitude < 0.0004f)
            {
                return;
            }

            Vector2 dir = toPlayer.normalized;
            Vector2 spawnPos = origin + dir * _bossProjectileSpawnOffset;
            GameObject bolt = Instantiate(_bossProjectilePrefab, spawnPos, Quaternion.identity);
            Projectile projectile = bolt.GetComponent<Projectile>();
            if (projectile != null)
            {
                projectile.SetHostileToPlayerOnly(true);
                projectile.SetBaseDamage(_bossProjectileDamage);
                projectile.SetBaseSpeed(_bossProjectileSpeed);
                projectile.Fire(dir);
            }

            _nextFireTime = Time.time + _fireInterval;
        }

        private void SpawnMiteWave()
        {
            if (_mitePrefab == null || _target == null)
            {
                return;
            }

            Sprite sprite = _spriteRenderer != null ? _spriteRenderer.sprite : null;
            int count = Mathf.Min(_maxMitesPerSpawn, _baseMiteCount + (_bossLevel - 1) * _extraMitesPerLevel);
            count = Mathf.Max(1, count);
            Vector3 bossPos = transform.position;

            for (int i = 0; i < count; i++)
            {
                float t = (i + 0.5f) / count;
                float ang = t * Mathf.PI * 2f + _aliveTime * 0.12f;
                Vector3 off = new Vector3(
                    Mathf.Cos(ang) * _miteSpawnRadius,
                    Mathf.Sin(ang) * _miteSpawnRadius * 0.62f,
                    0f);
                Vector3 pos = bossPos + off;
                GameObject go = Instantiate(_mitePrefab, pos, Quaternion.identity);
                BossMite mite = go.GetComponent<BossMite>();
                if (mite != null)
                {
                    mite.Initialize(
                        this,
                        _target,
                        sprite,
                        _miteVisualScale,
                        _miteMoveSpeed,
                        _miteMaxHealth,
                        _miteContactDamage,
                        _miteContactInterval,
                        _miteLifetimeSeconds);
                    RegisterMite(mite);
                }
            }
        }

        private void FixedUpdate()
        {
            if (!_active || _target == null)
            {
                return;
            }

            Vector2 pos = _rigidbody2D.position;
            Vector2 target = _target.position;
            Vector2 toTarget = target - pos;
            if (toTarget.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Vector2 toTargetNorm = toTarget.normalized;
            Vector2 tangent = new Vector2(-toTargetNorm.y, toTargetNorm.x);
            float orbitSign = Mathf.Sin(_aliveTime * 0.8f);
            Vector2 desiredDir = (toTargetNorm + tangent * _orbitAmount * orbitSign).normalized;
            float speedMul = PlayerPowerupController.GlobalEnemySpeedMultiplier;
            Vector2 next = pos + desiredDir * (_effectiveMoveSpeed * speedMul * Time.fixedDeltaTime);
            _rigidbody2D.MovePosition(next);

            float desiredAngle = Mathf.Atan2(desiredDir.y, desiredDir.x) * Mathf.Rad2Deg - 90f;
            float angle = Mathf.LerpAngle(transform.eulerAngles.z, desiredAngle, _turnSpeed * Time.fixedDeltaTime);
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        public void TakeDamage(float amount)
        {
            if (!_active || amount <= 0f)
            {
                return;
            }

            _currentHealth -= amount;
            if (_currentHealth > 0f)
            {
                return;
            }

            _currentHealth = 0f;
            _active = false;
            Defeated?.Invoke();
            gameObject.SetActive(false);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (!_active || other == null || !other.CompareTag("Player"))
            {
                return;
            }

            float now = Time.time;
            if (now < _nextContactTime)
            {
                return;
            }

            PlayerHealth health = other.GetComponent<PlayerHealth>();
            if (health == null)
            {
                health = other.GetComponentInParent<PlayerHealth>();
            }

            if (health == null)
            {
                return;
            }

            health.TakeDamage(_contactDamage);
            _nextContactTime = now + _contactInterval;
        }

        private Sprite ResolveFallbackBossSprite()
        {
            Sprite loaded = Resources.Load<Sprite>("Gameplay/Boss1");
            if (loaded != null)
            {
                return loaded;
            }

            return GetProceduralBossSprite();
        }

        private static Sprite GetProceduralBossSprite()
        {
            if (_proceduralBossSprite != null)
            {
                return _proceduralBossSprite;
            }

            const int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;

            Vector2 c = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float nx = (x - c.x) / c.x;
                    float ny = (y - c.y) / c.y;
                    float hull = Mathf.Abs(nx) * 0.75f + Mathf.Abs(ny) * 1.05f;
                    bool inside = hull <= 0.92f && ny > -0.7f;
                    if (!inside)
                    {
                        tex.SetPixel(x, y, new Color(0f, 0f, 0f, 0f));
                        continue;
                    }

                    float shade = 1f - Mathf.Clamp01(Mathf.Abs(nx) * 0.35f + Mathf.Max(0f, -ny) * 0.4f);
                    tex.SetPixel(x, y, new Color(0.82f * shade, 0.44f * shade, 0.32f * shade, 1f));
                }
            }

            tex.Apply();
            _proceduralBossSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            return _proceduralBossSprite;
        }
    }
}
