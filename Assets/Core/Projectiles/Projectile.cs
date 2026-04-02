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

        private ObjectPooler _pooler;
        private float _age;
        private Vector2 _direction;
        private Collider2D _collider2D;
        private float _castRadius;
        private float _damageMultiplier = 1f;
        private float _speedMultiplier = 1f;
        private float _splashRadius;
        private float _splashDamageMultiplier = 0.45f;
        private int _remainingPierceHits;
        private readonly List<Collider2D> _piercedTargets = new List<Collider2D>(12);
        private static readonly Collider2D[] _splashHits = new Collider2D[24];

        private void Awake()
        {
            _collider2D = GetComponent<Collider2D>();
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
            int extraPierceHits)
        {
            _damageMultiplier = Mathf.Max(0.1f, damageMultiplier);
            _speedMultiplier = Mathf.Max(0.1f, speedMultiplier);
            _splashRadius = Mathf.Max(0f, splashRadius);
            _splashDamageMultiplier = Mathf.Clamp(splashDamageMultiplier, 0f, 1f);
            _remainingPierceHits = Mathf.Max(0, extraPierceHits);
        }

        private void OnEnable()
        {
            _age = 0f;
            _damageMultiplier = 1f;
            _speedMultiplier = 1f;
            _splashRadius = 0f;
            _splashDamageMultiplier = 0.45f;
            _remainingPierceHits = 0;
            _piercedTargets.Clear();
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
            if (_age >= _lifetime)
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
}
