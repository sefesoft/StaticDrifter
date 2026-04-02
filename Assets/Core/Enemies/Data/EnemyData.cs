using UnityEngine;

namespace StaticDrift.Enemies.Data
{
    /// <summary>
    /// Per-type stats. Add new assets for new enemy kinds; wire each to a pooled prefab via EnemyWaveSpawner.
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyData", menuName = "Static Drift/Enemy Data", order = 20)]
    public class EnemyData : ScriptableObject
    {
        [SerializeField] private string _displayName = "Drone";
        [SerializeField] private float _maxHealth = 10f;
        [SerializeField] private float _moveSpeed = 3f;
        [SerializeField] private float _contactDamage = 5f;
        [SerializeField] private float _contactDamageInterval = 0.6f;

        public string DisplayName => _displayName;
        public float MaxHealth => _maxHealth;
        public float MoveSpeed => _moveSpeed;
        public float ContactDamage => _contactDamage;
        public float ContactDamageInterval => _contactDamageInterval;
    }
}
