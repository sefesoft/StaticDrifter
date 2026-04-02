using UnityEngine;
using StaticDrift.Player;
using System;

namespace StaticDrift.Enemies
{
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class BossShip : MonoBehaviour, IDamageable
    {
        [SerializeField] private float _moveSpeed = 1.8f;
        [SerializeField] private float _turnSpeed = 4.2f;
        [SerializeField] private float _orbitAmount = 1.7f;
        [SerializeField] private float _contactDamage = 12f;
        [SerializeField] private float _contactInterval = 0.45f;

        private Rigidbody2D _rigidbody2D;
        private CircleCollider2D _collider2D;
        private SpriteRenderer _spriteRenderer;
        private Transform _target;
        private float _maxHealth;
        private float _currentHealth;
        private float _nextContactTime;
        private float _aliveTime;
        private bool _active;
        private static Sprite _bossSprite;

        public float MaxHealth => _maxHealth;
        public float CurrentHealth => _currentHealth;
        public float Health01 => _maxHealth > 0f ? Mathf.Clamp01(_currentHealth / _maxHealth) : 0f;
        public bool IsActiveBoss => _active && gameObject.activeSelf;
        public event Action Defeated;

        private void Awake()
        {
            _rigidbody2D = GetComponent<Rigidbody2D>();
            _collider2D = GetComponent<CircleCollider2D>();
            _spriteRenderer = GetComponent<SpriteRenderer>();

            _rigidbody2D.gravityScale = 0f;
            _rigidbody2D.bodyType = RigidbodyType2D.Kinematic;
            _rigidbody2D.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _collider2D.isTrigger = true;
            _collider2D.radius = 0.9f;

            _spriteRenderer.sprite = GetBossSprite();
            _spriteRenderer.color = new Color(0.9f, 0.45f, 0.3f, 1f);
            _spriteRenderer.sortingOrder = 8;
            transform.localScale = new Vector3(2.6f, 2.6f, 1f);
            gameObject.SetActive(false);
        }

        public void Activate(Transform target, float health, Vector3 spawnPosition)
        {
            _target = target;
            _maxHealth = Mathf.Max(1f, health);
            _currentHealth = _maxHealth;
            _nextContactTime = 0f;
            _aliveTime = 0f;
            _active = true;

            transform.position = spawnPosition;
            gameObject.SetActive(true);
        }

        public void Deactivate()
        {
            _active = false;
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!_active)
            {
                return;
            }

            _aliveTime += Time.deltaTime;
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
            Vector2 next = pos + desiredDir * (_moveSpeed * speedMul * Time.fixedDeltaTime);
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

        private static Sprite GetBossSprite()
        {
            if (_bossSprite != null)
            {
                return _bossSprite;
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
            _bossSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            return _bossSprite;
        }
    }
}
