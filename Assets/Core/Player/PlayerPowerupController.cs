using UnityEngine;
using StaticDrift.Items;
using StaticDrift.Managers;
using StaticDrift.VFX;
namespace StaticDrift.Player
{
    public class PlayerPowerupController : MonoBehaviour
    {
        [Header("Durations")]
        [SerializeField] private float _shieldDuration = 7f;
        [SerializeField] private float _piercingDuration = 10f;
        [SerializeField] private float _overdriveDuration = 8f;
        [SerializeField] private float _timeWarpDuration = 7f;

        [Header("Effect Values")]
        [SerializeField] private int _piercingExtraHits = 8;
        [SerializeField] private float _overdriveFireIntervalMultiplier = 0.72f;
        [SerializeField] private float _overdriveProjectileSpeedMultiplier = 1.2f;
        [SerializeField] private float _timeWarpEnemySpeedMultiplier = 0.58f;
        [Tooltip("Instant heal when picking up a health pack. Cannot exceed max HP.")]
        [SerializeField] private float _healthPackHealAmount = 30f;

        private float _shieldUntil;
        private float _piercingUntil;
        private float _overdriveUntil;
        private float _timeWarpUntil;

        [Header("Ship outline & glow (active items)")]
        [SerializeField] private float _outlineScale = 1.08f;
        [SerializeField] private float _outlinePulseSpeed = 8.5f;
        [SerializeField] private float _outlineAlphaMin = 0.82f;
        [SerializeField] private float _outlineAlphaMax = 1f;
        [SerializeField] private float _outlineColorBrighten = 0.42f;
        [SerializeField] private float _itemGlowScale = 1.14f;
        [SerializeField] private float _itemGlowAlphaMin = 0.35f;
        [SerializeField] private float _itemGlowAlphaMax = 0.72f;
        [SerializeField] private float _itemGlowPulseScaleMul = 0.035f;

        private SpriteRenderer _bodySprite;
        private SpriteRenderer _outlineSprite;
        private SpriteRenderer _glowSprite;
        private Transform _outlineParent;
        private ParticleSystem _itemAuraParticles;
        private static PlayerPowerupController _activeInstance;

        public static float GlobalEnemySpeedMultiplier
        {
            get
            {
                if (_activeInstance == null)
                {
                    return 1f;
                }

                return _activeInstance.GetEnemySpeedMultiplier();
            }
        }

        public bool IsDamageImmune => Time.time < _shieldUntil;
        public bool HasPiercingLaserActive => Time.time < _piercingUntil;
        public int ExtraProjectilePierceHits => Time.time < _piercingUntil ? _piercingExtraHits : 0;
        public float FireIntervalMultiplier => Time.time < _overdriveUntil ? _overdriveFireIntervalMultiplier : 1f;
        public float ProjectileSpeedMultiplier => Time.time < _overdriveUntil ? _overdriveProjectileSpeedMultiplier : 1f;
        public bool HasTimeWarpActive => Time.time < _timeWarpUntil;

        private void Awake()
        {
            _activeInstance = this;
            EnsureOutline();
        }

        private void OnDestroy()
        {
            if (_activeInstance == this)
            {
                _activeInstance = null;
            }
        }

        public void ApplyItem(ItemType itemType)
        {
            float now = Time.time;
            if (itemType == ItemType.ContactShield)
            {
                _shieldUntil = Mathf.Max(_shieldUntil, now + _shieldDuration);
            }
            else if (itemType == ItemType.PiercingLaser)
            {
                _piercingUntil = Mathf.Max(_piercingUntil, now + _piercingDuration);
            }
            else if (itemType == ItemType.Overdrive)
            {
                _overdriveUntil = Mathf.Max(_overdriveUntil, now + _overdriveDuration);
            }
            else if (itemType == ItemType.TimeWarp)
            {
                _timeWarpUntil = Mathf.Max(_timeWarpUntil, now + _timeWarpDuration);
            }
            else if (itemType == ItemType.HealthPack)
            {
                PlayerHealth health = GetComponent<PlayerHealth>();
                if (health == null)
                {
                    health = GetComponentInParent<PlayerHealth>();
                }

                if (health != null)
                {
                    health.Heal(_healthPackHealAmount);
                }
            }

            AudioManager.EnsureExists().PlayUiConfirm();
        }

        private void LateUpdate()
        {
            UpdateOutlineVisual();
        }

        private void EnsureOutline()
        {
            _bodySprite = GetComponent<SpriteRenderer>();
            if (_bodySprite == null)
            {
                SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
                int count = renderers != null ? renderers.Length : 0;
                for (int i = 0; i < count; i++)
                {
                    SpriteRenderer sr = renderers[i];
                    if (sr == null
                        || sr.gameObject.name == "PowerupOutline"
                        || sr.gameObject.name == "PowerupItemGlow"
                        || sr.gameObject.name == "PowerupItemAura")
                    {
                        continue;
                    }

                    if (sr.enabled && sr.sprite != null)
                    {
                        _bodySprite = sr;
                        break;
                    }
                }
            }

            if (_bodySprite == null)
            {
                return;
            }

            _outlineParent = _bodySprite.transform;
            Transform existing = _outlineParent.Find("PowerupOutline");
            GameObject outlineGo;
            if (existing != null)
            {
                outlineGo = existing.gameObject;
            }
            else
            {
                outlineGo = new GameObject("PowerupOutline");
                outlineGo.transform.SetParent(_outlineParent, false);
                outlineGo.transform.SetAsFirstSibling();
            }

            outlineGo.transform.localPosition = Vector3.zero;
            outlineGo.transform.localRotation = Quaternion.identity;
            outlineGo.transform.localScale = Vector3.one * _outlineScale;

            _outlineSprite = outlineGo.GetComponent<SpriteRenderer>();
            if (_outlineSprite == null)
            {
                _outlineSprite = outlineGo.AddComponent<SpriteRenderer>();
            }

            _outlineSprite.sprite = _bodySprite.sprite;
            _outlineSprite.flipX = _bodySprite.flipX;
            _outlineSprite.flipY = _bodySprite.flipY;
            _outlineSprite.sortingLayerID = _bodySprite.sortingLayerID;
            _outlineSprite.sortingOrder = _bodySprite.sortingOrder + 2;
            _outlineSprite.enabled = false;

            EnsureItemGlowSprite();
            EnsureItemAuraParticles();
        }

        private void EnsureItemGlowSprite()
        {
            if (_bodySprite == null || _outlineParent == null)
            {
                return;
            }

            Transform existingGlow = _outlineParent.Find("PowerupItemGlow");
            GameObject glowGo;
            if (existingGlow != null)
            {
                glowGo = existingGlow.gameObject;
            }
            else
            {
                glowGo = new GameObject("PowerupItemGlow");
                glowGo.transform.SetParent(_outlineParent, false);
                glowGo.transform.SetAsFirstSibling();
            }

            glowGo.transform.localPosition = Vector3.zero;
            glowGo.transform.localRotation = Quaternion.identity;
            glowGo.transform.localScale = Vector3.one * _itemGlowScale;

            _glowSprite = glowGo.GetComponent<SpriteRenderer>();
            if (_glowSprite == null)
            {
                _glowSprite = glowGo.AddComponent<SpriteRenderer>();
            }

            _glowSprite.sprite = _bodySprite.sprite;
            _glowSprite.flipX = _bodySprite.flipX;
            _glowSprite.flipY = _bodySprite.flipY;
            _glowSprite.sortingLayerID = _bodySprite.sortingLayerID;
            _glowSprite.sortingOrder = _bodySprite.sortingOrder - 3;
            _glowSprite.enabled = false;
        }

        private void EnsureItemAuraParticles()
        {
            if (_outlineParent == null || _bodySprite == null)
            {
                return;
            }

            Transform existing = _outlineParent.Find("PowerupItemAura");
            GameObject auraGo;
            if (existing != null)
            {
                auraGo = existing.gameObject;
            }
            else
            {
                auraGo = new GameObject("PowerupItemAura");
                auraGo.transform.SetParent(_outlineParent, false);
                auraGo.transform.SetAsFirstSibling();
            }

            auraGo.transform.localPosition = Vector3.zero;
            auraGo.transform.localRotation = Quaternion.identity;
            auraGo.transform.localScale = Vector3.one;

            _itemAuraParticles = auraGo.GetComponent<ParticleSystem>();
            if (_itemAuraParticles == null)
            {
                _itemAuraParticles = auraGo.AddComponent<ParticleSystem>();
            }

            _itemAuraParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _itemAuraParticles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.85f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.38f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.22f);
            main.maxParticles = 32;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.gravityModifier = 0f;
            main.startColor = new ParticleSystem.MinMaxGradient(Color.white);

            var emission = _itemAuraParticles.emission;
            emission.rateOverTime = 8f;

            var shape = _itemAuraParticles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.3f;
            shape.arc = 360f;

            var vel = _itemAuraParticles.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.orbitalX = 0f;
            vel.orbitalY = 0f;
            vel.orbitalZ = 1.4f;

            var col = _itemAuraParticles.colorOverLifetime;
            col.enabled = true;
            col.color = new ParticleSystem.MinMaxGradient(
                new Color(1f, 1f, 1f, 0.95f),
                new Color(1f, 1f, 1f, 0f));

            ParticleSystemRenderer auraR = _itemAuraParticles.GetComponent<ParticleSystemRenderer>();
            auraR.sortingLayerID = _bodySprite.sortingLayerID;
            auraR.sortingOrder = _bodySprite.sortingOrder - 2;
            SharedVfxMaterials.ApplyUrpParticlesUnlit(auraR);

            auraR.renderMode = ParticleSystemRenderMode.Billboard;

            auraGo.SetActive(false);
        }

        private void UpdateOutlineVisual()
        {
            if (_outlineSprite == null || _bodySprite == null)
            {
                return;
            }

            _outlineSprite.sprite = _bodySprite.sprite;
            _outlineSprite.flipX = _bodySprite.flipX;
            _outlineSprite.flipY = _bodySprite.flipY;
            if (_glowSprite != null)
            {
                _glowSprite.sprite = _bodySprite.sprite;
                _glowSprite.flipX = _bodySprite.flipX;
                _glowSprite.flipY = _bodySprite.flipY;
            }

            Color blended;
            int count = 0;
            float r = 0f;
            float g = 0f;
            float b = 0f;
            if (IsDamageImmune)
            {
                Color c = ItemVisualColors.Get(ItemType.ContactShield);
                r += c.r; g += c.g; b += c.b;
                count++;
            }

            if (Time.time < _piercingUntil)
            {
                Color c = ItemVisualColors.Get(ItemType.PiercingLaser);
                r += c.r; g += c.g; b += c.b;
                count++;
            }

            if (Time.time < _overdriveUntil)
            {
                Color c = ItemVisualColors.Get(ItemType.Overdrive);
                r += c.r; g += c.g; b += c.b;
                count++;
            }

            if (HasTimeWarpActive)
            {
                Color c = ItemVisualColors.Get(ItemType.TimeWarp);
                r += c.r; g += c.g; b += c.b;
                count++;
            }

            if (count == 0)
            {
                _outlineSprite.enabled = false;
                if (_glowSprite != null)
                {
                    _glowSprite.enabled = false;
                }

                if (_itemAuraParticles != null)
                {
                    _itemAuraParticles.gameObject.SetActive(false);
                }

                return;
            }

            blended = new Color(r / count, g / count, b / count, 1f);
            Color punchy = Color.Lerp(blended, Color.white, Mathf.Clamp01(_outlineColorBrighten));
            float pulseFast = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * _outlinePulseSpeed);
            float pulseSlow = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * (_outlinePulseSpeed * 0.62f));

            Color outlineC = punchy;
            outlineC.a = Mathf.Lerp(_outlineAlphaMin, _outlineAlphaMax, pulseFast);
            _outlineSprite.color = outlineC;
            _outlineSprite.enabled = true;

            if (_glowSprite != null)
            {
                float glowPulse = Mathf.Lerp(_itemGlowAlphaMin, _itemGlowAlphaMax, pulseSlow);
                Color glowC = punchy;
                glowC.a = glowPulse;
                _glowSprite.color = glowC;
                float glowScale = _itemGlowScale * (1f + _itemGlowPulseScaleMul * (pulseFast - 0.5f) * 2f);
                _glowSprite.transform.localScale = Vector3.one * glowScale;
                _glowSprite.enabled = true;
            }

            if (_itemAuraParticles != null)
            {
                _itemAuraParticles.gameObject.SetActive(true);
                var main = _itemAuraParticles.main;
                Color a = punchy;
                a.a = 1f;
                Color bDim = a * 0.55f;
                bDim.a = 0.35f;
                main.startColor = new ParticleSystem.MinMaxGradient(a, bDim);
                if (!_itemAuraParticles.isPlaying)
                {
                    _itemAuraParticles.Play();
                }
            }
        }

        private float GetEnemySpeedMultiplier()
        {
            return Time.time < _timeWarpUntil ? _timeWarpEnemySpeedMultiplier : 1f;
        }
    }
}
