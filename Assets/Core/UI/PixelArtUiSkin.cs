using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace StaticDrift.UI
{
    /// <summary>
    /// Procedural pixel-style UI skin used by runtime-built menus.
    /// </summary>
    public static class PixelArtUiSkin
    {
        private static Sprite _buttonSprite;

        public static void ApplyButtonStyle(Button button, Image image, TMP_Text label)
        {
            if (button == null || image == null)
            {
                return;
            }

            image.sprite = GetButtonSprite();
            image.type = Image.Type.Sliced;
            image.color = new Color(0.95f, 0.98f, 1f, 1f);

            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.35f, 0.52f, 0.86f, 1f);
            colors.highlightedColor = new Color(0.52f, 0.74f, 1f, 1f);
            colors.selectedColor = new Color(0.62f, 0.83f, 1f, 1f);
            colors.pressedColor = new Color(0.95f, 0.73f, 0.32f, 1f);
            colors.disabledColor = new Color(0.28f, 0.31f, 0.38f, 0.78f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.03f;
            button.colors = colors;

            if (label != null)
            {
                GameFontLibrary.Apply(label);
                label.color = new Color(0.95f, 0.98f, 1f, 1f);
                label.characterSpacing = 4f;
                GameFontLibrary.ApplyOutline(label, 0.2f, new Color(0.04f, 0.06f, 0.1f, 1f));
            }
        }

        private static Sprite GetButtonSprite()
        {
            if (_buttonSprite != null)
            {
                return _buttonSprite;
            }

            const int size = 24;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            Color32 transparent = new Color32(0, 0, 0, 0);
            Color32 outer = new Color32(9, 18, 35, 255);
            Color32 shadow = new Color32(18, 34, 63, 255);
            Color32 fill = new Color32(54, 88, 158, 255);
            Color32 highlight = new Color32(108, 160, 235, 255);
            Color32 shine = new Color32(196, 228, 255, 255);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    texture.SetPixel(x, y, transparent);
                }
            }

            for (int y = 1; y < size - 1; y++)
            {
                for (int x = 1; x < size - 1; x++)
                {
                    bool cutCorner =
                        (x <= 2 && y >= size - 3) ||
                        (x >= size - 3 && y >= size - 3) ||
                        (x <= 2 && y <= 2) ||
                        (x >= size - 3 && y <= 2);

                    if (cutCorner)
                    {
                        continue;
                    }

                    Color32 color = fill;

                    if (x == 1 || x == size - 2 || y == 1 || y == size - 2)
                    {
                        color = outer;
                    }
                    else if (x == 2 || y == size - 3)
                    {
                        color = shine;
                    }
                    else if (x == size - 3 || y == 2)
                    {
                        color = shadow;
                    }
                    else if (y >= size - 6)
                    {
                        color = highlight;
                    }

                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();
            _buttonSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, size, size),
                new Vector2(0.5f, 0.5f),
                16f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(6f, 6f, 6f, 6f));
            return _buttonSprite;
        }
    }
}
