using UnityEngine;
using StaticDrift.Pooling;
using StaticDrift.Player;
using System;
using StaticDrift.Managers;

namespace StaticDrift.Enemies
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class Asteroid : MonoBehaviour, IDamageable
    {
        public static event Action<AsteroidSize, AsteroidKind> AsteroidDestroyed;

        public enum AsteroidSize
        {
            Large,
            Medium,
            Small
        }

        public enum AsteroidKind
        {
            Rock,
            Ice,
            Obsidian,
            Copper
        }

        private ObjectPooler _pooler;
        private EnemyWaveSpawner _spawner;
        private Rigidbody2D _rigidbody2D;
        private SpriteRenderer _spriteRenderer;
        private CircleCollider2D _circleCollider2D;
        private AsteroidSize _size;
        private AsteroidKind _kind;
        private float _health;
        private float _screenWrapMargin;
        private float _nextPlayerCollisionTime;
        private Vector2 _baseVelocity;
        private float _lastSpeedMultiplier = 1f;

        /// <summary>Multiplies incoming projectile damage so obsidian takes noticeably more hits (ship collision chip uses <see cref="ApplyDamage"/> with resist skipped).</summary>
        private const float ObsidianProjectileDamageTakenMultiplier = 0.52f;

        private void Awake()
        {
            _rigidbody2D = GetComponent<Rigidbody2D>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
            _circleCollider2D = GetComponent<CircleCollider2D>();
            if (_circleCollider2D == null)
            {
                _circleCollider2D = gameObject.AddComponent<CircleCollider2D>();
            }
            _circleCollider2D.isTrigger = true;

            BoxCollider2D box = GetComponent<BoxCollider2D>();
            if (box != null)
            {
                box.enabled = false;
            }

            _rigidbody2D.gravityScale = 0f;
            _rigidbody2D.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        public AsteroidKind Kind => _kind;

        public void Initialize(
            AsteroidSize size,
            AsteroidKind kind,
            float health,
            Sprite visualSprite,
            float screenWrapMargin,
            Vector2 velocity,
            float angularVelocity,
            Vector3 visualScale,
            ObjectPooler pooler,
            EnemyWaveSpawner spawner)
        {
            _size = size;
            _kind = kind;
            _health = health;
            _screenWrapMargin = screenWrapMargin;
            _pooler = pooler;
            _spawner = spawner;

            // Spawner often uses (x, y, z) with z = 1 while prefabs have "Constrain Proportions" on; Unity then
            // rejects or locks scale. Use X as the size and apply uniform XYZ so Inspector Small/Medium/Large Scale works.
            float uniform = Mathf.Max(0.01f, visualScale.x);
            transform.localScale = new Vector3(uniform, uniform, uniform);
            ApplyVisualSprite(visualSprite);
            MatchColliderToSprite();
            _baseVelocity = velocity;
            _lastSpeedMultiplier = PlayerPowerupController.GlobalEnemySpeedMultiplier;
            _rigidbody2D.linearVelocity = _baseVelocity * _lastSpeedMultiplier;
            _rigidbody2D.angularVelocity = angularVelocity;
            _nextPlayerCollisionTime = 0f;
        }

        private void Update()
        {
            float speedMultiplier = PlayerPowerupController.GlobalEnemySpeedMultiplier;
            if (Mathf.Abs(speedMultiplier - _lastSpeedMultiplier) > 0.01f)
            {
                _lastSpeedMultiplier = speedMultiplier;
                _rigidbody2D.linearVelocity = _baseVelocity * _lastSpeedMultiplier;
            }

            WrapAtScreenEdges();
        }

        public void TakeDamage(float amount)
        {
            ApplyDamage(amount, applyObsidianProjectileResist: true);
        }

        private void ApplyDamage(float amount, bool applyObsidianProjectileResist)
        {
            if (amount <= 0f)
            {
                return;
            }

            if (applyObsidianProjectileResist && _kind == AsteroidKind.Obsidian)
            {
                amount *= ObsidianProjectileDamageTakenMultiplier;
            }

            _health -= amount;
            if (_health > 0f)
            {
                AudioManager.EnsureExists().PlayAsteroidHit();
                return;
            }

            if (_spawner != null)
            {
                _spawner.HandleAsteroidDestroyed(_size, transform.position, _rigidbody2D.linearVelocity, _kind);
            }

            AsteroidDestroyed?.Invoke(_size, _kind);
            AudioManager.EnsureExists().PlayAsteroidBreak();

            Despawn();
        }

        private void Despawn()
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

        private void WrapAtScreenEdges()
        {
            Camera cam = _spawner != null ? _spawner.ActiveCamera : Camera.main;
            if (cam == null || !cam.orthographic)
            {
                return;
            }

            Vector3 camPos = cam.transform.position;
            float halfHeight = cam.orthographicSize;
            float halfWidth = halfHeight * cam.aspect;

            float left = camPos.x - halfWidth;
            float right = camPos.x + halfWidth;
            float bottom = camPos.y - halfHeight;
            float top = camPos.y + halfHeight;

            Vector2 pos = _rigidbody2D.position;
            bool wrapped = false;

            if (pos.x < left - _screenWrapMargin)
            {
                pos.x = right + _screenWrapMargin;
                wrapped = true;
            }
            else if (pos.x > right + _screenWrapMargin)
            {
                pos.x = left - _screenWrapMargin;
                wrapped = true;
            }

            if (pos.y < bottom - _screenWrapMargin)
            {
                pos.y = top + _screenWrapMargin;
                wrapped = true;
            }
            else if (pos.y > top + _screenWrapMargin)
            {
                pos.y = bottom - _screenWrapMargin;
                wrapped = true;
            }

            if (wrapped)
            {
                _rigidbody2D.position = pos;
            }
        }

        private void ApplyVisualSprite(Sprite sprite)
        {
            if (_spriteRenderer == null || sprite == null)
            {
                return;
            }

            _spriteRenderer.sprite = sprite;
        }

        private void MatchColliderToSprite()
        {
            if (_circleCollider2D == null || _spriteRenderer == null || _spriteRenderer.sprite == null)
            {
                return;
            }

            Bounds bounds = _spriteRenderer.sprite.bounds;
            float radius = Mathf.Min(bounds.extents.x, bounds.extents.y) * 0.95f;
            _circleCollider2D.offset = bounds.center;
            _circleCollider2D.radius = Mathf.Max(0.01f, radius);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            HandlePlayerCollision(other);
        }

        private void OnTriggerStay2D(Collider2D other)
        {
            HandlePlayerCollision(other);
        }

        private void HandlePlayerCollision(Collider2D other)
        {
            if (other == null || !other.CompareTag("Player"))
            {
                return;
            }

            float now = Time.time;
            if (now < _nextPlayerCollisionTime)
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

            Rigidbody2D playerBody = other.attachedRigidbody;
            if (playerBody == null)
            {
                playerBody = other.GetComponentInParent<Rigidbody2D>();
            }

            if (playerBody != null)
            {
                Vector2 pushDir = playerBody.position - _rigidbody2D.position;
                if (pushDir.sqrMagnitude < 0.0001f)
                {
                    pushDir = UnityEngine.Random.insideUnitCircle.normalized;
                }
                else
                {
                    pushDir.Normalize();
                }

                playerBody.AddForce(pushDir * GetPushImpulse(), ForceMode2D.Impulse);
            }

            float dmg = GetCollisionDamage();
            health.TakeDamage(dmg);

            if (_kind == AsteroidKind.Ice)
            {
                ApplyIceFreeze(health);
            }

            _nextPlayerCollisionTime = now + 0.2f;

            // Collision with the ship also counts as an asteroid hit (no obsidian projectile resist on this chip).
            ApplyDamage(1f, applyObsidianProjectileResist: false);
        }

        private void ApplyIceFreeze(PlayerHealth health)
        {
            if (health == null)
            {
                return;
            }

            GameObject root = health.gameObject;
            PlayerFreezeController freeze = root.GetComponent<PlayerFreezeController>();
            if (freeze == null)
            {
                freeze = root.AddComponent<PlayerFreezeController>();
            }

            freeze.ApplyFreeze();
        }

        private float GetCollisionDamage()
        {
            float baseDamage;
            if (_size == AsteroidSize.Large)
            {
                baseDamage = 5f;
            }
            else if (_size == AsteroidSize.Medium)
            {
                baseDamage = 3f;
            }
            else
            {
                baseDamage = 1f;
            }

            if (_kind == AsteroidKind.Obsidian)
            {
                return baseDamage * 1.55f;
            }

            return baseDamage;
        }

        private float GetPushImpulse()
        {
            if (_size == AsteroidSize.Large)
            {
                return 2.6f;
            }

            if (_size == AsteroidSize.Medium)
            {
                return 1.9f;
            }

            return 1.2f;
        }

    }
}
