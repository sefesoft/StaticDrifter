using UnityEngine;

namespace StaticDrift.Items
{
    /// <summary>
    /// Loads item art from Resources/Gameplay/Items/{ItemType} (sprite file name matches <see cref="ItemType"/>).
    /// </summary>
    public static class ItemTypeSprites
    {
        private const string ResourcePathPrefix = "Gameplay/Items/";

        private static Sprite[] _cache;
        private static Sprite _fallbackRing;

        public static Sprite Get(ItemType type)
        {
            EnsureCached();
            int i = (int)type;
            if (_cache != null && i >= 0 && i < _cache.Length && _cache[i] != null)
            {
                return _cache[i];
            }

            return GetFallbackRingSprite();
        }

        private static void EnsureCached()
        {
            if (_cache != null)
            {
                return;
            }

            int n = System.Enum.GetNames(typeof(ItemType)).Length;
            _cache = new Sprite[n];
            foreach (ItemType t in System.Enum.GetValues(typeof(ItemType)))
            {
                string path = ResourcePathPrefix + t;
                Sprite s = Resources.Load<Sprite>(path);
                if (s != null)
                {
                    _cache[(int)t] = s;
                }
            }
        }

        private static Sprite GetFallbackRingSprite()
        {
            if (_fallbackRing != null)
            {
                return _fallbackRing;
            }

            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float outer = 30f;
            float inner = 16f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = 0f;
                    if (d <= outer && d >= inner)
                    {
                        alpha = 1f - Mathf.InverseLerp(outer, inner, d) * 0.2f;
                    }
                    else if (d < inner)
                    {
                        alpha = 0.22f;
                    }

                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            _fallbackRing = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            return _fallbackRing;
        }
    }
}
