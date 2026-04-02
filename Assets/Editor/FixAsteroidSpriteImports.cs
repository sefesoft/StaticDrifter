using UnityEditor;
using UnityEngine;

namespace StaticDrift.Editor
{
    public static class FixAsteroidSpriteImports
    {
        private const string LargeSpritePath = "Assets/Art/Sprites/AsteroidLarge.png";
        private const string MediumSpritePath = "Assets/Art/Sprites/AsteroidMedium.png";
        private const string SmallSpritePath = "Assets/Art/Sprites/AsteroidSmall.png";

        private const string LargePrefabPath = "Assets/Prefabs/Gameplay/AsteroidLarge.prefab";
        private const string MediumPrefabPath = "Assets/Prefabs/Gameplay/AsteroidMedium.prefab";
        private const string SmallPrefabPath = "Assets/Prefabs/Gameplay/AsteroidSmall.prefab";

        private const string MenuPath = "Static Drift/Fix Asteroid Sprite Imports";

        [InitializeOnLoadMethod]
        private static void AutoFixOnLoad()
        {
            EditorApplication.delayCall += TryFixOnce;
        }

        [MenuItem(MenuPath)]
        public static void TryFixOnce()
        {
            FixSprite(LargeSpritePath);
            FixSprite(MediumSpritePath);
            FixSprite(SmallSpritePath);

            AssignSpriteToPrefab(LargePrefabPath, LargeSpritePath);
            AssignSpriteToPrefab(MediumPrefabPath, MediumSpritePath);
            AssignSpriteToPrefab(SmallPrefabPath, SmallSpritePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void FixSprite(string assetPath)
        {
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            if (texture == null)
            {
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
            }

            TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        private static void AssignSpriteToPrefab(string prefabPath, string spritePath)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (sprite == null)
            {
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
            {
                return;
            }

            SpriteRenderer renderer = root.GetComponent<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.sprite = sprite;
                EditorUtility.SetDirty(renderer);
            }

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
