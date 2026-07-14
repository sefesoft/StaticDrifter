using UnityEngine;
using UnityEngine.UI;
using TMPro;
using StaticDrift.Managers;

namespace StaticDrift.UI
{
    /// <summary>Builds title-screen menu hierarchies (used by <see cref="TitleScreenMenu"/> and editor prefab baking).</summary>
    public static class TitleMenuUiRuntimeFactory
    {
        public static GameObject CreateMainMenu(Transform parent, Sprite titleSprite)
        {
            GameObject mainPanel = new GameObject("MainMenu");
            mainPanel.transform.SetParent(parent, false);
            RectTransform mainRect = mainPanel.AddComponent<RectTransform>();
            mainRect.anchorMin = Vector2.zero;
            mainRect.anchorMax = Vector2.one;
            mainRect.offsetMin = Vector2.zero;
            mainRect.offsetMax = Vector2.zero;

            if (!TryCreateTitleImage(mainPanel.transform, titleSprite))
            {
                CreateText(mainPanel.transform, "Title", "STATIC DRIFT", new Vector2(0.5f, 0.72f), 92f);
            }

            TitleMainMenuRefs refs = mainPanel.AddComponent<TitleMainMenuRefs>();
            refs.StartButton = CreateMainMenuButton(mainPanel.transform, "StartButton", "Start", new Vector2(0.5f, 0.485f), null);
            refs.SettingsButton = CreateMainMenuButton(mainPanel.transform, "SettingsButton", "Settings", new Vector2(0.5f, 0.385f), null);
            refs.AchievementsButton = CreateMainMenuButton(mainPanel.transform, "AchievementsButton", "Achievements", new Vector2(0.5f, 0.285f), null);
            refs.LeaderboardButton = CreateMainMenuButton(mainPanel.transform, "LeaderboardButton", "Leaderboard", new Vector2(0.5f, 0.185f), null);
            refs.ExitButton = CreateMainMenuButton(mainPanel.transform, "ExitButton", "Exit", new Vector2(0.5f, 0.085f), null);
            return mainPanel;
        }

        public static GameObject CreateSettingsMenu(Transform settingsMenuRoot)
        {
            Transform contentRoot = CreateMenuContainer(settingsMenuRoot, "SettingsContainer", new Vector2(1200f, 900f)).transform;
            CreateText(contentRoot, "SettingsTitle", "SETTINGS", new Vector2(0.5f, 0.82f), 72f);

            CreateAlignedText(contentRoot, "MusicVolumeLabel", "Music Volume", new Vector2(0.10f, 0.68f), new Vector2(320f, 70f), 34f, TextAlignmentOptions.MidlineLeft);
            Slider musicSlider = CreateSlider(contentRoot, "MusicVolumeSlider", new Vector2(0.56f, 0.68f), 0f, 1f, GameSettings.MusicVolume);
            TMP_Text musicValue = CreateAlignedText(contentRoot, "MusicVolumeValue", "", new Vector2(0.92f, 0.68f), new Vector2(120f, 60f), 30f, TextAlignmentOptions.MidlineRight);

            CreateAlignedText(contentRoot, "SfxVolumeLabel", "SFX Volume", new Vector2(0.10f, 0.575f), new Vector2(320f, 70f), 34f, TextAlignmentOptions.MidlineLeft);
            Slider sfxSlider = CreateSlider(contentRoot, "SfxVolumeSlider", new Vector2(0.56f, 0.575f), 0f, 1f, GameSettings.SfxVolume);
            TMP_Text sfxValue = CreateAlignedText(contentRoot, "SfxVolumeValue", "", new Vector2(0.92f, 0.575f), new Vector2(120f, 60f), 30f, TextAlignmentOptions.MidlineRight);

            CreateAlignedText(contentRoot, "SensitivityLabel", "Rotation Sensitivity", new Vector2(0.10f, 0.47f), new Vector2(380f, 70f), 34f, TextAlignmentOptions.MidlineLeft);
            Slider sensitivitySlider = CreateSlider(contentRoot, "SensitivitySlider", new Vector2(0.56f, 0.47f), 0.5f, 2f, GameSettings.RotationSensitivity);
            TMP_Text sensValue = CreateAlignedText(contentRoot, "SensitivityValue", "", new Vector2(0.92f, 0.47f), new Vector2(120f, 60f), 30f, TextAlignmentOptions.MidlineRight);

            CreateAlignedText(contentRoot, "TouchRotationLabel", "Touch rotation", new Vector2(0.10f, 0.365f), new Vector2(380f, 70f), 34f, TextAlignmentOptions.MidlineLeft);
            Toggle touchToggle = CreateTouchRotationToggle(
                contentRoot,
                "TouchRotationToggle",
                new Vector2(0.56f, 0.365f),
                GameSettings.TouchRotationMode == TouchRotationMode.VirtualJoystick,
                null);
            TMP_Text touchValue = CreateAlignedText(contentRoot, "TouchRotationValue", "", new Vector2(0.92f, 0.365f), new Vector2(200f, 60f), 28f, TextAlignmentOptions.MidlineRight);

            CreateText(
                contentRoot,
                "ControlHints",
                "Controls\nKeyboard: W accelerate, A rotate left, D rotate right\nGamepad: L/R rotate, A accelerate\nTouch: use Settings above for L/R buttons vs joystick",
                new Vector2(0.5f, 0.19f),
                26f);

            Button back = CreateButton(contentRoot, "BackButton", "Back", new Vector2(0.5f, 0.075f), null);

            TitleSettingsMenuRefs refs = contentRoot.gameObject.AddComponent<TitleSettingsMenuRefs>();
            refs.MusicVolumeSlider = musicSlider;
            refs.SfxVolumeSlider = sfxSlider;
            refs.SensitivitySlider = sensitivitySlider;
            refs.TouchRotationToggle = touchToggle;
            refs.MusicVolumeValueText = musicValue;
            refs.SfxVolumeValueText = sfxValue;
            refs.SensitivityValueText = sensValue;
            refs.TouchRotationValueText = touchValue;
            refs.BackButton = back;
            return settingsMenuRoot.gameObject;
        }

        public static GameObject CreateLeaderboardMenu(Transform leaderboardPanelRoot)
        {
            Transform contentRoot = CreateMenuContainer(leaderboardPanelRoot, "LeaderboardContainer", new Vector2(980f, 760f)).transform;
            CreateText(contentRoot, "LeaderboardTitle", "LEADERBOARD", new Vector2(0.5f, 0.79f), 72f);
            CreateText(contentRoot, "LeaderboardSubtitle", "Current Top Scores", new Vector2(0.5f, 0.67f), 34f);
            TMP_Text scoresText = CreateText(contentRoot, "LeaderboardScores", BuildLeaderboardText(), new Vector2(0.5f, 0.42f), 34f);
            RectTransform scoresRect = scoresText.GetComponent<RectTransform>();
            if (scoresRect != null)
            {
                scoresRect.sizeDelta = new Vector2(860f, 360f);
            }

            scoresText.alignment = TextAlignmentOptions.Top;
            scoresText.enableWordWrapping = true;
            scoresText.overflowMode = TextOverflowModes.Overflow;

            Button back = CreateButton(contentRoot, "LeaderboardBackButton", "Back", new Vector2(0.5f, 0.10f), null);
            TitleLeaderboardMenuRefs refs = contentRoot.gameObject.AddComponent<TitleLeaderboardMenuRefs>();
            refs.LeaderboardScoresText = scoresText;
            refs.BackButton = back;
            return leaderboardPanelRoot.gameObject;
        }

        public static GameObject CreateAchievementsMenu(Transform achievementsPanelRoot)
        {
            EnsureAchievementsBackdrop(achievementsPanelRoot);

            bool mobile = UseTitleMobileLayout();
            Vector2 containerSize = mobile ? new Vector2(1240f, 920f) : new Vector2(1080f, 820f);
            Transform contentRoot = CreateMenuContainer(achievementsPanelRoot, "AchievementsContainer", containerSize).transform;
            contentRoot.SetAsLastSibling();

            float titleFont = mobile ? 86f : 72f;
            CreateText(contentRoot, "AchievementsTitle", "ACHIEVEMENTS", new Vector2(0.5f, 0.88f), titleFont);

            AchievementListPanel.Layout scrollLayout = new AchievementListPanel.Layout(
                new Vector2(0.03f, 0.05f),
                new Vector2(0.97f, 0.805f),
                Vector2.zero,
                Vector2.zero);

            float bodyFont = mobile ? 40f : 34f;
            int descRich = mobile ? 34 : 30;
            float sbw = mobile ? 44f : 38f;
            AchievementListPanel.Style achStyle = new AchievementListPanel.Style(bodyFont, descRich, sbw);
            TMP_Text achievementListText = AchievementListPanel.CreateScrollingBody(contentRoot, scrollLayout, achStyle);

            Button close = CreateAchievementsCloseXButton(contentRoot, null);

            TitleAchievementsMenuRefs refs = achievementsPanelRoot.gameObject.AddComponent<TitleAchievementsMenuRefs>();
            refs.AchievementListText = achievementListText;
            refs.CloseButton = close;
            return achievementsPanelRoot.gameObject;
        }

        private static string BuildLeaderboardText()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            System.Collections.Generic.List<int> scores = MatchController.GetSavedTopScores(10);
            if (scores == null || scores.Count == 0)
            {
                sb.Append("No scores recorded yet.\n");
                sb.Append("Finish a run to claim the first spot.");
                return sb.ToString();
            }

            for (int i = 0; i < scores.Count; i++)
            {
                sb.Append(i + 1);
                sb.Append(". ");
                sb.Append(scores[i]);
                sb.Append('\n');
            }

            return sb.ToString();
        }

        private static void EnsureAchievementsBackdrop(Transform achievementsPanel)
        {
            if (achievementsPanel == null)
            {
                return;
            }

            Transform existing = achievementsPanel.Find("AchievementsBackdrop");
            if (existing != null)
            {
                return;
            }

            GameObject go = new GameObject("AchievementsBackdrop");
            go.transform.SetParent(achievementsPanel, false);
            go.transform.SetAsFirstSibling();
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            Image img = go.AddComponent<Image>();
            img.color = new Color(0.02f, 0.03f, 0.06f, 0.94f);
            img.raycastTarget = true;
        }

        private static bool UseTitleMobileLayout()
        {
            int shortSide = Mathf.Min(Screen.width, Screen.height);
            return Application.isMobilePlatform || shortSide <= 1080;
        }

        private static bool TryCreateTitleImage(Transform parent, Sprite titleSprite)
        {
            if (titleSprite == null)
            {
                return false;
            }

            GameObject go = new GameObject("TitleImage");
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.70f);
            rect.anchorMax = new Vector2(0.5f, 0.70f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(920f, 340f);

            Image image = go.AddComponent<Image>();
            image.sprite = titleSprite;
            image.preserveAspect = true;
            image.color = Color.white;
            image.raycastTarget = false;
            return true;
        }

        private static GameObject CreateMenuContainer(Transform parent, string name, Vector2 size)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;

            Image image = go.AddComponent<Image>();
            image.color = new Color(0.04f, 0.06f, 0.11f, 0.97f);
            image.raycastTarget = true;
            Outline outline = go.AddComponent<Outline>();
            outline.effectColor = new Color(0.55f, 0.78f, 1f, 0.42f);
            outline.effectDistance = new Vector2(3f, -3f);
            outline.useGraphicAlpha = false;
            return go;
        }

        private static Button CreateAchievementsCloseXButton(Transform parent, UnityEngine.Events.UnityAction onClose)
        {
            bool mobile = Mathf.Min(Screen.width, Screen.height) <= 1080 || Application.isMobilePlatform;
            GameObject go = new GameObject("AchievementsCloseButton");
            go.transform.SetParent(parent, false);
            go.transform.SetAsLastSibling();
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            float inset = mobile ? 10f : 14f;
            float side = mobile ? 96f : 84f;
            rect.anchoredPosition = new Vector2(-inset, -inset);
            rect.sizeDelta = new Vector2(side, side);

            Image image = go.AddComponent<Image>();
            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;
            if (onClose != null)
            {
                button.onClick.AddListener(onClose);
            }

            go.AddComponent<UiSelectOnPointerEnter>();

            GameObject textGo = new GameObject("Label");
            textGo.transform.SetParent(go.transform, false);
            RectTransform textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(8f, 8f);
            textRect.offsetMax = new Vector2(-8f, -8f);
            TMP_Text text = textGo.AddComponent<TextMeshProUGUI>();
            text.text = "X";
            text.fontSize = mobile ? 52f : 46f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.88f, 0.97f, 1f, 1f);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            text.raycastTarget = false;
            PixelArtUiSkin.ApplyButtonStyle(button, image, text);
            return button;
        }

        private static Button CreateMainMenuButton(Transform parent, string name, string label, Vector2 anchor, UnityEngine.Events.UnityAction callback)
        {
            Button btn = CreateButton(parent, name, label, anchor, callback);
            RectTransform rt = btn.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.sizeDelta = new Vector2(400f, 78f);
            }

            TMP_Text labelText = btn.GetComponentInChildren<TMP_Text>();
            if (labelText != null)
            {
                labelText.fontSizeMax = 42f;
                labelText.fontSizeMin = 16f;
            }

            return btn;
        }

        private static TMP_Text CreateText(Transform parent, string name, string value, Vector2 anchor, float fontSize)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(880f, 160f);
            TMP_Text text = go.AddComponent<TextMeshProUGUI>();
            text.text = value;
            GameFontLibrary.Apply(text);
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.87f, 0.96f, 1f, 1f);
            text.enableWordWrapping = true;
            text.raycastTarget = false;
            GameFontLibrary.ApplyOutline(text, 0.24f, new Color(0.02f, 0.03f, 0.08f, 1f));
            return text;
        }

        private static TMP_Text CreateAlignedText(Transform parent, string name, string value, Vector2 anchor, Vector2 size, float fontSize, TextAlignmentOptions alignment)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            float pivotX = 0.5f;
            if (alignment == TextAlignmentOptions.MidlineLeft)
            {
                pivotX = 0f;
            }
            else if (alignment == TextAlignmentOptions.MidlineRight)
            {
                pivotX = 1f;
            }

            rect.pivot = new Vector2(pivotX, 0.5f);
            rect.sizeDelta = size;

            TMP_Text text = go.AddComponent<TextMeshProUGUI>();
            text.text = value;
            GameFontLibrary.Apply(text);
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Bold;
            text.alignment = alignment;
            text.color = new Color(0.9f, 0.97f, 1f, 1f);
            text.raycastTarget = false;
            GameFontLibrary.ApplyOutline(text, 0.22f, new Color(0.02f, 0.03f, 0.08f, 1f));
            return text;
        }

        private static Toggle CreateTouchRotationToggle(
            Transform parent,
            string name,
            Vector2 anchor,
            bool isOn,
            UnityEngine.Events.UnityAction<bool> onValueChanged)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(96f, 48f);

            Image bg = go.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.18f, 0.3f, 1f);
            bg.raycastTarget = true;

            GameObject checkGo = new GameObject("Checkmark");
            checkGo.transform.SetParent(go.transform, false);
            RectTransform checkRect = checkGo.AddComponent<RectTransform>();
            checkRect.anchorMin = new Vector2(0.12f, 0.18f);
            checkRect.anchorMax = new Vector2(0.88f, 0.82f);
            checkRect.offsetMin = Vector2.zero;
            checkRect.offsetMax = Vector2.zero;
            Image checkImg = checkGo.AddComponent<Image>();
            checkImg.color = new Color(0.35f, 0.82f, 1f, 0.95f);
            checkImg.raycastTarget = false;

            Toggle toggle = go.AddComponent<Toggle>();
            toggle.targetGraphic = bg;
            toggle.graphic = checkImg;
            toggle.isOn = isOn;
            toggle.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = toggle.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.95f, 0.98f, 1f, 1f);
            colors.pressedColor = new Color(0.85f, 0.92f, 1f, 1f);
            colors.selectedColor = new Color(0.95f, 0.98f, 1f, 1f);
            colors.fadeDuration = 0.06f;
            toggle.colors = colors;
            if (onValueChanged != null)
            {
                toggle.onValueChanged.AddListener(onValueChanged);
            }

            go.AddComponent<UiSelectOnPointerEnter>();
            return toggle;
        }

        private static Slider CreateSlider(Transform parent, string name, Vector2 anchor, float min, float max, float value)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(420f, 52f);

            Image bg = go.AddComponent<Image>();
            bg.sprite = CreateSliderFrameSprite();
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.89f, 0.95f, 1f, 1f);
            Slider slider = go.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            slider.targetGraphic = bg;
            ColorBlock sliderColors = slider.colors;
            sliderColors.normalColor = new Color(0.34f, 0.49f, 0.8f, 1f);
            sliderColors.highlightedColor = new Color(0.52f, 0.72f, 1f, 1f);
            sliderColors.selectedColor = new Color(0.6f, 0.82f, 1f, 1f);
            sliderColors.pressedColor = new Color(0.95f, 0.73f, 0.32f, 1f);
            sliderColors.colorMultiplier = 1f;
            sliderColors.fadeDuration = 0.05f;
            slider.colors = sliderColors;

            GameObject fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(go.transform, false);
            RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0f, 0f);
            fillAreaRect.anchorMax = new Vector2(1f, 1f);
            fillAreaRect.offsetMin = new Vector2(14f, 12f);
            fillAreaRect.offsetMax = new Vector2(-14f, -12f);

            Image fillAreaBg = fillArea.AddComponent<Image>();
            fillAreaBg.color = new Color(0.1f, 0.16f, 0.28f, 0.95f);
            fillAreaBg.raycastTarget = false;

            GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            RectTransform fillRect = fill.AddComponent<RectTransform>();
            fillRect.anchorMin = new Vector2(0f, 0f);
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            Image fillImage = fill.AddComponent<Image>();
            fillImage.color = new Color(0.31f, 0.84f, 1f, 0.95f);
            slider.fillRect = fillRect;

            GameObject handle = new GameObject("Handle");
            GameObject handleSlideArea = new GameObject("Handle Slide Area");
            handleSlideArea.transform.SetParent(go.transform, false);
            RectTransform handleSlideAreaRect = handleSlideArea.AddComponent<RectTransform>();
            handleSlideAreaRect.anchorMin = new Vector2(0f, 0f);
            handleSlideAreaRect.anchorMax = new Vector2(1f, 1f);
            handleSlideAreaRect.offsetMin = new Vector2(14f, 0f);
            handleSlideAreaRect.offsetMax = new Vector2(-14f, 0f);

            handle.transform.SetParent(handleSlideArea.transform, false);
            RectTransform handleRect = handle.AddComponent<RectTransform>();
            handleRect.anchorMin = new Vector2(0f, 0.5f);
            handleRect.anchorMax = new Vector2(0f, 0.5f);
            handleRect.pivot = new Vector2(0.5f, 0.5f);
            handleRect.sizeDelta = new Vector2(20f, 44f);
            Image handleImage = handle.AddComponent<Image>();
            handleImage.sprite = CreateSliderHandleSprite();
            handleImage.type = Image.Type.Sliced;
            handleImage.color = new Color(0.97f, 0.98f, 1f, 1f);
            slider.targetGraphic = handleImage;
            slider.handleRect = handleRect;

            slider.direction = Slider.Direction.LeftToRight;
            go.AddComponent<UiSelectOnPointerEnter>();
            return slider;
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 anchor, UnityEngine.Events.UnityAction callback)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(380f, 94f);

            Image image = go.AddComponent<Image>();
            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;
            if (callback != null)
            {
                button.onClick.AddListener(callback);
            }

            go.AddComponent<UiSelectOnPointerEnter>();

            GameObject textGo = new GameObject("Label");
            textGo.transform.SetParent(go.transform, false);
            RectTransform textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(20f, 10f);
            textRect.offsetMax = new Vector2(-20f, -10f);
            TMP_Text text = textGo.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 46f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.88f, 0.97f, 1f, 1f);
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.enableAutoSizing = true;
            text.fontSizeMin = 18f;
            text.fontSizeMax = 46f;
            text.lineSpacing = -8f;
            text.raycastTarget = false;
            PixelArtUiSkin.ApplyButtonStyle(button, image, text);
            return button;
        }

        private static Sprite CreateSliderFrameSprite()
        {
            return CreatePixelRectSprite(
                "pixel_slider_frame",
                32,
                16,
                new Color32(8, 16, 32, 255),
                new Color32(190, 226, 255, 255),
                new Color32(23, 40, 74, 255),
                new Color32(52, 83, 149, 255));
        }

        private static Sprite CreateSliderHandleSprite()
        {
            return CreatePixelRectSprite(
                "pixel_slider_handle",
                16,
                24,
                new Color32(11, 18, 35, 255),
                new Color32(232, 245, 255, 255),
                new Color32(34, 56, 92, 255),
                new Color32(109, 164, 235, 255));
        }

        private static Sprite CreatePixelRectSprite(string name, int width, int height, Color32 border, Color32 highlight, Color32 shadow, Color32 fill)
        {
            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color32 color = fill;
                    if (x == 0 || y == 0 || x == width - 1 || y == height - 1)
                    {
                        color = border;
                    }
                    else if (x == 1 || y == height - 2)
                    {
                        color = highlight;
                    }
                    else if (x == width - 2 || y == 1)
                    {
                        color = shadow;
                    }

                    texture.SetPixel(x, y, color);
                }
            }

            texture.Apply();
            return Sprite.Create(
                texture,
                new Rect(0f, 0f, width, height),
                new Vector2(0.5f, 0.5f),
                16f,
                0,
                SpriteMeshType.FullRect,
                new Vector4(4f, 4f, 4f, 4f));
        }
    }
}
