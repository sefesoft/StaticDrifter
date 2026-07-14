using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using StaticDrift.UI;

namespace StaticDrift.EditorTools
{
    /// <summary>
    /// Captures pause UI from a running game (while paused) into Assets/Prefabs/UI and auto-fills ref components.
    /// </summary>
    public static class PauseMenuPrefabBaker
    {
        private const string PrefabFolder = "Assets/Prefabs/UI";

        [MenuItem("Static Drift/Open Prefabs/UI Folder")]
        public static void SelectPrefabUiFolder()
        {
            EnsureFolder(PrefabFolder);
            Object folder = AssetDatabase.LoadAssetAtPath<Object>(PrefabFolder);
            if (folder != null)
            {
                EditorGUIUtility.PingObject(folder);
                Selection.activeObject = folder;
            }
        }

        [MenuItem("Static Drift/Capture Pause Menu Prefabs (Play Mode + Paused)")]
        public static void CapturePausePrefabsFromRunningGame()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog(
                    "Capture pause prefabs",
                    "Enter Play Mode, open the pause menu, then run this command again.",
                    "OK");
                return;
            }

            GameObject pauseCanvas = GameObject.Find("PauseCanvas");
            if (pauseCanvas == null)
            {
                EditorUtility.DisplayDialog(
                    "Capture pause prefabs",
                    "Could not find PauseCanvas. Pause the game first.",
                    "OK");
                return;
            }

            Transform panel = pauseCanvas.transform.Find("Panel");
            if (panel == null)
            {
                EditorUtility.DisplayDialog("Capture pause prefabs", "PauseCanvas has no Panel child.", "OK");
                return;
            }

            EnsureFolder("Assets/Prefabs");
            EnsureFolder(PrefabFolder);

            Transform pauseRoot = panel.Find("PauseMenuRoot");
            if (pauseRoot != null)
            {
                EnsurePauseMainMenuRefs(pauseRoot.gameObject);
                SaveUniquePrefab(pauseRoot.gameObject, $"{PrefabFolder}/PauseMenuRoot.prefab");
            }

            Transform achTf = FindDeepChild(pauseCanvas.transform, "PauseAchievementsWindow");
            if (achTf != null)
            {
                EnsurePauseAchievementsRefs(achTf.gameObject);
                SaveUniquePrefab(achTf.gameObject, $"{PrefabFolder}/PauseAchievementsWindow.prefab");
            }

            Transform infoTf = FindDeepChild(pauseCanvas.transform, "PauseInfoWindow");
            if (infoTf != null)
            {
                EnsurePauseInfoRefs(infoTf.gameObject);
                SaveUniquePrefab(infoTf.gameObject, $"{PrefabFolder}/PauseInfoWindow.prefab");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog(
                "Capture pause prefabs",
                $"Saved under {PrefabFolder}.\n" +
                "Assign the three prefabs on Assets/Prefabs/Core/MatchController.prefab → MatchController " +
                "(Pause Menu Root Prefab, Pause Achievements Window Prefab, Pause Info Window Prefab).",
                "OK");
        }

        private static void EnsurePauseMainMenuRefs(GameObject root)
        {
            PauseMainMenuRefs r = root.GetComponent<PauseMainMenuRefs>();
            if (r == null)
            {
                r = root.AddComponent<PauseMainMenuRefs>();
            }

            r.PauseTitle = FindTmp(root.transform, "PauseTitle");
            r.PauseHint = FindTmp(root.transform, "PauseHint");
            r.ResumeButton = FindButton(root.transform, "ResumeButton");
            r.AchievementsFromPauseButton = FindButton(root.transform, "AchievementsFromPauseButton");
            r.InfoFromPauseButton = FindButton(root.transform, "InfoFromPauseButton");
            r.RetryFromPauseButton = FindButton(root.transform, "RetryFromPauseButton");
            r.TitleFromPauseButton = FindButton(root.transform, "TitleFromPauseButton");
        }

        private static void EnsurePauseAchievementsRefs(GameObject root)
        {
            PauseAchievementsWindowRefs r = root.GetComponent<PauseAchievementsWindowRefs>();
            if (r == null)
            {
                r = root.AddComponent<PauseAchievementsWindowRefs>();
            }

            r.CloseAchievementsButton = FindButton(root.transform, "CloseAchievementsButton");
            Transform block = root.transform.Find("AchievementScrollBlock");
            if (block != null)
            {
                Transform scroll = block.Find("AchievementScroll");
                if (scroll != null)
                {
                    Transform viewport = scroll.Find("Viewport");
                    if (viewport != null)
                    {
                        Transform content = viewport.Find("Content");
                        if (content != null)
                        {
                            r.AchievementBodyText = content.GetComponent<TMP_Text>();
                        }
                    }
                }
            }
        }

        private static void EnsurePauseInfoRefs(GameObject root)
        {
            PauseInfoWindowRefs r = root.GetComponent<PauseInfoWindowRefs>();
            if (r == null)
            {
                r = root.AddComponent<PauseInfoWindowRefs>();
            }

            r.CloseInfoButton = FindButton(root.transform, "CloseInfoButton");
            r.ItemsTabButton = FindButton(root.transform, "ItemsTabButton");
            r.UpgradesTabButton = FindButton(root.transform, "UpgradesTabButton");
            r.ItemsScroll = root.transform.Find("ItemsScroll")?.GetComponent<ScrollRect>();
            r.UpgradesScroll = root.transform.Find("UpgradesScroll")?.GetComponent<ScrollRect>();
            r.ScrollInput = root.GetComponent<PauseHelpScrollInput>();
            if (r.ScrollInput == null)
            {
                r.ScrollInput = root.AddComponent<PauseHelpScrollInput>();
            }

            if (r.ItemsScroll != null && r.UpgradesScroll != null)
            {
                r.ScrollInput.SetScrollRects(r.ItemsScroll, r.UpgradesScroll);
            }
        }

        private static TMP_Text FindTmp(Transform t, string name)
        {
            Transform c = t.Find(name);
            return c != null ? c.GetComponent<TMP_Text>() : null;
        }

        private static Button FindButton(Transform t, string name)
        {
            Transform c = t.Find(name);
            return c != null ? c.GetComponent<Button>() : null;
        }

        private static void SaveUniquePrefab(GameObject instance, string path)
        {
            // Do not use HideFlags.HideAndDontSave on the clone — PrefabUtility.SaveAsPrefabAsset refuses to save those objects.
            GameObject detached = Object.Instantiate(instance);
            detached.name = instance.name;
            ClearHideFlagsRecursive(detached);
            try
            {
                PrefabUtility.SaveAsPrefabAsset(detached, path);
            }
            finally
            {
                Object.DestroyImmediate(detached);
            }
        }

        private static void ClearHideFlagsRecursive(GameObject root)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                t.gameObject.hideFlags = HideFlags.None;
            }
        }

        private static Transform FindDeepChild(Transform root, string name)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                {
                    return t;
                }
            }

            return null;
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
    }
}
