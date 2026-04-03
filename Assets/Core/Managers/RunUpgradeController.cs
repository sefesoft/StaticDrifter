using System.Collections.Generic;
using UnityEngine;
using StaticDrift.Cards;
using StaticDrift.Player;

namespace StaticDrift.Managers
{
    public class RunUpgradeController : MonoBehaviour
    {
        public const int LoadoutSlotCount = 4;

        [SerializeField] private int _maxStacksPerUpgrade = 3;
        [SerializeField] private float _startingFireIntervalMultiplier = 1.15f;
        [Tooltip("Base projectile lifetime multiplier (lower = shorter shots before upgrades).")]
        [SerializeField] private float _startingProjectileLifetimeMultiplier = 0.36f;
        [SerializeField] private int _maxBonusLivesFromUpgrades = 4;

        public enum UpgradeId
        {
            VoltOverclock,
            VoltChainCharge,
            KineticPayload,
            KineticSlinger,
            ThermalFlux,
            ThermalCore,
            StaticPlating,
            StaticField,
            RepairNanites,
            RepairWeave,
            ReachExtender,
            ReachCalibrator,
            VolleySpread,
            BackupCell,
            ReserveHarness
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
                Description = "-8% fire interval."
            },
            new UpgradeOption
            {
                Id = UpgradeId.VoltChainCharge,
                Tag = CardTag.Volt,
                Title = "Volt Accelerator",
                Description = "+12% projectile speed."
            },
            new UpgradeOption
            {
                Id = UpgradeId.KineticPayload,
                Tag = CardTag.Kinetic,
                Title = "Kinetic Payload",
                Description = "+12% projectile damage."
            },
            new UpgradeOption
            {
                Id = UpgradeId.KineticSlinger,
                Tag = CardTag.Kinetic,
                Title = "Kinetic Slinger",
                Description = "+10% projectile speed, +4% damage."
            },
            new UpgradeOption
            {
                Id = UpgradeId.ThermalFlux,
                Tag = CardTag.Thermal,
                Title = "Thermal Shrapnel",
                Description = "+0.30 splash radius, enables AOE."
            },
            new UpgradeOption
            {
                Id = UpgradeId.ThermalCore,
                Tag = CardTag.Thermal,
                Title = "Thermal Reactor",
                Description = "+8% damage, +0.38 splash, +6% splash damage."
            },
            new UpgradeOption
            {
                Id = UpgradeId.StaticPlating,
                Tag = CardTag.Static,
                Title = "Static Plating",
                Description = "-8% incoming damage."
            },
            new UpgradeOption
            {
                Id = UpgradeId.StaticField,
                Tag = CardTag.Static,
                Title = "Static Field",
                Description = "Heal 12 HP, -3% fire interval, -4% incoming damage."
            },
            new UpgradeOption
            {
                Id = UpgradeId.RepairNanites,
                Tag = CardTag.Repair,
                Title = "Nanite Swarm",
                Description = "+0.09 HP/sec regeneration."
            },
            new UpgradeOption
            {
                Id = UpgradeId.RepairWeave,
                Tag = CardTag.Repair,
                Title = "Biosuture Weave",
                Description = "+0.07 HP/sec regeneration."
            },
            new UpgradeOption
            {
                Id = UpgradeId.ReachExtender,
                Tag = CardTag.Reach,
                Title = "Coil Extender",
                Description = "+9% projectile travel distance."
            },
            new UpgradeOption
            {
                Id = UpgradeId.ReachCalibrator,
                Tag = CardTag.Reach,
                Title = "Harmonic Lens",
                Description = "+7% reach, +3% damage."
            },
            new UpgradeOption
            {
                Id = UpgradeId.VolleySpread,
                Tag = CardTag.Volley,
                Title = "Scatter Matrix",
                Description = "+1 spread shot (up to 4 total, Contra-style fan)."
            },
            new UpgradeOption
            {
                Id = UpgradeId.BackupCell,
                Tag = CardTag.Vitality,
                Title = "Backup Cell",
                Description = "+1 extra life; on lethal hit revive at 50% HP."
            },
            new UpgradeOption
            {
                Id = UpgradeId.ReserveHarness,
                Tag = CardTag.Vitality,
                Title = "Reserve Harness",
                Description = "+1 extra life, heal 15 HP now; revives at 50% HP."
            }
        };

        public static RunUpgradeController Instance { get; private set; }

        public float FireIntervalMultiplier { get; private set; } = 1f;
        public float ProjectileDamageMultiplier { get; private set; } = 1f;
        public float ProjectileSpeedMultiplier { get; private set; } = 1f;
        public float ProjectileSplashRadius { get; private set; }
        public float ProjectileSplashDamageMultiplier { get; private set; } = 0.45f;
        public float IncomingDamageMultiplier { get; private set; } = 1f;
        public float ProjectileLifetimeMultiplier { get; private set; } = 1f;
        public float HealthRegenPerSecond { get; private set; }
        public int VolleyPelletCount { get; private set; } = 1;

        private readonly List<CardTag> _loadoutTags = new List<CardTag>(LoadoutSlotCount);
        private int[] _stackCounts;
        private int _bonusLivesFromUpgrades;

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
            ProjectileLifetimeMultiplier = Mathf.Clamp(_startingProjectileLifetimeMultiplier, 0.22f, 1.1f);
            HealthRegenPerSecond = 0f;
            VolleyPelletCount = 1;
            _bonusLivesFromUpgrades = 0;
            _loadoutTags.Clear();
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

        public IReadOnlyList<CardTag> LoadoutTags => _loadoutTags;
        public bool IsLoadoutFull => _loadoutTags.Count >= LoadoutSlotCount;

        public CardTag GetLoadoutSlotTag(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= LoadoutSlotCount)
            {
                return CardTag.None;
            }

            return slotIndex < _loadoutTags.Count ? _loadoutTags[slotIndex] : CardTag.None;
        }

        public string GetLoadoutSlotStacksText(int slotIndex)
        {
            CardTag tag = GetLoadoutSlotTag(slotIndex);
            if (tag == CardTag.None)
            {
                return "--";
            }

            return GetTotalStacksForTag(tag) + "/" + GetMaxPossibleStacksForTag(tag);
        }

        public int GetTotalStacksForTag(CardTag tag)
        {
            int sum = 0;
            int n = _allOptions.Length;
            for (int i = 0; i < n; i++)
            {
                if (_allOptions[i].Tag == tag)
                {
                    sum += _stackCounts != null && i < _stackCounts.Length ? _stackCounts[i] : 0;
                }
            }

            return sum;
        }

        public int GetMaxPossibleStacksForTag(CardTag tag)
        {
            int defs = 0;
            int n = _allOptions.Length;
            for (int i = 0; i < n; i++)
            {
                if (_allOptions[i].Tag == tag)
                {
                    defs++;
                }
            }

            return defs * Mathf.Max(1, _maxStacksPerUpgrade);
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

            while (selected < target && safety < 400)
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

            CardTag tag = GetTagForUpgradeId(id);
            if (tag == CardTag.None)
            {
                return false;
            }

            if (!CanTakeTagForNewPick(tag))
            {
                return false;
            }

            if (id == UpgradeId.BackupCell || id == UpgradeId.ReserveHarness)
            {
                if (playerHealth == null || _bonusLivesFromUpgrades >= _maxBonusLivesFromUpgrades)
                {
                    return false;
                }
            }

            if (id == UpgradeId.VolleySpread && VolleyPelletCount >= 4)
            {
                return false;
            }

            switch (id)
            {
                case UpgradeId.VoltOverclock:
                    FireIntervalMultiplier *= 0.92f;
                    break;
                case UpgradeId.VoltChainCharge:
                    ProjectileSpeedMultiplier *= 1.12f;
                    break;
                case UpgradeId.KineticPayload:
                    ProjectileDamageMultiplier *= 1.12f;
                    break;
                case UpgradeId.KineticSlinger:
                    ProjectileSpeedMultiplier *= 1.10f;
                    ProjectileDamageMultiplier *= 1.04f;
                    break;
                case UpgradeId.ThermalFlux:
                    ProjectileSplashRadius += 0.30f;
                    break;
                case UpgradeId.ThermalCore:
                    ProjectileDamageMultiplier *= 1.08f;
                    ProjectileSplashRadius += 0.38f;
                    ProjectileSplashDamageMultiplier += 0.06f;
                    break;
                case UpgradeId.StaticPlating:
                    IncomingDamageMultiplier *= 0.92f;
                    break;
                case UpgradeId.StaticField:
                    IncomingDamageMultiplier *= 0.96f;
                    FireIntervalMultiplier *= 0.97f;
                    if (playerHealth != null)
                    {
                        playerHealth.Heal(12f);
                    }

                    break;
                case UpgradeId.RepairNanites:
                    HealthRegenPerSecond += 0.09f;
                    break;
                case UpgradeId.RepairWeave:
                    HealthRegenPerSecond += 0.07f;
                    break;
                case UpgradeId.ReachExtender:
                    ProjectileLifetimeMultiplier += 0.09f;
                    break;
                case UpgradeId.ReachCalibrator:
                    ProjectileLifetimeMultiplier += 0.07f;
                    ProjectileDamageMultiplier *= 1.03f;
                    break;
                case UpgradeId.VolleySpread:
                    VolleyPelletCount++;
                    break;
                case UpgradeId.BackupCell:
                    TryGrantBonusLife(playerHealth);
                    break;
                case UpgradeId.ReserveHarness:
                    TryGrantBonusLife(playerHealth);
                    if (playerHealth != null)
                    {
                        playerHealth.Heal(15f);
                    }

                    break;
                default:
                    return false;
            }

            if (_stackCounts != null && stackIndex >= 0 && stackIndex < _stackCounts.Length)
            {
                _stackCounts[stackIndex]++;
            }

            RegisterLoadoutTag(tag);
            FireIntervalMultiplier = Mathf.Clamp(FireIntervalMultiplier, 0.38f, 1.6f);
            IncomingDamageMultiplier = Mathf.Clamp(IncomingDamageMultiplier, 0.52f, 1f);
            ProjectileSplashDamageMultiplier = Mathf.Clamp(ProjectileSplashDamageMultiplier, 0.2f, 0.85f);
            ProjectileDamageMultiplier = Mathf.Clamp(ProjectileDamageMultiplier, 0.8f, 2.35f);
            ProjectileSpeedMultiplier = Mathf.Clamp(ProjectileSpeedMultiplier, 0.8f, 2.1f);
            ProjectileSplashRadius = Mathf.Clamp(ProjectileSplashRadius, 0f, 2.2f);
            ProjectileLifetimeMultiplier = Mathf.Clamp(ProjectileLifetimeMultiplier, 0.22f, 0.92f);
            HealthRegenPerSecond = Mathf.Clamp(HealthRegenPerSecond, 0f, 1.05f);
            return true;
        }

        private void TryGrantBonusLife(PlayerHealth playerHealth)
        {
            if (playerHealth == null)
            {
                return;
            }

            if (_bonusLivesFromUpgrades >= _maxBonusLivesFromUpgrades)
            {
                return;
            }

            _bonusLivesFromUpgrades++;
            playerHealth.AddBonusLife(1);
        }

        private static CardTag GetTagForUpgradeId(UpgradeId id)
        {
            int n = _allOptions.Length;
            for (int i = 0; i < n; i++)
            {
                if (_allOptions[i].Id == id)
                {
                    return _allOptions[i].Tag;
                }
            }

            return CardTag.None;
        }

        public string GetSynergySummary()
        {
            char[] letters = { '?', '?', '?', '?' };
            for (int i = 0; i < _loadoutTags.Count && i < LoadoutSlotCount; i++)
            {
                letters[i] = GetTagLetterChar(_loadoutTags[i]);
            }

            return letters[0] + "[" + GetSlotStacksOrDash(0) + "] "
                + letters[1] + "[" + GetSlotStacksOrDash(1) + "] "
                + letters[2] + "[" + GetSlotStacksOrDash(2) + "] "
                + letters[3] + "[" + GetSlotStacksOrDash(3) + "]";
        }

        private string GetSlotStacksOrDash(int slotIndex)
        {
            CardTag tag = GetLoadoutSlotTag(slotIndex);
            if (tag == CardTag.None)
            {
                return "-";
            }

            return GetTotalStacksForTag(tag).ToString();
        }

        private static char GetTagLetterChar(CardTag tag)
        {
            switch (tag)
            {
                case CardTag.Volt:
                    return 'V';
                case CardTag.Kinetic:
                    return 'K';
                case CardTag.Thermal:
                    return 'T';
                case CardTag.Static:
                    return 'S';
                case CardTag.Repair:
                    return 'R';
                case CardTag.Reach:
                    return 'E';
                case CardTag.Volley:
                    return 'C';
                case CardTag.Vitality:
                    return 'L';
                default:
                    return '?';
            }
        }

        private void RegisterLoadoutTag(CardTag tag)
        {
            if (tag == CardTag.None)
            {
                return;
            }

            if (_loadoutTags.Contains(tag))
            {
                return;
            }

            if (_loadoutTags.Count < LoadoutSlotCount)
            {
                _loadoutTags.Add(tag);
            }
        }

        private bool CanTakeTagForNewPick(CardTag tag)
        {
            if (tag == CardTag.None)
            {
                return true;
            }

            if (_loadoutTags.Count >= LoadoutSlotCount)
            {
                return _loadoutTags.Contains(tag);
            }

            return true;
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
                CardTag tag = _allOptions[i].Tag;
                bool tagAllowed = CanTakeTagForNewPick(tag);
                if (canStack && tagAllowed)
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
    }
}
