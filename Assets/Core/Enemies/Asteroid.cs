using UnityEngine;
using StaticDrift.Pooling;
using System.Collections.Generic;
using StaticDrift.Player;
using System;

namespace StaticDrift.Enemies
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class Asteroid : MonoBehaviour, IDamageable
    {
        public static event Action<AsteroidSize> AsteroidDestroyed;

        public enum AsteroidSize
        {
            Large,
            Medium,
            Small
        }

        private ObjectPooler _pooler;
        private EnemyWaveSpawner _spawner;
        private Rigidbody2D _rigidbody2D;
        private SpriteRenderer _spriteRenderer;
        private CircleCollider2D _circleCollider2D;
        private AsteroidSize _size;
        private float _health;
        private float _screenWrapMargin;
        private float _nextPlayerCollisionTime;
        private static readonly Dictionary<AsteroidSize, Sprite> _spriteBySize = new Dictionary<AsteroidSize, Sprite>(3);

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

        public void Initialize(
            AsteroidSize size,
            float health,
            float screenWrapMargin,
            Vector2 velocity,
            float angularVelocity,
            Vector3 visualScale,
            ObjectPooler pooler,
            EnemyWaveSpawner spawner)
        {
            _size = size;
            _health = health;
            _screenWrapMargin = screenWrapMargin;
            _pooler = pooler;
            _spawner = spawner;

            transform.localScale = visualScale;
            ApplySizeVisual(size);
            MatchColliderToSprite();
            _rigidbody2D.linearVelocity = velocity;
            _rigidbody2D.angularVelocity = angularVelocity;
            _nextPlayerCollisionTime = 0f;
        }

        private void Update()
        {
            WrapAtScreenEdges();
        }

        public void TakeDamage(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            _health -= amount;
            if (_health > 0f)
            {
                return;
            }

            if (_spawner != null)
            {
                _spawner.HandleAsteroidDestroyed(_size, transform.position, _rigidbody2D.linearVelocity);
            }

            AsteroidDestroyed?.Invoke(_size);

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

        private void ApplySizeVisual(AsteroidSize size)
        {
            if (_spriteRenderer == null)
            {
                return;
            }

            Sprite sprite;
            if (!_spriteBySize.TryGetValue(size, out sprite) || sprite == null)
            {
                sprite = CreateAsteroidSprite(size);
                _spriteBySize[size] = sprite;
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

            health.TakeDamage(GetCollisionDamage());
            _nextPlayerCollisionTime = now + 0.2f;

            // Collision with the ship also counts as an asteroid hit.
            TakeDamage(1f);
        }

        private float GetCollisionDamage()
        {
            if (_size == AsteroidSize.Large)
            {
                return 5f;
            }

            if (_size == AsteroidSize.Medium)
            {
                return 3f;
            }

            return 1f;
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

        private static Sprite CreateAsteroidSprite(AsteroidSize size)
        {
            int textureSize = 128;
            int radius = size == AsteroidSize.Large ? 56 : (size == AsteroidSize.Medium ? 49 : 42);
            float noise = size == AsteroidSize.Large ? 0.10f : (size == AsteroidSize.Medium ? 0.13f : 0.16f);
            Color color = size == AsteroidSize.Large
                ? new Color(0.70f, 0.72f, 0.74f, 1f)
                : (size == AsteroidSize.Medium
                    ? new Color(0.68f, 0.69f, 0.72f, 1f)
                    : new Color(0.62f, 0.64f, 0.67f, 1f));

            Texture2D tex = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Vector2 center = new Vector2((textureSize - 1) * 0.5f, (textureSize - 1) * 0.5f);
            float invRadius = 1f / radius;

            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    float dx = x - center.x;
                    float dy = y - center.y;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float normalized = dist * invRadius;

                    float angle = Mathf.Atan2(dy, dx);
                    float shapeNoise =
                        Mathf.Sin(angle * 4.1f) * noise +
                        Mathf.Sin(angle * 7.6f + 0.6f) * (noise * 0.35f);
                    float edge = 1f + shapeNoise;

                    if (normalized <= edge)
                    {
                        float shade = 1f - Mathf.Clamp01(normalized) * 0.25f;
                        float alpha = 1f;
                        float edgeFadeStart = edge - 0.04f;
                        if (normalized > edgeFadeStart)
                        {
                            alpha = Mathf.Clamp01((edge - normalized) / (edge - edgeFadeStart));
                        }

                        Color px = color * shade;
                        px.a = alpha;
                        tex.SetPixel(x, y, px);
                    }
                    else
                    {
                        tex.SetPixel(x, y, new Color(0f, 0f, 0f, 0f));
                    }
                }
            }

            tex.Apply();
            return Sprite.Create(
                tex,
                new Rect(0f, 0f, textureSize, textureSize),
                new Vector2(0.5f, 0.5f),
                100f);
        }
    }
}
