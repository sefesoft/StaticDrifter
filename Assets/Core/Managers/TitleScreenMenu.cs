using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem;
using TMPro;
using StaticDrift.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace StaticDrift.Managers
{
    public class TitleScreenMenu : MonoBehaviour
    {
        [SerializeField] private string _gameplaySceneName = "Gameplay";
        [SerializeField] private Sprite _backgroundSprite;
        [SerializeField] private Sprite _titleSprite;
        private GameObject _mainPanel;
        private GameObject _optionsPanel;
        private GameObject _leaderboardPanel;
        private TMP_Text _musicVolumeValueText;
        private TMP_Text _sfxVolumeValueText;
        private TMP_Text _sensitivityValueText;
        private Button _startButton;
        private Button _optionsButton;
        private Button _leaderboardButton;
        private Button _exitButton;
        private Button _backButton;
        private Button _leaderboardBackButton;
        private Slider _musicVolumeSlider;
        private Slider _sfxVolumeSlider;
        private UiSelectionKeepAlive _selectionKeepAlive;
        private MenuPanelPointerGuard _panelPointerGuard;

        private void Start()
        {
            GameSettings.Load();
            AudioManager.EnsureExists().PlayMusicForScene("TitleScreen");
            BuildMenu();
        }

        private void BuildMenu()
        {
            EnsureEventSystem();

            GameObject existingCanvas = GameObject.Find("TitleCanvas");
            if (existingCanvas != null)
            {
                Destroy(existingCanvas);
            }

            GameObject canvasGo = new GameObject("TitleCanvas");
            Canvas canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            canvasGo.AddComponent<GraphicRaycaster>();
            _selectionKeepAlive = canvasGo.AddComponent<UiSelectionKeepAlive>();

            GameObject panel = new GameObject("Panel");
            panel.transform.SetParent(canvasGo.transform, false);
            RectTransform panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0f, 0f, 0f, 0.32f);
            _panelPointerGuard = panel.AddComponent<MenuPanelPointerGuard>();

            CreateBackgroundImage(panel.transform);

            _mainPanel = new GameObject("MainMenu");
            _mainPanel.transform.SetParent(panel.transform, false);
            RectTransform mainRect = _mainPanel.AddComponent<RectTransform>();
            mainRect.anchorMin = Vector2.zero;
            mainRect.anchorMax = Vector2.one;
            mainRect.offsetMin = Vector2.zero;
            mainRect.offsetMax = Vector2.zero;

            _optionsPanel = new GameObject("OptionsMenu");
            _optionsPanel.transform.SetParent(panel.transform, false);
            RectTransform optionsRect = _optionsPanel.AddComponent<RectTransform>();
            optionsRect.anchorMin = Vector2.zero;
            optionsRect.anchorMax = Vector2.one;
            optionsRect.offsetMin = Vector2.zero;
            optionsRect.offsetMax = Vector2.zero;
            _optionsPanel.SetActive(false);

            _leaderboardPanel = new GameObject("LeaderboardMenu");
            _leaderboardPanel.transform.SetParent(panel.transform, false);
            RectTransform leaderboardRect = _leaderboardPanel.AddComponent<RectTransform>();
            leaderboardRect.anchorMin = Vector2.zero;
            leaderboardRect.anchorMax = Vector2.one;
            leaderboardRect.offsetMin = Vector2.zero;
            leaderboardRect.offsetMax = Vector2.zero;
            _leaderboardPanel.SetActive(false);

            BuildMainMenu();
            BuildOptionsMenu();
            BuildLeaderboardMenu();
            GameObject initial = _startButton != null ? _startButton.gameObject : null;
            SetSelected(initial);
            SyncMenuSelectionTargets(initial);
        }

        private void BuildMainMenu()
        {
            if (!CreateTitleImage(_mainPanel.transform))
            {
                CreateText(_mainPanel.transform, "Title", "STATIC DRIFT", new Vector2(0.5f, 0.72f), 92f);
            }

            _startButton = CreateButton(_mainPanel.transform, "StartButton", "Start", new Vector2(0.5f, 0.47f), () =>
            {
                AudioManager.EnsureExists().PlayUiConfirm();
                Time.timeScale = 1f;
                SceneManager.LoadScene(_gameplaySceneName);
            });

            _optionsButton = CreateButton(_mainPanel.transform, "OptionsButton", "Options", new Vector2(0.5f, 0.36f), () =>
            {
                AudioManager.EnsureExists().PlayUiConfirm();
                ShowOptions(true);
            });

            _leaderboardButton = CreateButton(_mainPanel.transform, "LeaderboardButton", "Leaderboard", new Vector2(0.5f, 0.25f), () =>
            {
                AudioManager.EnsureExists().PlayUiConfirm();
                ShowLeaderboard(true);
            });

            _exitButton = CreateButton(_mainPanel.transform, "ExitButton", "Exit", new Vector2(0.5f, 0.14f), ExitGame);
        }

        private void CreateBackgroundImage(Transform parent)
        {
            if (_backgroundSprite == null)
            {
                return;
            }

            GameObject go = new GameObject("BackgroundImage");
            go.transform.SetParent(parent, false);
            go.transform.SetAsFirstSibling();

            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image image = go.AddComponent<Image>();
            image.sprite = _backgroundSprite;
            image.preserveAspect = false;
            image.color = Color.white;
            image.raycastTarget = false;
        }

        private bool CreateTitleImage(Transform parent)
        {
            if (_titleSprite == null)
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
            image.sprite = _titleSprite;
            image.preserveAspect = true;
            image.color = Color.white;
            image.raycastTarget = false;
            return true;
        }

        private void BuildOptionsMenu()
        {
            Transform contentRoot = CreateMenuContainer(_optionsPanel.transform, "OptionsContainer", new Vector2(1160f, 760f)).transform;
            CreateText(contentRoot, "OptionsTitle", "OPTIONS", new Vector2(0.5f, 0.72f), 72f);

            CreateAlignedText(contentRoot, "MusicVolumeLabel", "Music Volume", new Vector2(0.10f, 0.60f), new Vector2(320f, 70f), 34f, TextAlignmentOptions.MidlineLeft);
            Slider musicSlider = CreateSlider(contentRoot, "MusicVolumeSlider", new Vector2(0.56f, 0.60f), 0f, 1f, GameSettings.MusicVolume);
            _musicVolumeSlider = musicSlider;
            _musicVolumeValueText = CreateAlignedText(contentRoot, "MusicVolumeValue", "", new Vector2(0.92f, 0.60f), new Vector2(120f, 60f), 30f, TextAlignmentOptions.MidlineRight);
            musicSlider.onValueChanged.AddListener(value =>
            {
                GameSettings.SetMusicVolume(value);
                RefreshOptionValueLabels();
                AudioManager.EnsureExists().PlayUiMove();
            });

            CreateAlignedText(contentRoot, "SfxVolumeLabel", "SFX Volume", new Vector2(0.10f, 0.47f), new Vector2(320f, 70f), 34f, TextAlignmentOptions.MidlineLeft);
            Slider sfxSlider = CreateSlider(contentRoot, "SfxVolumeSlider", new Vector2(0.56f, 0.47f), 0f, 1f, GameSettings.SfxVolume);
            _sfxVolumeSlider = sfxSlider;
            _sfxVolumeValueText = CreateAlignedText(contentRoot, "SfxVolumeValue", "", new Vector2(0.92f, 0.47f), new Vector2(120f, 60f), 30f, TextAlignmentOptions.MidlineRight);
            sfxSlider.onValueChanged.AddListener(value =>
            {
                GameSettings.SetSfxVolume(value);
                RefreshOptionValueLabels();
                AudioManager.EnsureExists().PlayUiMove();
            });

            CreateAlignedText(contentRoot, "SensitivityLabel", "Rotation Sensitivity", new Vector2(0.10f, 0.34f), new Vector2(360f, 70f), 34f, TextAlignmentOptions.MidlineLeft);
            Slider sensitivitySlider = CreateSlider(contentRoot, "SensitivitySlider", new Vector2(0.56f, 0.34f), 0.5f, 2f, GameSettings.RotationSensitivity);
            _sensitivityValueText = CreateAlignedText(contentRoot, "SensitivityValue", "", new Vector2(0.92f, 0.34f), new Vector2(120f, 60f), 30f, TextAlignmentOptions.MidlineRight);
            sensitivitySlider.onValueChanged.AddListener(value =>
            {
                GameSettings.SetRotationSensitivity(value);
                RefreshOptionValueLabels();
                AudioManager.EnsureExists().PlayUiMove();
            });

            CreateText(
                contentRoot,
                "ControlHints",
                "Controls\nKeyboard: W accelerate, A rotate left, D rotate right\nGamepad: L/R rotate, A accelerate",
                new Vector2(0.5f, 0.18f),
                28f);

            _backButton = CreateButton(contentRoot, "BackButton", "Back", new Vector2(0.5f, 0.08f), () =>
            {
                AudioManager.EnsureExists().PlayUiConfirm();
                ShowOptions(false);
            });
            RefreshOptionValueLabels();
        }

        private void BuildLeaderboardMenu()
        {
            Transform contentRoot = CreateMenuContainer(_leaderboardPanel.transform, "LeaderboardContainer", new Vector2(980f, 760f)).transform;
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

            _leaderboardBackButton = CreateButton(contentRoot, "LeaderboardBackButton", "Back", new Vector2(0.5f, 0.10f), () =>
            {
                AudioManager.EnsureExists().PlayUiConfirm();
                ShowLeaderboard(false);
            });
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
            image.color = new Color(0.03f, 0.06f, 0.12f, 0.88f);
            return go;
        }

        private void ShowOptions(bool show)
        {
            if (_mainPanel != null)
            {
                _mainPanel.SetActive(!show);
            }
            if (_optionsPanel != null)
            {
                _optionsPanel.SetActive(show);
            }
            if (_leaderboardPanel != null)
            {
                _leaderboardPanel.SetActive(false);
            }

            GameObject next = show ? (_musicVolumeSlider != null ? _musicVolumeSlider.gameObject : null) : (_startButton != null ? _startButton.gameObject : null);
            SetSelected(next);
            SyncMenuSelectionTargets(next);
        }

        private void ShowLeaderboard(bool show)
        {
            if (_mainPanel != null)
            {
                _mainPanel.SetActive(!show);
            }
            if (_optionsPanel != null)
            {
                _optionsPanel.SetActive(false);
            }
            if (_leaderboardPanel != null)
            {
                _leaderboardPanel.SetActive(show);
            }

            GameObject next = show ? (_leaderboardBackButton != null ? _leaderboardBackButton.gameObject : null) : (_startButton != null ? _startButton.gameObject : null);
            SetSelected(next);
            SyncMenuSelectionTargets(next);
        }

        private void SyncMenuSelectionTargets(GameObject defaultSelectable)
        {
            if (_selectionKeepAlive != null)
            {
                _selectionKeepAlive.DefaultSelection = defaultSelectable;
            }

            if (_panelPointerGuard != null)
            {
                _panelPointerGuard.DefaultSelection = defaultSelectable;
            }
        }

        private void RefreshOptionValueLabels()
        {
            if (_musicVolumeValueText != null)
            {
                _musicVolumeValueText.text = Mathf.RoundToInt(GameSettings.MusicVolume * 100f) + "%";
            }

            if (_sfxVolumeValueText != null)
            {
                _sfxVolumeValueText.text = Mathf.RoundToInt(GameSettings.SfxVolume * 100f) + "%";
            }
            if (_sensitivityValueText != null)
            {
                _sensitivityValueText.text = GameSettings.RotationSensitivity.ToString("0.00") + "x";
            }
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            GameObject go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<InputSystemUIInputModule>();
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
            button.onClick.AddListener(callback);
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

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Gamepad gamepad = Gamepad.current;

            bool backPressed = false;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                backPressed = true;
            }
            if (gamepad != null && gamepad.buttonEast.wasPressedThisFrame)
            {
                backPressed = true;
            }

            if (backPressed && _optionsPanel != null && _optionsPanel.activeSelf)
            {
                ShowOptions(false);
            }
            else if (backPressed && _leaderboardPanel != null && _leaderboardPanel.activeSelf)
            {
                ShowLeaderboard(false);
            }
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

        private static void ExitGame()
        {
            Application.Quit();
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#endif
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

        private static void SetSelected(GameObject selectedObject)
        {
            if (selectedObject == null)
            {
                return;
            }

            EventSystem es = EventSystem.current;
            if (es == null)
            {
                return;
            }

            es.firstSelectedGameObject = selectedObject;
            es.SetSelectedGameObject(selectedObject);
        }
    }
}
