using UnityEngine;
using StaticDrift.Items;
using StaticDrift.Managers;

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

        [Header("Ship outline (active items)")]
        [SerializeField] private float _outlineScale = 1.28f;
        [SerializeField] private float _outlinePulseSpeed = 7.2f;

        private SpriteRenderer _bodySprite;
        private SpriteRenderer _outlineSprite;
        private Transform _outlineParent;

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
                    if (sr == null || sr.gameObject.name == "PowerupOutline")
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
            _outlineSprite.sortingOrder = _bodySprite.sortingOrder - 1;
            _outlineSprite.enabled = false;
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
                return;
            }

            blended = new Color(r / count, g / count, b / count, 1f);
            float pulse = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * _outlinePulseSpeed);
            blended.a = Mathf.Clamp01(pulse * 1.05f);
            _outlineSprite.color = blended;
            _outlineSprite.enabled = true;
        }

        private float GetEnemySpeedMultiplier()
        {
            return Time.time < _timeWarpUntil ? _timeWarpEnemySpeedMultiplier : 1f;
        }
    }
}
