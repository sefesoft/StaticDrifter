using UnityEngine;
using StaticDrift.Enemies.Data;
using StaticDrift.Pooling;
using StaticDrift.Player;

namespace StaticDrift.Enemies
{
    /// <summary>
    /// Pooled enemy: follows player, deals contact damage, receives projectile damage from <see cref="Projectiles.Projectile"/>.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class Enemy : MonoBehaviour, IDamageable
    {
        private EnemyData _data;
        private ObjectPooler _pooler;
        private Transform _playerTarget;
        private float _currentHealth;
        private float _nextContactDamageTime;
        private Rigidbody2D _rigidbody2D;

        private void Awake()
        {
            _rigidbody2D = GetComponent<Rigidbody2D>();
            if (_rigidbody2D != null)
            {
                _rigidbody2D.bodyType = RigidbodyType2D.Kinematic;
                _rigidbody2D.gravityScale = 0f;
                _rigidbody2D.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                _rigidbody2D.constraints = RigidbodyConstraints2D.FreezeRotation;
            }
        }

        /// <summary>
        /// Call immediately after spawning from pool.
        /// </summary>
        public void Initialize(EnemyData data, ObjectPooler pooler, Transform playerTarget)
        {
            _data = data;
            _pooler = pooler;
            _playerTarget = playerTarget;

            if (_data == null)
            {
                _currentHealth = 1f;
            }
            else
            {
                _currentHealth = _data.MaxHealth;
            }

            _nextContactDamageTime = 0f;
        }

        private void Update()
        {
            if (_playerTarget == null)
            {
                return;
            }

            if (_data == null)
            {
                return;
            }

            Vector2 self = _rigidbody2D.position;
            Vector2 target = _playerTarget.position;
            Vector2 delta = target - self;
            float sqr = delta.sqrMagnitude;
            if (sqr < 0.0001f)
            {
                return;
            }

            delta.Normalize();
            float speedMultiplier = PlayerPowerupController.GlobalEnemySpeedMultiplier;
            Vector2 next = self + delta * (_data.MoveSpeed * speedMultiplier * Time.deltaTime);
            _rigidbody2D.MovePosition(next);
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
                Die();
            }
        }

        private void Die()
        {
            if (_pooler != null)
            {
                _pooler.Despawn(gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            if (_data == null)
            {
                return;
            }

            if (!other.CompareTag("Player"))
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

            float time = Time.time;
            if (time < _nextContactDamageTime)
            {
                return;
            }

            health.TakeDamage(_data.ContactDamage);
            _nextContactDamageTime = time + _data.ContactDamageInterval;
        }
    }
}
