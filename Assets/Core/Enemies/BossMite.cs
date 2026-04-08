using UnityEngine;
using StaticDrift.Player;
using StaticDrift.VFX;

namespace StaticDrift.Enemies
{
    /// <summary>
    /// Small spawn from <see cref="BossShip"/> swarm style: chases the player, contact damage, low HP, limited lifetime.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(CircleCollider2D))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class BossMite : MonoBehaviour, IDamageable
    {
        private BossShip _owner;
        private Transform _playerTarget;
        private Rigidbody2D _rigidbody2D;
        private float _moveSpeed;
        private float _maxHealth;
        private float _currentHealth;
        private float _contactDamage;
        private float _contactInterval;
        private float _expireTime;
        private float _nextContactTime;

        public void Initialize(
            BossShip owner,
            Transform playerTarget,
            Sprite sprite,
            float uniformScale,
            float moveSpeed,
            float maxHealth,
            float contactDamage,
            float contactInterval,
            float lifetimeSeconds)
        {
            _owner = owner;
            _playerTarget = playerTarget;
            _moveSpeed = moveSpeed;
            _maxHealth = Mathf.Max(0.5f, maxHealth);
            _currentHealth = _maxHealth;
            _contactDamage = contactDamage;
            _contactInterval = Mathf.Max(0.08f, contactInterval);
            _expireTime = Time.time + Mathf.Max(0.5f, lifetimeSeconds);
            _nextContactTime = 0f;

            if (_rigidbody2D == null)
            {
                _rigidbody2D = GetComponent<Rigidbody2D>();
            }

            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sprite != null)
            {
                sr.sprite = sprite;
            }

            sr.sortingOrder = 7;
            float s = Mathf.Max(0.05f, uniformScale);
            transform.localScale = Vector3.one * s;

            CircleCollider2D circle = GetComponent<CircleCollider2D>();
            if (circle != null && sr.sprite != null)
            {
                Bounds b = sr.sprite.bounds;
                float halfExtent = Mathf.Max(b.extents.x, b.extents.y);
                circle.radius = Mathf.Clamp(halfExtent * 0.5f, 0.1f, 4f);
            }
        }

        private void Awake()
        {
            _rigidbody2D = GetComponent<Rigidbody2D>();
            _rigidbody2D.gravityScale = 0f;
            _rigidbody2D.bodyType = RigidbodyType2D.Kinematic;
            _rigidbody2D.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _rigidbody2D.constraints = RigidbodyConstraints2D.FreezeRotation;

            CircleCollider2D circle = GetComponent<CircleCollider2D>();
            if (circle != null)
            {
                circle.isTrigger = true;
            }
        }

        private void Update()
        {
            if (Time.time >= _expireTime)
            {
                Expire();
                return;
            }

            if (_playerTarget == null)
            {
                Expire();
                return;
            }

            Vector2 self = _rigidbody2D.position;
            Vector2 target = _playerTarget.position;
            Vector2 delta = target - self;
            if (delta.sqrMagnitude < 0.0001f)
            {
                return;
            }

            delta.Normalize();
            float speedMul = PlayerPowerupController.GlobalEnemySpeedMultiplier;
            Vector2 next = self + delta * (_moveSpeed * speedMul * Time.deltaTime);
            _rigidbody2D.MovePosition(next);

            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);
        }

        public void TakeDamage(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            _currentHealth -= amount;
            if (_currentHealth <= 0f)
            {
                Destroy(gameObject);
            }
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (other == null || !other.CompareTag("Player"))
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

        private void Expire()
        {
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (_owner != null)
            {
                _owner.UnregisterMite(this);
            }

            if (!Application.isPlaying)
            {
                return;
            }

            SmallOrangeExplosion.Spawn(transform.position, 0.42f);
        }
    }
}
