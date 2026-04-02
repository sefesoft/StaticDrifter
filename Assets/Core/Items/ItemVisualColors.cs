using UnityEngine;

namespace StaticDrift.Items
{
    public static class ItemVisualColors
    {
        public static Color Get(ItemType type)
        {
            switch (type)
            {
                case ItemType.ContactShield:
                    return new Color(0.32f, 0.86f, 1f, 0.95f);
                case ItemType.PiercingLaser:
                    return new Color(1f, 0.42f, 0.24f, 0.95f);
                case ItemType.Overdrive:
                    return new Color(1f, 0.82f, 0.24f, 0.95f);
                case ItemType.TimeWarp:
                    return new Color(0.63f, 0.44f, 1f, 0.95f);
                case ItemType.HealthPack:
                    return new Color(0.32f, 0.95f, 0.58f, 0.95f);
                default:
                    return new Color(0.63f, 0.44f, 1f, 0.95f);
            }
        }
    }
}
