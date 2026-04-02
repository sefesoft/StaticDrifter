using System.Collections.Generic;
using UnityEngine;
using StaticDrift.Cards;

namespace StaticDrift.Synergy
{
    public class SynergyManager : MonoBehaviour
    {
        [SerializeField] private SynergyTierConfig _tierConfig;

        private SynergyState _voltState;
        private SynergyState _kineticState;
        private SynergyState _thermalState;
        private SynergyState _staticState;

        public SynergyState VoltState => _voltState;
        public SynergyState KineticState => _kineticState;
        public SynergyState ThermalState => _thermalState;
        public SynergyState StaticState => _staticState;

        public void RebuildFromDeck(IList<CardData> activeDeck)
        {
            int voltCount = 0;
            int kineticCount = 0;
            int thermalCount = 0;
            int staticCount = 0;

            int count = activeDeck != null ? activeDeck.Count : 0;
            for (int i = 0; i < count; i++)
            {
                CardData card = activeDeck[i];
                if (card == null)
                {
                    continue;
                }

                switch (card.Tag)
                {
                    case CardTag.Volt:
                        voltCount++;
                        break;
                    case CardTag.Kinetic:
                        kineticCount++;
                        break;
                    case CardTag.Thermal:
                        thermalCount++;
                        break;
                    case CardTag.Static:
                        staticCount++;
                        break;
                }
            }

            _voltState = new SynergyState(CardTag.Volt, voltCount, GetTierForCount(voltCount));
            _kineticState = new SynergyState(CardTag.Kinetic, kineticCount, GetTierForCount(kineticCount));
            _thermalState = new SynergyState(CardTag.Thermal, thermalCount, GetTierForCount(thermalCount));
            _staticState = new SynergyState(CardTag.Static, staticCount, GetTierForCount(staticCount));
        }

        public SynergyTier GetTierForTag(CardTag tag)
        {
            switch (tag)
            {
                case CardTag.Volt: return _voltState.Tier;
                case CardTag.Kinetic: return _kineticState.Tier;
                case CardTag.Thermal: return _thermalState.Tier;
                case CardTag.Static: return _staticState.Tier;
                default: return SynergyTier.None;
            }
        }

        public float GetVoltAttackSpeedMultiplier()
        {
            return _tierConfig != null
                ? _tierConfig.GetMultiplierForVolt(_voltState.Tier)
                : 1f;
        }

        public float GetKineticImpactMultiplier()
        {
            return _tierConfig != null
                ? _tierConfig.GetMultiplierForKinetic(_kineticState.Tier)
                : 1f;
        }

        public float GetThermalAreaMultiplier()
        {
            return _tierConfig != null
                ? _tierConfig.GetMultiplierForThermal(_thermalState.Tier)
                : 1f;
        }

        public float GetStaticUtilityMultiplier()
        {
            return _tierConfig != null
                ? _tierConfig.GetMultiplierForStatic(_staticState.Tier)
                : 1f;
        }

        private SynergyTier GetTierForCount(int count)
        {
            if (_tierConfig == null)
            {
                return SynergyTier.None;
            }

            if (count < _tierConfig.Tier1Threshold)
            {
                return SynergyTier.None;
            }

            if (count < _tierConfig.Tier2Threshold)
            {
                return SynergyTier.Tier1;
            }

            if (count < _tierConfig.Tier3Threshold)
            {
                return SynergyTier.Tier2;
            }

            return SynergyTier.Tier3;
        }
    }
}
