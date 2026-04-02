using UnityEngine;
using StaticDrift.Enemies;
using StaticDrift.Pooling;

namespace StaticDrift.Projectiles
{
    public class Projectile : MonoBehaviour
    {
        [SerializeField] private float _speed = 15f;
        [SerializeField] private float _lifetime = 2f;
        [SerializeField] private float _damage = 10f;
        [SerializeField] private float _fallbackHitRadius = 0.08f;

        private ObjectPooler _pooler;
        private float _age;
        private Vector2 _direction;
        private Collider2D _collider2D;
        private float _castRadius;

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

        private void OnEnable()
        {
            _age = 0f;
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            float moveDistance = _speed * deltaTime;

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

            damageable.TakeDamage(_damage);
            Despawn();
            return true;
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
