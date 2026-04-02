using UnityEngine;
using StaticDrift.Cards;
using StaticDrift.Player;

namespace StaticDrift.Managers
{
    public class RunUpgradeController : MonoBehaviour
    {
        [Header("Upgrade Limits (Per Tag)")]
        [SerializeField] private int _maxVoltPicks = 3;
        [SerializeField] private int _maxKineticPicks = 3;
        [SerializeField] private int _maxThermalPicks = 3;
        [SerializeField] private int _maxStaticPicks = 3;

        [Header("Upgrade Limits (Per Upgrade)")]
        [SerializeField] private int _maxStacksPerUpgrade = 3;
        [SerializeField] private float _startingFireIntervalMultiplier = 1.15f;

        public enum UpgradeId
        {
            VoltOverclock,
            VoltChainCharge,
            KineticPayload,
            KineticSlinger,
            ThermalFlux,
            ThermalCore,
            StaticPlating,
            StaticField
        }

        public struct UpgradeOption
        {
            public UpgradeId Id;
            public CardTag Tag;
            public string Title;
            public string Description;
        }

        private static readonly UpgradeOption[] _allOptions = new UpgradeOption[]
        {
            new UpgradeOption
            {
                Id = UpgradeId.VoltOverclock,
                Tag = CardTag.Volt,
                Title = "Volt Overclock",
                Description = "-12% fire interval."
            },
            new UpgradeOption
            {
                Id = UpgradeId.VoltChainCharge,
                Tag = CardTag.Volt,
                Title = "Volt Accelerator",
                Description = "+20% projectile speed."
            },
            new UpgradeOption
            {
                Id = UpgradeId.KineticPayload,
                Tag = CardTag.Kinetic,
                Title = "Kinetic Payload",
                Description = "+20% projectile damage."
            },
            new UpgradeOption
            {
                Id = UpgradeId.KineticSlinger,
                Tag = CardTag.Kinetic,
                Title = "Kinetic Slinger",
                Description = "+15% projectile speed."
            },
            new UpgradeOption
            {
                Id = UpgradeId.ThermalFlux,
                Tag = CardTag.Thermal,
                Title = "Thermal Shrapnel",
                Description = "+0.45 splash radius, enables AOE."
            },
            new UpgradeOption
            {
                Id = UpgradeId.ThermalCore,
                Tag = CardTag.Thermal,
                Title = "Thermal Reactor",
                Description = "+15% damage, +0.60 splash, +10% splash damage."
            },
            new UpgradeOption
            {
                Id = UpgradeId.StaticPlating,
                Tag = CardTag.Static,
                Title = "Static Plating",
                Description = "-12% incoming damage."
            },
            new UpgradeOption
            {
                Id = UpgradeId.StaticField,
                Tag = CardTag.Static,
                Title = "Static Field",
                Description = "Heal 20 HP, -5% fire interval, -6% incoming damage."
            }
        };

        public static RunUpgradeController Instance { get; private set; }

        public float FireIntervalMultiplier { get; private set; } = 1f;
        public float ProjectileDamageMultiplier { get; private set; } = 1f;
        public float ProjectileSpeedMultiplier { get; private set; } = 1f;
        public float ProjectileSplashRadius { get; private set; }
        public float ProjectileSplashDamageMultiplier { get; private set; } = 0.45f;
        public float IncomingDamageMultiplier { get; private set; } = 1f;

        private int _voltPicks;
        private int _kineticPicks;
        private int _thermalPicks;
        private int _staticPicks;
        private int _totalPicks;
        private int[] _stackCounts;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            ResetRun();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void ResetRun()
        {
            FireIntervalMultiplier = Mathf.Max(1f, _startingFireIntervalMultiplier);
            ProjectileDamageMultiplier = 1f;
            ProjectileSpeedMultiplier = 1f;
            ProjectileSplashRadius = 0f;
            ProjectileSplashDamageMultiplier = 0.45f;
            IncomingDamageMultiplier = 1f;
            _voltPicks = 0;
            _kineticPicks = 0;
            _thermalPicks = 0;
            _staticPicks = 0;
            _totalPicks = 0;
            if (_stackCounts == null || _stackCounts.Length != _allOptions.Length)
            {
                _stackCounts = new int[_allOptions.Length];
            }
            else
            {
                for (int i = 0; i < _stackCounts.Length; i++)
                {
                    _stackCounts[i] = 0;
                }
            }
        }

        public UpgradeOption[] BuildDraftOptions(int count)
        {
            int[] candidateIndices = BuildCandidateIndexList();
            int candidateCount = candidateIndices.Length;
            if (candidateCount == 0)
            {
                return new UpgradeOption[0];
            }

            int target = Mathf.Clamp(count, 1, candidateCount);
            UpgradeOption[] result = new UpgradeOption[target];
            int[] used = new int[target];
            int selected = 0;
            int safety = 0;

            while (selected < target && safety < 200)
            {
                safety++;
                int candidateIndex = candidateIndices[Random.Range(0, candidateCount)];
                bool alreadyUsed = false;
                for (int i = 0; i < selected; i++)
                {
                    if (used[i] == candidateIndex)
                    {
                        alreadyUsed = true;
                        break;
                    }
                }

                if (alreadyUsed)
                {
                    continue;
                }

                used[selected] = candidateIndex;
                result[selected] = _allOptions[candidateIndex];
                selected++;
            }

            return result;
        }

        public bool ApplyUpgrade(UpgradeId id, PlayerHealth playerHealth)
        {
            int stackIndex = (int)id;
            if (_stackCounts != null && stackIndex >= 0 && stackIndex < _stackCounts.Length)
            {
                if (_stackCounts[stackIndex] >= Mathf.Max(1, _maxStacksPerUpgrade))
                {
                    return false;
                }
            }

            CardTag tag = CardTag.None;

            switch (id)
            {
                case UpgradeId.VoltOverclock:
                    FireIntervalMultiplier *= 0.88f;
                    tag = CardTag.Volt;
                    break;
                case UpgradeId.VoltChainCharge:
                    ProjectileSpeedMultiplier *= 1.20f;
                    tag = CardTag.Volt;
                    break;
                case UpgradeId.KineticPayload:
                    ProjectileDamageMultiplier *= 1.20f;
                    tag = CardTag.Kinetic;
                    break;
                case UpgradeId.KineticSlinger:
                    ProjectileSpeedMultiplier *= 1.15f;
                    ProjectileDamageMultiplier *= 1.08f;
                    tag = CardTag.Kinetic;
                    break;
                case UpgradeId.ThermalFlux:
                    ProjectileSplashRadius += 0.45f;
                    tag = CardTag.Thermal;
                    break;
                case UpgradeId.ThermalCore:
                    ProjectileDamageMultiplier *= 1.15f;
                    ProjectileSplashRadius += 0.60f;
                    ProjectileSplashDamageMultiplier += 0.10f;
                    tag = CardTag.Thermal;
                    break;
                case UpgradeId.StaticPlating:
                    IncomingDamageMultiplier *= 0.88f;
                    tag = CardTag.Static;
                    break;
                case UpgradeId.StaticField:
                    IncomingDamageMultiplier *= 0.94f;
                    FireIntervalMultiplier *= 0.95f;
                    if (playerHealth != null)
                    {
                        playerHealth.Heal(20f);
                    }
                    tag = CardTag.Static;
                    break;
            }

            if (tag != CardTag.None && !CanTakeTag(tag))
            {
                return false;
            }

            _totalPicks++;
            if (_stackCounts != null && stackIndex >= 0 && stackIndex < _stackCounts.Length)
            {
                _stackCounts[stackIndex]++;
            }

            IncrementTagCount(tag);
            FireIntervalMultiplier = Mathf.Clamp(FireIntervalMultiplier, 0.32f, 1.6f);
            IncomingDamageMultiplier = Mathf.Clamp(IncomingDamageMultiplier, 0.40f, 1f);
            ProjectileSplashDamageMultiplier = Mathf.Clamp(ProjectileSplashDamageMultiplier, 0.2f, 0.9f);
            ProjectileDamageMultiplier = Mathf.Clamp(ProjectileDamageMultiplier, 0.8f, 3f);
            ProjectileSpeedMultiplier = Mathf.Clamp(ProjectileSpeedMultiplier, 0.8f, 2.4f);
            ProjectileSplashRadius = Mathf.Clamp(ProjectileSplashRadius, 0f, 2.6f);
            return true;
        }

        public string GetSynergySummary()
        {
            // Show tag pick ranges (e.g. "V[0-3]") so players know each synergy cap.
            return "V[" + _voltPicks + "-" + _maxVoltPicks + "] "
                + "K[" + _kineticPicks + "-" + _maxKineticPicks + "] "
                + "T[" + _thermalPicks + "-" + _maxThermalPicks + "] "
                + "S[" + _staticPicks + "-" + _maxStaticPicks + "]";
        }

        private void IncrementTagCount(CardTag tag)
        {
            switch (tag)
            {
                case CardTag.Volt:
                    _voltPicks++;
                    break;
                case CardTag.Kinetic:
                    _kineticPicks++;
                    break;
                case CardTag.Thermal:
                    _thermalPicks++;
                    break;
                case CardTag.Static:
                    _staticPicks++;
                    break;
            }
        }

        private int[] BuildCandidateIndexList()
        {
            int limit = Mathf.Max(1, _maxStacksPerUpgrade);
            int count = _allOptions.Length;
            int[] temp = new int[count];
            int write = 0;

            for (int i = 0; i < count; i++)
            {
                int stacks = _stackCounts != null && i < _stackCounts.Length ? _stackCounts[i] : 0;
                bool canStack = stacks < limit;
                bool canTag = CanTakeTag(_allOptions[i].Tag);
                if (canStack && canTag)
                {
                    temp[write] = i;
                    write++;
                }
            }

            int[] result = new int[write];
            for (int i = 0; i < write; i++)
            {
                result[i] = temp[i];
            }

            return result;
        }

        private bool CanTakeTag(CardTag tag)
        {
            if (tag == CardTag.None)
            {
                return true;
            }

            if (tag == CardTag.Volt)
            {
                return _voltPicks < Mathf.Max(0, _maxVoltPicks);
            }
            if (tag == CardTag.Kinetic)
            {
                return _kineticPicks < Mathf.Max(0, _maxKineticPicks);
            }
            if (tag == CardTag.Thermal)
            {
                return _thermalPicks < Mathf.Max(0, _maxThermalPicks);
            }
            if (tag == CardTag.Static)
            {
                return _staticPicks < Mathf.Max(0, _maxStaticPicks);
            }

            return false;
        }
    }
}
