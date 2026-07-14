using UnityEditor;
using UnityEngine;
using StaticDrift.UI;

namespace StaticDrift.EditorTools
{
    /// <summary>
    /// Builds title-screen menu hierarchies and saves them as prefabs under Assets/Prefabs/UI.
    /// Run once after changing <see cref="TitleMenuUiRuntimeFactory"/>, then assign the prefabs on the TitleScreen scene TitleScreenMenu component.
    /// </summary>
    public static class TitleMenuPrefabBaker
    {
        private const string PrefabFolder = "Assets/Prefabs/UI";

        [MenuItem("Static Drift/Bake Title Menu Prefabs")]
        public static void BakeTitleMenuPrefabs()
        {
            EnsureFolder("Assets/Prefabs");
            EnsureFolder(PrefabFolder);

            GameObject canvasRoot = new GameObject("TitleMenuBakeCanvas");
            Canvas c = canvasRoot.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            Undo.RegisterCreatedObjectUndo(canvasRoot, "Bake Title Menus");

            GameObject panel = new GameObject("Panel");
            panel.transform.SetParent(canvasRoot.transform, false);
            RectTransform panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            GameObject main = TitleMenuUiRuntimeFactory.CreateMainMenu(panel.transform, null);
            SavePrefab(main, $"{PrefabFolder}/TitleMainMenu.prefab");

            GameObject settings = new GameObject("SettingsMenu");
            settings.transform.SetParent(panel.transform, false);
            RectTransform settingsRt = settings.AddComponent<RectTransform>();
            settingsRt.anchorMin = Vector2.zero;
            settingsRt.anchorMax = Vector2.one;
            settingsRt.offsetMin = Vector2.zero;
            settingsRt.offsetMax = Vector2.zero;
            TitleMenuUiRuntimeFactory.CreateSettingsMenu(settings.transform);
            SavePrefab(settings, $"{PrefabFolder}/TitleSettingsMenu.prefab");

            GameObject leaderboard = new GameObject("LeaderboardMenu");
            leaderboard.transform.SetParent(panel.transform, false);
            RectTransform lbRt = leaderboard.AddComponent<RectTransform>();
            lbRt.anchorMin = Vector2.zero;
            lbRt.anchorMax = Vector2.one;
            lbRt.offsetMin = Vector2.zero;
            lbRt.offsetMax = Vector2.zero;
            TitleMenuUiRuntimeFactory.CreateLeaderboardMenu(leaderboard.transform);
            SavePrefab(leaderboard, $"{PrefabFolder}/TitleLeaderboardMenu.prefab");

            GameObject achievements = new GameObject("AchievementsMenu");
            achievements.transform.SetParent(panel.transform, false);
            RectTransform achRt = achievements.AddComponent<RectTransform>();
            achRt.anchorMin = Vector2.zero;
            achRt.anchorMax = Vector2.one;
            achRt.offsetMin = Vector2.zero;
            achRt.offsetMax = Vector2.zero;
            TitleMenuUiRuntimeFactory.CreateAchievementsMenu(achievements.transform);
            SavePrefab(achievements, $"{PrefabFolder}/TitleAchievementsMenu.prefab");

            Undo.DestroyObjectImmediate(canvasRoot);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog(
                "Title menu prefabs",
                $"Saved under {PrefabFolder}.\nAssign them on the TitleScreen scene → TitleScreenMenu component.",
                "OK");
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/");
            string leaf = System.IO.Path.GetFileName(path);
            if (string.IsNullOrEmpty(parent) || string.IsNullOrEmpty(leaf))
            {
                return;
            }

            if (!AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, leaf);
        }

        private static void SavePrefab(GameObject instanceRoot, string assetPath)
        {
            PrefabUtility.SaveAsPrefabAsset(instanceRoot, assetPath);
        }
    }
}
