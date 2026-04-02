using UnityEngine;
using StaticDrift.Managers;

namespace StaticDrift.Player
{
    /// <summary>
    /// Receives damage from enemies. Tag the GameObject "Player".
    /// </summary>
    public class PlayerHealth : MonoBehaviour
    {
        [SerializeField] private float _maxHealth = 100f;
        [SerializeField] private float _invulnerabilityDuration = 0.5f;

        private float _currentHealth;
        private float _invulnerableUntil;
        private float _incomingDamageMultiplier = 1f;

        public float CurrentHealth => _currentHealth;
        public float MaxHealth => _maxHealth;
        public bool IsDead => _currentHealth <= 0f;

        private void Awake()
        {
            _currentHealth = _maxHealth;
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

            _currentHealth -= amount;
            if (_currentHealth < 0f)
            {
                _currentHealth = 0f;
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
