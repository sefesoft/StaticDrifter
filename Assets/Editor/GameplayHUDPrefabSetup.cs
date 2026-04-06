using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using StaticDrift.UI;

namespace StaticDrift.Editor
{
    /// <summary>
    /// Builds the GameplayHUD prefab hierarchy and assigns all GameplayHUD serialized references.
    /// </summary>
    public static class GameplayHUDPrefabSetup
    {
        private const string PrefabPath = "Assets/Prefabs/Gameplay/GameplayHUD.prefab";

        [MenuItem("Static Drift/Setup Gameplay HUD Prefab")]
        public static void SetupGameplayHudPrefab()
        {
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath))
            {
                Debug.LogError("GameplayHUD prefab not found at " + PrefabPath);
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                GameplayHUD hud = root.GetComponent<GameplayHUD>();
                if (hud == null)
                {
                    Debug.LogError("GameplayHUD component missing on prefab root.");
                    return;
                }

                Transform tr = root.transform;
                RectTransform cluster = EnsureTimerWaveCluster(tr);
                TMP_Text timerText = EnsureTimerUnderCluster(tr, cluster);
                TMP_Text waveText = EnsureWaveText(cluster);
                TMP_Text scoreText = EnsureScoreText(tr);
                TMP_Text buildText = EnsureBuildText(tr);
                RectTransform buildRow = EnsureBuildHudRow(tr, buildText);
                LoadoutSlotHudEntry[] slots = EnsureLoadoutSlots(buildRow);
                GameObject bossPanel = EnsureBossPanel(tr);
                Image bossFill = bossPanel.transform.Find("Fill")?.GetComponent<Image>();
                TMP_Text bossLabel = bossPanel.transform.Find("Label")?.GetComponent<TMP_Text>();
                GameObject hpPanel = EnsurePlayerHpPanel(tr);
                Transform hpTrack = hpPanel.transform.Find("Track");
                Image hpFill = hpTrack != null ? hpTrack.Find("Fill")?.GetComponent<Image>() : null;
                RectTransform hpFillRect = hpFill != null ? hpFill.rectTransform : null;
                TMP_Text hpLabel = hpPanel.transform.Find("Label")?.GetComponent<TMP_Text>();
                Button pauseButton = EnsurePauseButton(tr);
                GameObject rotateL = EnsureTouchZone(tr, "RotateLeftButton", new Vector2(0f, 0f), new Vector2(0.12f, 1f), "L");
                GameObject rotateR = EnsureTouchZone(tr, "RotateRightButton", new Vector2(0.12f, 0f), new Vector2(0.24f, 1f), "R");
                GameObject accel = EnsureTouchZone(tr, "AccelerateButton", new Vector2(0.78f, 0f), new Vector2(1f, 0.88f), "A");

                TMP_Text playerStats = tr.Find("PlayerStats")?.GetComponent<TMP_Text>();

                SerializedObject so = new SerializedObject(hud);
                so.FindProperty("_timerText").objectReferenceValue = timerText;
                so.FindProperty("_playerStatsText").objectReferenceValue = playerStats;
                so.FindProperty("_scoreText").objectReferenceValue = scoreText;
                so.FindProperty("_waveText").objectReferenceValue = waveText;
                so.FindProperty("_buildText").objectReferenceValue = buildText;
                SerializedProperty loadoutProp = so.FindProperty("_loadoutSlots");
                loadoutProp.arraySize = 4;
                for (int i = 0; i < 4; i++)
                {
                    SerializedProperty el = loadoutProp.GetArrayElementAtIndex(i);
                    if (slots != null && i < slots.Length && slots[i] != null)
                    {
                        el.FindPropertyRelative("Diamond").objectReferenceValue = slots[i].Diamond;
                        el.FindPropertyRelative("Letter").objectReferenceValue = slots[i].Letter;
                        el.FindPropertyRelative("Count").objectReferenceValue = slots[i].Count;
                    }
                }

                so.FindProperty("_bossPanel").objectReferenceValue = bossPanel;
                so.FindProperty("_bossHpFill").objectReferenceValue = bossFill;
                so.FindProperty("_bossHpText").objectReferenceValue = bossLabel;
                so.FindProperty("_playerHpPanel").objectReferenceValue = hpPanel;
                so.FindProperty("_playerHpFill").objectReferenceValue = hpFill;
                so.FindProperty("_playerHpFillRect").objectReferenceValue = hpFillRect;
                so.FindProperty("_playerHpText").objectReferenceValue = hpLabel;
                so.FindProperty("_pauseButton").objectReferenceValue = pauseButton;
                so.FindProperty("_rotateLeftButton").objectReferenceValue = rotateL;
                so.FindProperty("_rotateRightButton").objectReferenceValue = rotateR;
                so.FindProperty("_accelerateButton").objectReferenceValue = accel;
                so.ApplyModifiedPropertiesWithoutUndo();

                EditorUtility.SetDirty(root);
            }
            finally
            {
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("GameplayHUD prefab setup complete: " + PrefabPath);
        }

        private static bool UseMobileHudLayout()
        {
            int shortSide = Mathf.Min(Screen.width, Screen.height);
            return shortSide > 0 && shortSide <= 1080;
        }

        private static float GetBuildHudPauseHorizontalClearancePx()
        {
            return UseMobileHudLayout() ? 152f : 136f;
        }

        private static RectTransform EnsureTimerWaveCluster(Transform root)
        {
            Transform existing = root.Find("TimerWaveCluster");
            if (existing != null)
            {
                return existing.GetComponent<RectTransform>();
            }

            GameObject go = new GameObject("TimerWaveCluster", typeof(RectTransform));
            go.transform.SetParent(root, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -14f);
            rt.sizeDelta = new Vector2(440f, 108f);
            return rt;
        }

        private static TMP_Text EnsureTimerUnderCluster(Transform root, RectTransform cluster)
        {
            Transform timerTf = root.Find("Timer");
            if (timerTf == null)
            {
                timerTf = FindDeepChild(root, "Timer");
            }

            if (timerTf == null)
            {
                GameObject timerGo = new GameObject("Timer", typeof(RectTransform));
                timerTf = timerGo.transform;
                timerTf.SetParent(cluster, false);
                TMP_Text tmp = timerGo.AddComponent<TextMeshProUGUI>();
                tmp.text = "00:00";
                tmp.fontSize = 42f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;
                GameFontLibrary.Apply(tmp);
            }
            else
            {
                timerTf.SetParent(cluster, false);
            }

            RectTransform timerRt = timerTf.GetComponent<RectTransform>();
            timerRt.anchorMin = new Vector2(0.5f, 1f);
            timerRt.anchorMax = new Vector2(0.5f, 1f);
            timerRt.pivot = new Vector2(0.5f, 1f);
            timerRt.anchoredPosition = Vector2.zero;
            timerRt.sizeDelta = new Vector2(400f, 58f);
            return timerTf.GetComponent<TMP_Text>();
        }

        private static TMP_Text EnsureWaveText(RectTransform cluster)
        {
            Transform waveTf = cluster.transform.Find("WaveText");
            if (waveTf == null)
            {
                GameObject waveGo = new GameObject("WaveText", typeof(RectTransform));
                waveTf = waveGo.transform;
                waveTf.SetParent(cluster, false);
                TMP_Text tmp = waveGo.AddComponent<TextMeshProUGUI>();
                tmp.text = "WAVE 1";
                tmp.fontSize = 46f;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = Color.white;
                GameFontLibrary.Apply(tmp);
            }

            RectTransform waveRt = waveTf.GetComponent<RectTransform>();
            waveRt.anchorMin = new Vector2(0.5f, 1f);
            waveRt.anchorMax = new Vector2(0.5f, 1f);
            waveRt.pivot = new Vector2(0.5f, 1f);
            waveRt.anchoredPosition = new Vector2(0f, -56f);
            waveRt.sizeDelta = new Vector2(420f, 46f);
            return waveTf.GetComponent<TMP_Text>();
        }

        private static TMP_Text EnsureScoreText(Transform root)
        {
            Transform t = root.Find("ScoreText");
            if (t == null)
            {
                GameObject go = new GameObject("ScoreText", typeof(RectTransform));
                t = go.transform;
                t.SetParent(root, false);
                RectTransform rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.04f, 0.955f);
                rt.anchorMax = new Vector2(0.04f, 0.955f);
                rt.pivot = new Vector2(0f, 0.5f);
                rt.sizeDelta = new Vector2(520f, 86f);
                TMP_Text tmp = go.AddComponent<TextMeshProUGUI>();
                tmp.text = "SCORE 0";
                tmp.fontSize = 46f;
                tmp.alignment = TextAlignmentOptions.Left;
                tmp.color = Color.white;
                GameFontLibrary.Apply(tmp);
            }

            return t.GetComponent<TMP_Text>();
        }

        private static TMP_Text EnsureBuildText(Transform root)
        {
            Transform t = root.Find("BuildText");
            if (t == null)
            {
                GameObject go = new GameObject("BuildText", typeof(RectTransform));
                t = go.transform;
                t.SetParent(root, false);
                RectTransform rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.965f, 0.90f);
                rt.anchorMax = new Vector2(0.965f, 0.90f);
                rt.pivot = new Vector2(1f, 0.5f);
                rt.sizeDelta = new Vector2(760f, 132f);
                TMP_Text tmp = go.AddComponent<TextMeshProUGUI>();
                tmp.text = "BUILD";
                tmp.fontSize = 46f;
                tmp.alignment = TextAlignmentOptions.Right;
                tmp.color = Color.white;
                GameFontLibrary.Apply(tmp);
            }

            return t.GetComponent<TMP_Text>();
        }

        private static RectTransform EnsureBuildHudRow(Transform root, TMP_Text buildText)
        {
            Transform rowTf = root.Find("BuildHudRow");
            if (rowTf == null)
            {
                GameObject rowGo = new GameObject("BuildHudRow", typeof(RectTransform));
                rowTf = rowGo.transform;
                rowTf.SetParent(root, false);
                RectTransform rowRect = rowGo.GetComponent<RectTransform>();
                rowRect.anchorMin = new Vector2(0.965f, 0.90f);
                rowRect.anchorMax = new Vector2(0.965f, 0.90f);
                rowRect.pivot = new Vector2(1f, 0.5f);
                rowRect.sizeDelta = new Vector2(920f, 140f);
                Vector2 buildHome = buildText.rectTransform.anchoredPosition;
                rowRect.anchoredPosition = new Vector2(buildHome.x - GetBuildHudPauseHorizontalClearancePx(), buildHome.y);

                HorizontalLayoutGroup hlg = rowGo.AddComponent<HorizontalLayoutGroup>();
                hlg.childAlignment = TextAnchor.MiddleRight;
                hlg.spacing = 10f;
                hlg.childForceExpandWidth = false;
                hlg.childForceExpandHeight = false;
            }

            buildText.transform.SetParent(rowTf, false);
            LayoutElement buildLe = buildText.gameObject.GetComponent<LayoutElement>();
            if (buildLe == null)
            {
                buildLe = buildText.gameObject.AddComponent<LayoutElement>();
            }

            buildLe.preferredWidth = 118f;
            buildLe.minWidth = 96f;
            buildLe.flexibleWidth = 0f;

            RectTransform brt = buildText.rectTransform;
            brt.anchorMin = new Vector2(0f, 0.5f);
            brt.anchorMax = new Vector2(0f, 0.5f);
            brt.pivot = new Vector2(0f, 0.5f);
            brt.anchoredPosition = Vector2.zero;
            brt.sizeDelta = new Vector2(118f, 72f);

            return rowTf.GetComponent<RectTransform>();
        }

        private static LoadoutSlotHudEntry[] EnsureLoadoutSlots(RectTransform buildRow)
        {
            float iconSize = UseMobileHudLayout() ? 52f : 44f;
            var result = new LoadoutSlotHudEntry[4];
            for (int i = 0; i < 4; i++)
            {
                string slotName = "LoadoutSlot_" + i;
                Transform slotTf = buildRow.Find(slotName);
                if (slotTf == null)
                {
                    slotTf = CreateBuildTagSlot(buildRow, i, iconSize).transform;
                }

                Transform iconTf = slotTf.Find("Icon");
                Transform letterTf = iconTf != null ? iconTf.Find("Letter") : null;
                Transform countTf = slotTf.Find("Count");
                result[i] = new LoadoutSlotHudEntry
                {
                    Diamond = iconTf != null ? iconTf.GetComponent<Image>() : null,
                    Letter = letterTf != null ? letterTf.GetComponent<TMP_Text>() : null,
                    Count = countTf != null ? countTf.GetComponent<TMP_Text>() : null
                };
            }

            return result;
        }

        private static GameObject CreateBuildTagSlot(Transform parent, int slotIndex, float iconSize)
        {
            GameObject slot = new GameObject("LoadoutSlot_" + slotIndex, typeof(RectTransform));
            slot.transform.SetParent(parent, false);
            VerticalLayoutGroup vlg = slot.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.MiddleCenter;
            vlg.spacing = 2;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = false;

            LayoutElement slotLe = slot.AddComponent<LayoutElement>();
            slotLe.preferredWidth = iconSize + 22f;
            slotLe.minWidth = iconSize + 6f;

            GameObject iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(slot.transform, false);
            RectTransform iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.sizeDelta = new Vector2(iconSize, iconSize);
            LayoutElement iconLe = iconGo.AddComponent<LayoutElement>();
            iconLe.preferredWidth = iconSize;
            iconLe.preferredHeight = iconSize;
            Image iconImage = iconGo.AddComponent<Image>();
            iconImage.sprite = UpgradeHudVisuals.GetDiamondSprite();
            iconImage.color = new Color(0.32f, 0.36f, 0.42f, 0.55f);
            iconImage.preserveAspect = true;
            iconImage.raycastTarget = false;

            GameObject letterGo = new GameObject("Letter", typeof(RectTransform));
            letterGo.transform.SetParent(iconGo.transform, false);
            RectTransform letterRect = letterGo.GetComponent<RectTransform>();
            letterRect.anchorMin = Vector2.zero;
            letterRect.anchorMax = Vector2.one;
            letterRect.offsetMin = Vector2.zero;
            letterRect.offsetMax = Vector2.zero;
            TMP_Text letterTmp = letterGo.AddComponent<TextMeshProUGUI>();
            GameFontLibrary.Apply(letterTmp);
            letterTmp.fontSize = Mathf.Max(22f, iconSize * 0.58f);
            letterTmp.fontStyle = FontStyles.Bold;
            letterTmp.text = "?";
            letterTmp.color = new Color(0.04f, 0.05f, 0.08f, 1f);
            letterTmp.alignment = TextAlignmentOptions.Center;
            letterTmp.raycastTarget = false;

            GameObject countGo = new GameObject("Count", typeof(RectTransform));
            countGo.transform.SetParent(slot.transform, false);
            RectTransform countRt = countGo.GetComponent<RectTransform>();
            countRt.sizeDelta = new Vector2(iconSize + 16f, 28f);
            LayoutElement countLe = countGo.AddComponent<LayoutElement>();
            countLe.preferredHeight = 30f;
            TMP_Text countTmp = countGo.AddComponent<TextMeshProUGUI>();
            GameFontLibrary.Apply(countTmp);
            countTmp.text = "--";
            countTmp.fontSize = UseMobileHudLayout() ? 30f : 26f;
            countTmp.fontStyle = FontStyles.Bold;
            countTmp.alignment = TextAlignmentOptions.Center;
            countTmp.color = new Color(0.88f, 0.9f, 1f, 0.95f);
            countTmp.raycastTarget = false;

            return slot;
        }

        private static GameObject EnsureBossPanel(Transform root)
        {
            Transform t = root.Find("BossPanel");
            if (t != null)
            {
                t.gameObject.SetActive(false);
                return t.gameObject;
            }

            GameObject panel = new GameObject("BossPanel", typeof(RectTransform));
            panel.transform.SetParent(root, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.885f);
            rect.anchorMax = new Vector2(0.5f, 0.885f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(720f, 44f);

            Image bg = panel.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.04f, 0.05f, 0.92f);

            GameObject fillGo = new GameObject("Fill", typeof(RectTransform));
            fillGo.transform.SetParent(panel.transform, false);
            RectTransform fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = new Vector2(4f, 4f);
            fillRect.offsetMax = new Vector2(-4f, -4f);
            Image fillImage = fillGo.AddComponent<Image>();
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = 0;
            fillImage.fillAmount = 1f;
            fillImage.color = new Color(0.92f, 0.24f, 0.18f, 0.95f);

            GameObject labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(panel.transform, false);
            RectTransform labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            TMP_Text labelText = labelGo.AddComponent<TextMeshProUGUI>();
            GameFontLibrary.Apply(labelText);
            labelText.fontSize = 34f;
            labelText.fontStyle = FontStyles.Bold;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.color = new Color(1f, 0.92f, 0.9f, 1f);
            labelText.text = "BOSS";

            panel.SetActive(false);
            return panel;
        }

        private static GameObject EnsurePlayerHpPanel(Transform root)
        {
            Transform t = root.Find("PlayerHpPanel");
            if (t != null)
            {
                return t.gameObject;
            }

            GameObject panel = new GameObject("PlayerHpPanel", typeof(RectTransform));
            panel.transform.SetParent(root, false);
            RectTransform rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.07f);
            rect.anchorMax = new Vector2(0.5f, 0.07f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(420f, 50f);

            Image bg = panel.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.07f, 0.12f, 0.98f);
            Outline outline = panel.AddComponent<Outline>();
            outline.effectColor = new Color(0.7f, 0.9f, 1f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = false;

            GameObject trackGo = new GameObject("Track", typeof(RectTransform));
            trackGo.transform.SetParent(panel.transform, false);
            RectTransform trackRect = trackGo.GetComponent<RectTransform>();
            trackRect.anchorMin = Vector2.zero;
            trackRect.anchorMax = Vector2.one;
            trackRect.offsetMin = new Vector2(4f, 4f);
            trackRect.offsetMax = new Vector2(-4f, -4f);
            Image trackImage = trackGo.AddComponent<Image>();
            trackImage.color = new Color(0.09f, 0.15f, 0.22f, 1f);
            trackImage.raycastTarget = false;

            GameObject fillGo = new GameObject("Fill", typeof(RectTransform));
            fillGo.transform.SetParent(trackGo.transform, false);
            RectTransform fillRect = fillGo.GetComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fillRect.pivot = new Vector2(0f, 0.5f);
            Image fillImage = fillGo.AddComponent<Image>();
            fillImage.type = Image.Type.Simple;
            fillImage.fillAmount = 1f;
            fillImage.color = new Color(0.2f, 0.78f, 0.42f, 0.95f);
            fillImage.raycastTarget = false;

            GameObject labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(panel.transform, false);
            RectTransform labelRect = labelGo.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            TMP_Text labelText = labelGo.AddComponent<TextMeshProUGUI>();
            GameFontLibrary.Apply(labelText);
            labelText.fontSize = 32f;
            labelText.fontStyle = FontStyles.Bold;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.color = new Color(0.95f, 0.98f, 1f, 1f);
            labelText.text = "HP: 100 / 100";
            labelText.raycastTarget = false;

            return panel;
        }

        private static Button EnsurePauseButton(Transform root)
        {
            Transform t = root.Find("PauseButton");
            if (t != null)
            {
                return t.GetComponent<Button>();
            }

            GameObject buttonGo = new GameObject("PauseButton", typeof(RectTransform));
            buttonGo.transform.SetParent(root, false);
            RectTransform rect = buttonGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            float pauseInsetX = UseMobileHudLayout() ? -40f : -36f;
            float pauseInsetY = UseMobileHudLayout() ? -178f : -158f;
            rect.anchoredPosition = new Vector2(pauseInsetX, pauseInsetY);
            rect.sizeDelta = new Vector2(86f, 86f);

            Image image = buttonGo.AddComponent<Image>();
            Button button = buttonGo.AddComponent<Button>();
            button.targetGraphic = image;

            GameObject textGo = new GameObject("Label", typeof(RectTransform));
            textGo.transform.SetParent(buttonGo.transform, false);
            RectTransform textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TMP_Text text = textGo.AddComponent<TextMeshProUGUI>();
            text.text = "II";
            text.fontSize = 34f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.88f, 0.97f, 1f, 1f);
            text.raycastTarget = false;
            GameFontLibrary.Apply(text);

            PixelArtUiSkin.ApplyButtonStyle(button, image, text);
            return button;
        }

        private static GameObject EnsureTouchZone(Transform root, string objectName, Vector2 anchorMin, Vector2 anchorMax, string label)
        {
            Transform t = root.Find(objectName);
            if (t != null)
            {
                EnsureTouchZoneVisuals(t.gameObject, label);
                return t.gameObject;
            }

            GameObject buttonGo = new GameObject(objectName, typeof(RectTransform));
            buttonGo.transform.SetParent(root, false);

            RectTransform rect = buttonGo.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image bg = buttonGo.AddComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.001f);

            GameObject visualGo = new GameObject("Visual", typeof(RectTransform));
            visualGo.transform.SetParent(buttonGo.transform, false);
            RectTransform visualRect = visualGo.GetComponent<RectTransform>();
            visualRect.anchorMin = new Vector2(0.5f, 0f);
            visualRect.anchorMax = new Vector2(0.5f, 0f);
            visualRect.pivot = new Vector2(0.5f, 0f);
            visualRect.anchoredPosition = new Vector2(0f, 34f);
            visualRect.sizeDelta = new Vector2(150f, 150f);

            Image visualBg = visualGo.AddComponent<Image>();
            visualBg.sprite = GetCircleButtonSprite();
            visualBg.type = Image.Type.Sliced;
            visualBg.color = new Color(0.12f, 0.16f, 0.25f, 0.45f);
            visualBg.raycastTarget = false;

            GameObject textGo = new GameObject("Label", typeof(RectTransform));
            textGo.transform.SetParent(visualGo.transform, false);
            RectTransform textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            TMP_Text tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 44f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.76f, 0.93f, 1f, 0.95f);
            tmp.raycastTarget = false;
            GameFontLibrary.Apply(tmp);

            return buttonGo;
        }

        private static void EnsureTouchZoneVisuals(GameObject buttonGo, string label)
        {
            Transform visualTf = buttonGo.transform.Find("Visual");
            if (visualTf == null)
            {
                return;
            }

            Image visualBg = visualTf.GetComponent<Image>();
            if (visualBg != null && visualBg.sprite == null)
            {
                visualBg.sprite = GetCircleButtonSprite();
                visualBg.type = Image.Type.Sliced;
            }

            Transform labelTf = visualTf.Find("Label");
            if (labelTf != null)
            {
                TMP_Text tmp = labelTf.GetComponent<TMP_Text>();
                if (tmp != null && string.IsNullOrEmpty(tmp.text))
                {
                    tmp.text = label;
                }
            }
        }

        private static Sprite _editorCircleSprite;

        private static Sprite GetCircleButtonSprite()
        {
            if (_editorCircleSprite != null)
            {
                return _editorCircleSprite;
            }

            const int size = 128;
            const float radius = 60f;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;

            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - center.x;
                    float dy = y - center.y;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = 0f;
                    if (dist <= radius)
                    {
                        float t = Mathf.InverseLerp(radius, radius - 6f, dist);
                        alpha = Mathf.Clamp01(t);
                    }

                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            _editorCircleSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return _editorCircleSprite;
        }

        private static Transform FindDeepChild(Transform parent, string childName)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform c = parent.GetChild(i);
                if (c.name == childName)
                {
                    return c;
                }

                Transform nested = FindDeepChild(c, childName);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }
    }
}
