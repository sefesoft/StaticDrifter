using UnityEngine;
using StaticDrift.Pooling;
using StaticDrift.Projectiles;
using StaticDrift.Managers;

namespace StaticDrift.Player
{
    public class PlayerAutoFire : MonoBehaviour
    {
        private static readonly float[] _shotAngleScratch = new float[4];
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

            RunUpgradeController upgrades = RunUpgradeController.Instance;
            int pelletCount = upgrades != null ? Mathf.Clamp(upgrades.VolleyPelletCount, 1, 4) : 1;
            FillSpreadAngles(pelletCount, _shotAngleScratch);

            Vector3 spawnPosition = _muzzleTransform != null ? _muzzleTransform.position : transform.position;
            Vector2 forward = transform.up;

            for (int p = 0; p < pelletCount; p++)
            {
                float angleDeg = _shotAngleScratch[p];
                Vector2 dir = RotateVectorDegrees(forward, angleDeg);
                Quaternion rot = Quaternion.FromToRotation(Vector3.up, new Vector3(dir.x, dir.y, 0f));

                GameObject instance = _pooler.Spawn(_projectilePrefab, spawnPosition, rot);
                if (instance == null)
                {
                    continue;
                }

                Projectile projectile = instance.GetComponent<Projectile>();
                if (projectile != null)
                {
                    projectile.Initialize(_pooler);
                    if (upgrades != null)
                    {
                        float projectileSpeedMultiplier = upgrades.ProjectileSpeedMultiplier;
                        float lifetimeMult = upgrades.ProjectileLifetimeMultiplier;
                        int extraPierceHits = 0;
                        bool useLaserVisual = false;
                        if (TryGetComponent(out PlayerPowerupController powerups))
                        {
                            projectileSpeedMultiplier *= powerups.ProjectileSpeedMultiplier;
                            extraPierceHits = powerups.ExtraProjectilePierceHits;
                            useLaserVisual = powerups.HasPiercingLaserActive;
                        }

                        projectile.ApplyRuntimeModifiers(
                            upgrades.ProjectileDamageMultiplier,
                            projectileSpeedMultiplier,
                            upgrades.ProjectileSplashRadius,
                            upgrades.ProjectileSplashDamageMultiplier,
                            extraPierceHits,
                            useLaserVisual,
                            lifetimeMult);
                    }
                    else if (TryGetComponent(out PlayerPowerupController onlyPowerups))
                    {
                        projectile.ApplyRuntimeModifiers(
                            1f,
                            onlyPowerups.ProjectileSpeedMultiplier,
                            0f,
                            0.45f,
                            onlyPowerups.ExtraProjectilePierceHits,
                            onlyPowerups.HasPiercingLaserActive,
                            1f);
                    }
                    else
                    {
                        projectile.ApplyRuntimeModifiers(1f, 1f, 0f, 0.45f, 0, false, 1f);
                    }

                    projectile.Fire(dir);
                }

                Collider2D projectileCollider = instance.GetComponent<Collider2D>();
                if (_ownerCollider2D != null && projectileCollider != null)
                {
                    Physics2D.IgnoreCollision(projectileCollider, _ownerCollider2D, true);
                }
            }

            AudioManager.EnsureExists().PlayShoot();
        }

        private static void FillSpreadAngles(int count, float[] into)
        {
            if (count <= 1)
            {
                into[0] = 0f;
                return;
            }

            float half = count == 2 ? 11f : count == 3 ? 15f : 20f;
            if (count == 2)
            {
                into[0] = -half;
                into[1] = half;
                return;
            }

            if (count == 3)
            {
                into[0] = -half;
                into[1] = 0f;
                into[2] = half;
                return;
            }

            into[0] = -half;
            into[1] = -half * 0.33f;
            into[2] = half * 0.33f;
            into[3] = half;
        }

        private static Vector2 RotateVectorDegrees(Vector2 v, float degrees)
        {
            float rad = degrees * Mathf.Deg2Rad;
            float c = Mathf.Cos(rad);
            float s = Mathf.Sin(rad);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }
    }
}
