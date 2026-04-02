using UnityEngine;
using StaticDrift.Pooling;
using StaticDrift.Projectiles;
using StaticDrift.Managers;

namespace StaticDrift.Player
{
    public class PlayerAutoFire : MonoBehaviour
    {
        [SerializeField] private ObjectPooler _pooler;
        [SerializeField] private GameObject _projectilePrefab;
        [SerializeField] private float _fireInterval = 0.6f;
        [SerializeField] private Transform _muzzleTransform;

        private float _timeSinceLastShot;
        private Collider2D _ownerCollider2D;
        private float _baseFireInterval;

        private void Awake()
        {
            _ownerCollider2D = GetComponent<Collider2D>();
            _baseFireInterval = Mathf.Max(0.05f, _fireInterval);

            if (_pooler == null)
            {
                _pooler = FindFirstObjectByType<ObjectPooler>();
            }
        }

        private void Update()
        {
            float deltaTime = Time.deltaTime;
            _timeSinceLastShot += deltaTime;
            float effectiveInterval = GetEffectiveFireInterval();

            if (_timeSinceLastShot >= effectiveInterval)
            {
                TryFire();
            }
        }

        private float GetEffectiveFireInterval()
        {
            RunUpgradeController upgrades = RunUpgradeController.Instance;
            PlayerPowerupController powerups = GetComponent<PlayerPowerupController>();
            if (upgrades == null)
            {
                if (powerups == null)
                {
                    return _baseFireInterval;
                }

                return Mathf.Max(0.05f, _baseFireInterval * powerups.FireIntervalMultiplier);
            }

            float interval = _baseFireInterval * upgrades.FireIntervalMultiplier;
            if (powerups != null)
            {
                interval *= powerups.FireIntervalMultiplier;
            }

            return Mathf.Max(0.05f, interval);
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
                RunUpgradeController upgrades = RunUpgradeController.Instance;
                if (upgrades != null)
                {
                    float projectileSpeedMultiplier = upgrades.ProjectileSpeedMultiplier;
                    int extraPierceHits = 0;
                    if (TryGetComponent(out PlayerPowerupController powerups))
                    {
                        projectileSpeedMultiplier *= powerups.ProjectileSpeedMultiplier;
                        extraPierceHits = powerups.ExtraProjectilePierceHits;
                    }

                    projectile.ApplyRuntimeModifiers(
                        upgrades.ProjectileDamageMultiplier,
                        projectileSpeedMultiplier,
                        upgrades.ProjectileSplashRadius,
                        upgrades.ProjectileSplashDamageMultiplier,
                        extraPierceHits);
                }
                else if (TryGetComponent(out PlayerPowerupController onlyPowerups))
                {
                    projectile.ApplyRuntimeModifiers(1f, onlyPowerups.ProjectileSpeedMultiplier, 0f, 0.45f, onlyPowerups.ExtraProjectilePierceHits);
                }
                projectile.Fire(transform.up);
            }

            AudioManager.EnsureExists().PlayShoot();

            Collider2D projectileCollider = instance.GetComponent<Collider2D>();
            if (_ownerCollider2D != null && projectileCollider != null)
            {
                Physics2D.IgnoreCollision(projectileCollider, _ownerCollider2D, true);
            }
        }
    }
}
