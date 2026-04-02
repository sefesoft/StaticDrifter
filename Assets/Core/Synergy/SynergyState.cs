using StaticDrift.Cards;

namespace StaticDrift.Synergy
{
    [System.Serializable]
    public struct SynergyState
    {
        public CardTag Tag;
        public int Count;
        public SynergyTier Tier;

        public SynergyState(CardTag tag, int count, SynergyTier tier)
        {
            Tag = tag;
            Count = count;
            Tier = tier;
        }
    }
}
