using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace StaticDrift.Editor
{
    public static class EnsureByteBounceFontAsset
    {
        private const string SourceFontPath = "Assets/TextMesh Pro/Fonts/ByteBounce.ttf";
        private const string ResourcesFolderPath = "Assets/Resources/Fonts";
        private const string OutputFontAssetPath = ResourcesFolderPath + "/ByteBounce SDF.asset";

        [InitializeOnLoadMethod]
        private static void EnsureOnLoad()
        {
            EditorApplication.delayCall += EnsureFontAssetExists;
        }

        [MenuItem("Static Drift/Refresh ByteBounce Font")]
        private static void EnsureFontAssetExists()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }

            Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(SourceFontPath);
            if (sourceFont == null)
            {
                return;
            }

            if (!AssetDatabase.IsValidFolder(ResourcesFolderPath))
            {
                Directory.CreateDirectory(ResourcesFolderPath);
                AssetDatabase.Refresh();
            }

            TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(OutputFontAssetPath);
            if (IsFontAssetValid(existing))
            {
                return;
            }

            if (existing != null)
            {
                AssetDatabase.DeleteAsset(OutputFontAssetPath);
                AssetDatabase.Refresh();
            }

            TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
                sourceFont,
                96,
                8,
                GlyphRenderMode.SDFAA,
                1024,
                1024,
                AtlasPopulationMode.Dynamic,
                true);

            if (fontAsset == null)
            {
                return;
            }

            AssetDatabase.CreateAsset(fontAsset, OutputFontAssetPath);
            AddSubAssets(fontAsset);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static bool IsFontAssetValid(TMP_FontAsset fontAsset)
        {
            if (fontAsset == null)
            {
                return false;
            }

            if (fontAsset.material == null)
            {
                return false;
            }

            if (fontAsset.atlasTextures == null || fontAsset.atlasTextures.Length == 0)
            {
                return false;
            }

            return fontAsset.atlasTextures[0] != null;
        }

        private static void AddSubAssets(TMP_FontAsset fontAsset)
        {
            if (fontAsset == null)
            {
                return;
            }

            if (fontAsset.material != null && AssetDatabase.GetAssetPath(fontAsset.material) != OutputFontAssetPath)
            {
                fontAsset.material.name = fontAsset.name + " Material";
                AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
            }

            Texture2D[] atlasTextures = fontAsset.atlasTextures;
            if (atlasTextures == null)
            {
                return;
            }

            int atlasCount = atlasTextures.Length;
            for (int i = 0; i < atlasCount; i++)
            {
                Texture2D atlasTexture = atlasTextures[i];
                if (atlasTexture == null)
                {
                    continue;
                }

                if (AssetDatabase.GetAssetPath(atlasTexture) == OutputFontAssetPath)
                {
                    continue;
                }

                atlasTexture.name = fontAsset.name + " Atlas " + i;
                AssetDatabase.AddObjectToAsset(atlasTexture, fontAsset);
            }
        }
    }
}
