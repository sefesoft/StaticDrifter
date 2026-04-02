using UnityEngine;

namespace StaticDrift.Synergy
{
    public enum SynergyTier
    {
        None = 0,
        Tier1 = 1,
        Tier2 = 2,
        Tier3 = 3
    }

    [CreateAssetMenu(
        fileName = "SynergyTierConfig",
        menuName = "Static Drift/Synergy Tier Config",
        order = 0)]
    public class SynergyTierConfig : ScriptableObject
    {
        [Header("Tier Thresholds (Shared)")]
        [SerializeField] private int _tier1Threshold = 3;
        [SerializeField] private int _tier2Threshold = 6;
        [SerializeField] private int _tier3Threshold = 9;

        [Header("Volt (Attack Speed / Chains)")]
        [SerializeField] private float _voltTier1Multiplier = 1.1f;
        [SerializeField] private float _voltTier2Multiplier = 1.25f;
        [SerializeField] private float _voltTier3Multiplier = 1.5f;

        [Header("Kinetic (Force / Knockback)")]
        [SerializeField] private float _kineticTier1Multiplier = 1.1f;
        [SerializeField] private float _kineticTier2Multiplier = 1.25f;
        [SerializeField] private float _kineticTier3Multiplier = 1.5f;

        [Header("Thermal (Area / DoT)")]
        [SerializeField] private float _thermalTier1Multiplier = 1.1f;
        [SerializeField] private float _thermalTier2Multiplier = 1.25f;
        [SerializeField] private float _thermalTier3Multiplier = 1.5f;

        [Header("Static (Utility / Shields)")]
        [SerializeField] private float _staticTier1Multiplier = 1.1f;
        [SerializeField] private float _staticTier2Multiplier = 1.25f;
        [SerializeField] private float _staticTier3Multiplier = 1.5f;

        public int Tier1Threshold => _tier1Threshold;
        public int Tier2Threshold => _tier2Threshold;
        public int Tier3Threshold => _tier3Threshold;

        public float GetMultiplierForVolt(SynergyTier tier) => GetMultiplier(tier, _voltTier1Multiplier, _voltTier2Multiplier, _voltTier3Multiplier);
        public float GetMultiplierForKinetic(SynergyTier tier) => GetMultiplier(tier, _kineticTier1Multiplier, _kineticTier2Multiplier, _kineticTier3Multiplier);
        public float GetMultiplierForThermal(SynergyTier tier) => GetMultiplier(tier, _thermalTier1Multiplier, _thermalTier2Multiplier, _thermalTier3Multiplier);
        public float GetMultiplierForStatic(SynergyTier tier) => GetMultiplier(tier, _staticTier1Multiplier, _staticTier2Multiplier, _staticTier3Multiplier);

        private float GetMultiplier(SynergyTier tier, float t1, float t2, float t3)
        {
            switch (tier)
            {
                case SynergyTier.Tier1: return t1;
                case SynergyTier.Tier2: return t2;
                case SynergyTier.Tier3: return t3;
                default: return 1f;
            }
        }
    }
}
