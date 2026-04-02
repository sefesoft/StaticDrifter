using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using StaticDrift.UI;

namespace StaticDrift.Editor
{
    public static class CreateGameplayHUD
    {
        private const string MenuPath = "Static Drift/Create Gameplay HUD in Scene";
        private const string AddToGameplayPath = "Static Drift/Add Gameplay HUD to Gameplay Scene";
        private const string GameplayScenePath = "Assets/Scenes/Gameplay.unity";

        [MenuItem(AddToGameplayPath)]
        public static void AddToGameplayScene()
        {
            if (!AssetDatabase.LoadAssetAtPath<SceneAsset>(GameplayScenePath))
            {
                Debug.LogWarning("Gameplay scene not found at " + GameplayScenePath);
                return;
            }
            Scene active = SceneManager.GetActiveScene();
            bool needClose = false;
            if (active.path != GameplayScenePath)
            {
                needClose = true;
                if (!EditorSceneManager.OpenScene(GameplayScenePath).IsValid())
                {
                    Debug.LogError("Could not open Gameplay scene.");
                    return;
                }
            }
            if (Object.FindFirstObjectByType<GameplayHUD>() != null)
            {
                Debug.Log("Gameplay HUD already exists in scene.");
                if (needClose && active.path != null)
                    EditorSceneManager.OpenScene(active.path);
                return;
            }
            CreateHUDInternal();
            EditorSceneManager.SaveOpenScenes();
            if (needClose && active.path != null)
                EditorSceneManager.OpenScene(active.path);
        }

        [MenuItem(MenuPath)]
        public static void CreateHUD()
        {
            CreateHUDInternal();
        }

        private static void CreateHUDInternal()
        {
            GameObject canvasGo = new GameObject("GameplayHUD_Canvas");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasGo.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);
            canvasGo.GetComponent<CanvasScaler>().matchWidthOrHeight = 0.5f;
            canvasGo.AddComponent<GraphicRaycaster>();

            GameObject hudGo = new GameObject("GameplayHUD");
            hudGo.transform.SetParent(canvasGo.transform, false);

            RectTransform hudRect = hudGo.AddComponent<RectTransform>();
            hudRect.anchorMin = Vector2.zero;
            hudRect.anchorMax = Vector2.one;
            hudRect.offsetMin = Vector2.zero;
            hudRect.offsetMax = Vector2.zero;

            GameplayHUD gameplayHUD = hudGo.AddComponent<GameplayHUD>();

            GameObject timerGo = new GameObject("Timer");
            timerGo.transform.SetParent(hudGo.transform, false);
            RectTransform timerRect = timerGo.AddComponent<RectTransform>();
            timerRect.anchorMin = new Vector2(0.5f, 1f);
            timerRect.anchorMax = new Vector2(0.5f, 1f);
            timerRect.pivot = new Vector2(0.5f, 1f);
            timerRect.anchoredPosition = new Vector2(0f, -40f);
            timerRect.sizeDelta = new Vector2(320f, 56f);
            TMP_Text timerText = timerGo.AddComponent<TextMeshProUGUI>();
            timerText.text = "00:00";
            timerText.fontSize = 42f;
            timerText.alignment = TextAlignmentOptions.Center;
            timerText.color = Color.white;

            GameObject statsGo = new GameObject("PlayerStats");
            statsGo.transform.SetParent(hudGo.transform, false);
            RectTransform statsRect = statsGo.AddComponent<RectTransform>();
            statsRect.anchorMin = new Vector2(0.5f, 0f);
            statsRect.anchorMax = new Vector2(0.5f, 0f);
            statsRect.pivot = new Vector2(0.5f, 0f);
            statsRect.anchoredPosition = new Vector2(0f, 60f);
            statsRect.sizeDelta = new Vector2(360f, 64f);
            TMP_Text statsText = statsGo.AddComponent<TextMeshProUGUI>();
            statsText.text = "HP: —";
            statsText.fontSize = 32f;
            statsText.alignment = TextAlignmentOptions.Center;
            statsText.color = Color.white;

            SerializedObject so = new SerializedObject(gameplayHUD);
            so.FindProperty("_timerText").objectReferenceValue = timerText;
            so.FindProperty("_playerStatsText").objectReferenceValue = statsText;
            so.ApplyModifiedPropertiesWithoutUndo();

            Undo.RegisterCreatedObjectUndo(canvasGo, "Create Gameplay HUD");
            Selection.activeGameObject = canvasGo;
        }
    }
}
