using UnityEngine;
using StaticDrift.Pooling;
using StaticDrift.Projectiles;

namespace StaticDrift.Player
{
    public class PlayerAutoFire : MonoBehaviour
    {
        [SerializeField] private ObjectPooler _pooler;
        [SerializeField] private GameObject _projectilePrefab;
        [SerializeField] private float _fireInterval = 0.5f;
        [SerializeField] private Transform _muzzleTransform;

        private float _timeSinceLastShot;
        private Collider2D _ownerCollider2D;

        private void Awake()
        {
            _ownerCollider2D = GetComponent<Collider2D>();

            if (_pooler == null)
            {
                _pooler = FindFirstObjectByType<ObjectPooler>();
            }
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            _timeSinceLastShot += deltaTime;

            if (_timeSinceLastShot >= _fireInterval)
            {
                TryFire();
            }
        }

        private void TryFire()
        {
            if (_pooler == null || _projectilePrefab == null)
            {
                return;
            }

            _timeSinceLastShot = 0f;

            Vector3 spawnPosition = _muzzleTransform != null ? _muzzleTransform.position : transform.position;
            Quaternion spawnRotation = transform.rotation;

            GameObject instance = _pooler.Spawn(_projectilePrefab, spawnPosition, spawnRotation);
            if (instance == null)
            {
                return;
            }

            Projectile projectile = instance.GetComponent<Projectile>();
            if (projectile != null)
            {
                projectile.Initialize(_pooler);
                projectile.Fire(transform.up);
            }

            Collider2D projectileCollider = instance.GetComponent<Collider2D>();
            if (_ownerCollider2D != null && projectileCollider != null)
            {
                Physics2D.IgnoreCollision(projectileCollider, _ownerCollider2D, true);
            }
        }
    }
}
