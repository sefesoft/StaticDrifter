using UnityEngine;

namespace StaticDrift.Cards
{
    public enum CardTag
    {
        None = 0,
        Volt = 1,
        Kinetic = 2,
        Thermal = 3,
        Static = 4,
        Repair = 5,
        Reach = 6,
        Volley = 7,
        Vitality = 8
    }

    public enum CardRarity
    {
        Common = 0,
        Rare = 1,
        Epic = 2,
        Legendary = 3
    }

    [CreateAssetMenu(
        fileName = "NewCard",
        menuName = "Static Drift/Card",
        order = 0)]
    public class CardData : ScriptableObject
    {
        [Header("Identity")]
        [SerializeField] private string _cardId;
        [SerializeField] private string _displayName;
        [SerializeField] [TextArea] private string _description;

        [Header("Classification")]
        [SerializeField] private CardTag _tag;
        [SerializeField] private CardRarity _rarity;

        [SerializeField] private bool _isHeavy;

        [Header("Level & Unlocks")]
        [SerializeField] private int _baseLevel = 1;
        [SerializeField] private int _maxLevel = 5;
        [SerializeField] private bool _unlockedByDefault = true;

        [Header("Numeric Tuning")]
        [SerializeField] private float _baseDamage = 10f;
        [SerializeField] private float _baseAttackInterval = 1f;
        [SerializeField] private float _baseAreaRadius = 1f;
        [SerializeField] private float _baseProjectileSpeed = 10f;
        [SerializeField] private float _baseDuration = 0f;

        [Header("Synergy Hooks")]
        [SerializeField] private float _voltAttackSpeedScaling = 1f;
        [SerializeField] private float _kineticImpactScaling = 1f;
        [SerializeField] private float _thermalAreaScaling = 1f;
        [SerializeField] private float _staticUtilityScaling = 1f;

        [Header("Visuals & Audio")]
        [SerializeField] private Sprite _icon;
        [SerializeField] private Color _accentColor = Color.white;
        [SerializeField] private GameObject _projectilePrefab;
        [SerializeField] private AudioClip _fireSfx;

        public string CardId => _cardId;
        public string DisplayName => _displayName;
        public string Description => _description;

        public CardTag Tag => _tag;
        public CardRarity Rarity => _rarity;
        public bool IsHeavy => _isHeavy;

        public int BaseLevel => _baseLevel;
        public int MaxLevel => _maxLevel;
        public bool UnlockedByDefault => _unlockedByDefault;

        public float BaseDamage => _baseDamage;
        public float BaseAttackInterval => _baseAttackInterval;
        public float BaseAreaRadius => _baseAreaRadius;
        public float BaseProjectileSpeed => _baseProjectileSpeed;
        public float BaseDuration => _baseDuration;

        public float VoltAttackSpeedScaling => _voltAttackSpeedScaling;
        public float KineticImpactScaling => _kineticImpactScaling;
        public float ThermalAreaScaling => _thermalAreaScaling;
        public float StaticUtilityScaling => _staticUtilityScaling;

        public Sprite Icon => _icon;
        public Color AccentColor => _accentColor;
        public GameObject ProjectilePrefab => _projectilePrefab;
        public AudioClip FireSfx => _fireSfx;
    }
}
