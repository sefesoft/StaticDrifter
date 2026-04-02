using TMPro;
using UnityEngine;

namespace StaticDrift.UI
{
    public static class GameFontLibrary
    {
        private const string ByteBounceFontResourcePath = "Fonts/ByteBounce SDF";
        private static TMP_FontAsset _cachedFont;

        public static TMP_FontAsset GetUIFont()
        {
            if (IsFontValid(_cachedFont))
            {
                return _cachedFont;
            }

            _cachedFont = Resources.Load<TMP_FontAsset>(ByteBounceFontResourcePath);
            if (!IsFontValid(_cachedFont))
            {
                _cachedFont = TMP_Settings.defaultFontAsset;
            }

            return _cachedFont;
        }

        public static void Apply(TMP_Text text)
        {
            if (text == null)
            {
                return;
            }

            TMP_FontAsset font = GetUIFont();
            if (font != null)
            {
                text.font = font;
            }
        }

        public static void ApplyOutline(TMP_Text text, float width, Color color)
        {
            if (text == null)
            {
                return;
            }

            try
            {
                Material material = text.fontMaterial;
                if (material == null)
                {
                    return;
                }

                if (!material.HasProperty(ShaderUtilities.ID_OutlineWidth) || !material.HasProperty(ShaderUtilities.ID_OutlineColor))
                {
                    return;
                }

                material.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
                material.SetColor(ShaderUtilities.ID_OutlineColor, color);
                text.UpdateMeshPadding();
            }
            catch (System.Exception)
            {
                // Some TMP font/material combinations are not outline-ready on first frame.
            }
        }

        private static bool IsFontValid(TMP_FontAsset font)
        {
            if (font == null)
            {
                return false;
            }

            if (font.material == null)
            {
                return false;
            }

            Texture2D[] atlasTextures = font.atlasTextures;
            if (atlasTextures == null || atlasTextures.Length == 0)
            {
                return false;
            }

            return atlasTextures[0] != null;
        }
    }
}
