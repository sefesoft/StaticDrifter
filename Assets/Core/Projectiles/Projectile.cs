using UnityEngine;
using StaticDrift.Enemies;
using StaticDrift.Pooling;
using System.Collections.Generic;

namespace StaticDrift.Projectiles
{
    public class Projectile : MonoBehaviour
    {
        [SerializeField] private float _speed = 15f;
        [SerializeField] private float _lifetime = 2f;
        [SerializeField] private float _damage = 10f;
        [SerializeField] private float _fallbackHitRadius = 0.08f;
        [SerializeField] private LayerMask _damageLayers = ~0;
        [Header("Laser Visual")]
        [SerializeField] private Color _laserColor = new Color(0.35f, 0.78f, 1f, 0.95f);
        [SerializeField] private float _laserTrailTime = 0.18f;
        [SerializeField] private float _laserTrailWidth = 0.22f;
        [Header("Splash Visual")]
        [SerializeField] private Color _splashVfxColor = new Color(1f, 0.62f, 0.24f, 0.8f);
        [SerializeField] private float _splashVfxDuration = 0.18f;

        private ObjectPooler _pooler;
        private float _age;
        private Vector2 _direction;
        private Collider2D _collider2D;
        private float _castRadius;
        private float _damageMultiplier = 1f;
        private float _speedMultiplier = 1f;
        private float _lifetimeMultiplier = 1f;
        private float _splashRadius;
        private float _splashDamageMultiplier = 0.45f;
        private int _remainingPierceHits;
        private bool _useLaserVisual;
        private SpriteRenderer _spriteRenderer;
        private TrailRenderer _trailRenderer;
        private Color _defaultSpriteColor = Color.white;
        private readonly List<Collider2D> _piercedTargets = new List<Collider2D>(12);
        private static readonly Collider2D[] _splashHits = new Collider2D[24];
        private static Material _laserTrailMaterial;

        private void Awake()
        {
            _collider2D = GetComponent<Collider2D>();
            _spriteRenderer = GetComponent<SpriteRenderer>();
            if (_spriteRenderer != null)
            {
                _defaultSpriteColor = _spriteRenderer.color;
            }

            EnsureTrailRenderer();
            _castRadius = CalculateCastRadius();
        }

        public void Initialize(ObjectPooler pooler)
        {
            _pooler = pooler;
        }

        public void Fire(Vector2 direction)
        {
            _direction = direction.normalized;
            _age = 0f;
        }

        public void ApplyRuntimeModifiers(
            float damageMultiplier,
            float speedMultiplier,
            float splashRadius,
            float splashDamageMultiplier,
            int extraPierceHits,
            bool useLaserVisual,
            float lifetimeMultiplier = 1f)
        {
            _damageMultiplier = Mathf.Max(0.1f, damageMultiplier);
            _speedMultiplier = Mathf.Max(0.1f, speedMultiplier);
            _splashRadius = Mathf.Max(0f, splashRadius);
            _splashDamageMultiplier = Mathf.Clamp(splashDamageMultiplier, 0f, 1f);
            _remainingPierceHits = Mathf.Max(0, extraPierceHits);
            _useLaserVisual = useLaserVisual;
            _lifetimeMultiplier = Mathf.Clamp(lifetimeMultiplier, 0.15f, 2f);
            ApplyVisualState();
        }

        private void OnEnable()
        {
            _age = 0f;
            _damageMultiplier = 1f;
            _speedMultiplier = 1f;
            _lifetimeMultiplier = 1f;
            _splashRadius = 0f;
            _splashDamageMultiplier = 0.45f;
            _remainingPierceHits = 0;
            _useLaserVisual = false;
            _piercedTargets.Clear();
            ApplyVisualState();
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            float moveDistance = (_speed * _speedMultiplier) * deltaTime;

            if (TryHitInPath(moveDistance))
            {
                return;
            }

            Vector3 position = transform.position;
            position += (Vector3)(_direction * moveDistance);
            transform.position = position;

            _age += deltaTime;
            if (_age >= _lifetime * _lifetimeMultiplier)
            {
                Despawn();
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            TryApplyDamage(other);
        }

        private bool TryHitInPath(float distance)
        {
            if (distance <= 0f)
            {
                return false;
            }

            RaycastHit2D hit = Physics2D.CircleCast(transform.position, _castRadius, _direction, distance);
            if (hit.collider == null)
            {
                return false;
            }

            if (hit.collider == _collider2D)
            {
                return false;
            }

            if (!TryApplyDamage(hit.collider))
            {
                return false;
            }

            transform.position = hit.point;
            return true;
        }

        private bool TryApplyDamage(Collider2D other)
        {
            IDamageable damageable = other.GetComponent<IDamageable>();
            if (damageable == null)
            {
                damageable = other.GetComponentInParent<IDamageable>();
            }

            if (damageable == null)
            {
                return false;
            }

            if (_remainingPierceHits > 0 && _piercedTargets.Contains(other))
            {
                return false;
            }

            float appliedDamage = _damage * _damageMultiplier;
            damageable.TakeDamage(appliedDamage);
            ApplySplashDamage(other, appliedDamage);
            if (_remainingPierceHits > 0)
            {
                _remainingPierceHits--;
                _piercedTargets.Add(other);
                return true;
            }

            Despawn();
            return true;
        }

        private void ApplySplashDamage(Collider2D directHit, float directDamage)
        {
            if (_splashRadius <= 0.001f)
            {
                return;
            }

            SpawnSplashVisual();

            int hitCount = Physics2D.OverlapCircleNonAlloc(
                transform.position,
                _splashRadius,
                _splashHits,
                _damageLayers);

            float splashDamage = directDamage * _splashDamageMultiplier;
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D hit = _splashHits[i];
                if (hit == null || hit == directHit || hit == _collider2D)
                {
                    continue;
                }

                IDamageable splashTarget = hit.GetComponent<IDamageable>();
                if (splashTarget == null)
                {
                    splashTarget = hit.GetComponentInParent<IDamageable>();
                }

                if (splashTarget != null)
                {
                    splashTarget.TakeDamage(splashDamage);
                }
            }
        }

        private float CalculateCastRadius()
        {
            CircleCollider2D circle = GetComponent<CircleCollider2D>();
            if (circle != null)
            {
                float scale = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y);
                return Mathf.Max(0.01f, circle.radius * scale);
            }

            if (_collider2D != null)
            {
                Vector2 extents = _collider2D.bounds.extents;
                return Mathf.Max(0.01f, Mathf.Min(extents.x, extents.y));
            }

            return _fallbackHitRadius;
        }

        private void SpawnSplashVisual()
        {
            int sortingOrder = _spriteRenderer != null ? _spriteRenderer.sortingOrder + 1 : 1;
            ProjectileSplashRing.Spawn(transform.position, _splashRadius, _splashVfxColor, _splashVfxDuration, sortingOrder);
        }

        private void EnsureTrailRenderer()
        {
            _trailRenderer = GetComponent<TrailRenderer>();
            if (_trailRenderer == null)
            {
                _trailRenderer = gameObject.AddComponent<TrailRenderer>();
            }

            _trailRenderer.enabled = false;
            _trailRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _trailRenderer.receiveShadows = false;
            _trailRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            _trailRenderer.time = _laserTrailTime;
            _trailRenderer.startWidth = _laserTrailWidth;
            _trailRenderer.endWidth = 0.03f;
            _trailRenderer.minVertexDistance = 0.02f;
            _trailRenderer.alignment = LineAlignment.TransformZ;
            _trailRenderer.textureMode = LineTextureMode.Stretch;
            _trailRenderer.numCapVertices = 2;
            _trailRenderer.numCornerVertices = 2;
            _trailRenderer.sortingLayerID = _spriteRenderer != null ? _spriteRenderer.sortingLayerID : 0;
            _trailRenderer.sortingOrder = (_spriteRenderer != null ? _spriteRenderer.sortingOrder : 0) - 1;
            _trailRenderer.material = GetLaserTrailMaterial();
            _trailRenderer.colorGradient = BuildLaserGradient();
            _trailRenderer.Clear();
        }

        private void ApplyVisualState()
        {
            if (_spriteRenderer != null)
            {
                _spriteRenderer.color = _useLaserVisual ? _laserColor : _defaultSpriteColor;
            }

            if (_trailRenderer == null)
            {
                return;
            }

            _trailRenderer.time = _useLaserVisual ? _laserTrailTime : 0f;
            _trailRenderer.startWidth = _useLaserVisual ? _laserTrailWidth : 0f;
            _trailRenderer.endWidth = _useLaserVisual ? 0.03f : 0f;
            _trailRenderer.enabled = _useLaserVisual;
            _trailRenderer.Clear();
        }

        private Gradient BuildLaserGradient()
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0.72f, 0.94f, 1f), 0f),
                    new GradientColorKey(_laserColor, 0.35f),
                    new GradientColorKey(new Color(0.08f, 0.28f, 0.95f), 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.95f, 0f),
                    new GradientAlphaKey(0.75f, 0.45f),
                    new GradientAlphaKey(0f, 1f)
                });
            return gradient;
        }

        private static Material GetLaserTrailMaterial()
        {
            if (_laserTrailMaterial != null)
            {
                return _laserTrailMaterial;
            }

            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                _laserTrailMaterial = new Material(shader);
            }

            return _laserTrailMaterial;
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
    }

    public class ProjectileSplashRing : MonoBehaviour
    {
        private SpriteRenderer _spriteRenderer;
        private float _duration;
        private float _elapsed;
        private float _startScale;
        private float _endScale;
        private Color _baseColor;

        private static Sprite _ringSprite;

        public static void Spawn(Vector3 position, float radius, Color color, float duration, int sortingOrder)
        {
            if (radius <= 0.001f)
            {
                return;
            }

            GameObject go = new GameObject("ProjectileSplashRing");
            go.transform.position = new Vector3(position.x, position.y, 0f);
            ProjectileSplashRing ring = go.AddComponent<ProjectileSplashRing>();
            ring.Initialize(radius, color, duration, sortingOrder);
        }

        private void Initialize(float radius, Color color, float duration, int sortingOrder)
        {
            _spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            _spriteRenderer.sprite = GetRingSprite();
            _spriteRenderer.color = color;
            _spriteRenderer.sortingOrder = sortingOrder;

            _duration = Mathf.Max(0.05f, duration);
            _baseColor = color;
            _startScale = Mathf.Max(0.1f, radius * 1.35f);
            _endScale = Mathf.Max(_startScale, radius * 2.25f);
            transform.localScale = Vector3.one * _startScale;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _duration);
            float eased = 1f - Mathf.Pow(1f - t, 2f);
            float scale = Mathf.Lerp(_startScale, _endScale, eased);
            transform.localScale = Vector3.one * scale;

            if (_spriteRenderer != null)
            {
                Color color = _baseColor;
                color.a *= 1f - t;
                _spriteRenderer.color = color;
            }

            if (_elapsed >= _duration)
            {
                Destroy(gameObject);
            }
        }

        private static Sprite GetRingSprite()
        {
            if (_ringSprite != null)
            {
                return _ringSprite;
            }

            const int size = 64;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;

            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float outerRadius = size * 0.45f;
            float innerRadius = size * 0.33f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center.x;
                    float dy = y - center.y;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = dist <= outerRadius && dist >= innerRadius ? 1f : 0f;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            _ringSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            return _ringSprite;
        }
    }
}
