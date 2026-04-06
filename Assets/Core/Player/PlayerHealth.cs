using UnityEngine;
using StaticDrift.Managers;

namespace StaticDrift.Player
{
    /// <summary>
    /// Receives damage from enemies. Tag the GameObject "Player".
    /// </summary>
    public class PlayerHealth : MonoBehaviour
    {
        /// <summary>Fires once per damage application that passes immunity checks (including hits absorbed by bonus lives).</summary>
        public static event System.Action PlayerTookDamage;

        [SerializeField] private float _maxHealth = 100f;
        [SerializeField] private float _invulnerabilityDuration = 0.5f;
        [Tooltip("HP fraction when a bonus life is consumed (0.5 = half max HP).")]
        [SerializeField] [Range(0.1f, 1f)] private float _bonusLifeRestoreHealthFraction = 0.5f;

        private float _currentHealth;
        private float _invulnerableUntil;
        private float _incomingDamageMultiplier = 1f;
        private int _bonusLives;

        public float CurrentHealth => _currentHealth;
        public float MaxHealth => _maxHealth;
        public bool IsDead => _currentHealth <= 0f;
        public int BonusLives => _bonusLives;

        private void Awake()
        {
            _currentHealth = _maxHealth;
        }

        private void Update()
        {
            RunUpgradeController run = RunUpgradeController.Instance;
            if (run == null || run.HealthRegenPerSecond <= 0f || IsDead)
            {
                return;
            }

            Heal(run.HealthRegenPerSecond * Time.deltaTime);
        }

        public void AddBonusLife(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            _bonusLives += amount;
        }

        public void TakeDamage(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            PlayerPowerupController powerups = GetComponent<PlayerPowerupController>();
            if (powerups == null)
            {
                powerups = GetComponentInParent<PlayerPowerupController>();
            }
            if (powerups != null && powerups.IsDamageImmune)
            {
                return;
            }

            amount *= _incomingDamageMultiplier;

            float time = Time.time;
            if (time < _invulnerableUntil)
            {
                return;
            }

            PlayerTookDamage?.Invoke();

            _currentHealth -= amount;
            if (_currentHealth < 0f)
            {
                _currentHealth = 0f;
            }

            if (_currentHealth <= 0f && _bonusLives > 0)
            {
                _bonusLives--;
                _currentHealth = _maxHealth * Mathf.Clamp01(_bonusLifeRestoreHealthFraction);
                _invulnerableUntil = time + _invulnerabilityDuration;
                AudioManager.EnsureExists().PlayPlayerHit();
                return;
            }

            AudioManager.EnsureExists().PlayPlayerHit();

            _invulnerableUntil = time + _invulnerabilityDuration;
        }

        public void HealFull()
        {
            _currentHealth = _maxHealth;
        }

        public void Heal(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            _currentHealth += amount;
            if (_currentHealth > _maxHealth)
            {
                _currentHealth = _maxHealth;
            }
        }

        public void SetIncomingDamageMultiplier(float multiplier)
        {
            _incomingDamageMultiplier = Mathf.Max(0.1f, multiplier);
        }
    }
}
