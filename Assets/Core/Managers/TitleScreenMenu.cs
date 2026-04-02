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
            panelImage.color = new Color(0.03f, 0.05f, 0.09f, 1f);
            _panelPointerGuard = panel.AddComponent<MenuPanelPointerGuard>();

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
            CreateText(_mainPanel.transform, "Title", "STATIC DRIFT", new Vector2(0.5f, 0.72f), 92f);
            CreateText(_mainPanel.transform, "Subtitle", "ASTEROIDS ROGUELIKE", new Vector2(0.5f, 0.64f), 36f);

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

        private void BuildOptionsMenu()
        {
            CreateText(_optionsPanel.transform, "OptionsTitle", "OPTIONS", new Vector2(0.5f, 0.72f), 72f);

            CreateText(_optionsPanel.transform, "MusicVolumeLabel", "Music Volume", new Vector2(0.35f, 0.58f), 34f);
            Slider musicSlider = CreateSlider(_optionsPanel.transform, "MusicVolumeSlider", new Vector2(0.58f, 0.58f), 0f, 1f, GameSettings.MusicVolume);
            _musicVolumeSlider = musicSlider;
            _musicVolumeValueText = CreateText(_optionsPanel.transform, "MusicVolumeValue", "", new Vector2(0.83f, 0.58f), 30f);
            musicSlider.onValueChanged.AddListener(value =>
            {
                GameSettings.SetMusicVolume(value);
                RefreshOptionValueLabels();
                AudioManager.EnsureExists().PlayUiMove();
            });

            CreateText(_optionsPanel.transform, "SfxVolumeLabel", "SFX Volume", new Vector2(0.35f, 0.50f), 34f);
            Slider sfxSlider = CreateSlider(_optionsPanel.transform, "SfxVolumeSlider", new Vector2(0.58f, 0.50f), 0f, 1f, GameSettings.SfxVolume);
            _sfxVolumeSlider = sfxSlider;
            _sfxVolumeValueText = CreateText(_optionsPanel.transform, "SfxVolumeValue", "", new Vector2(0.83f, 0.50f), 30f);
            sfxSlider.onValueChanged.AddListener(value =>
            {
                GameSettings.SetSfxVolume(value);
                RefreshOptionValueLabels();
                AudioManager.EnsureExists().PlayUiMove();
            });

            CreateText(_optionsPanel.transform, "SensitivityLabel", "Rotation Sensitivity", new Vector2(0.35f, 0.42f), 34f);
            Slider sensitivitySlider = CreateSlider(_optionsPanel.transform, "SensitivitySlider", new Vector2(0.58f, 0.42f), 0.5f, 2f, GameSettings.RotationSensitivity);
            _sensitivityValueText = CreateText(_optionsPanel.transform, "SensitivityValue", "", new Vector2(0.83f, 0.42f), 30f);
            sensitivitySlider.onValueChanged.AddListener(value =>
            {
                GameSettings.SetRotationSensitivity(value);
                RefreshOptionValueLabels();
                AudioManager.EnsureExists().PlayUiMove();
            });

            CreateText(
                _optionsPanel.transform,
                "ControlHints",
                "Controls\nKeyboard: W accelerate, A rotate left, D rotate right\nGamepad: L/R rotate, A accelerate",
                new Vector2(0.5f, 0.28f),
                30f);

            _backButton = CreateButton(_optionsPanel.transform, "BackButton", "Back", new Vector2(0.5f, 0.14f), () =>
            {
                AudioManager.EnsureExists().PlayUiConfirm();
                ShowOptions(false);
            });
            RefreshOptionValueLabels();
        }

        private void BuildLeaderboardMenu()
        {
            CreateText(_leaderboardPanel.transform, "LeaderboardTitle", "LEADERBOARD", new Vector2(0.5f, 0.74f), 72f);
            TMP_Text scoresText = CreateText(_leaderboardPanel.transform, "LeaderboardScores", BuildLeaderboardText(), new Vector2(0.5f, 0.48f), 34f);
            RectTransform scoresRect = scoresText.GetComponent<RectTransform>();
            if (scoresRect != null)
            {
                scoresRect.sizeDelta = new Vector2(860f, 420f);
            }
            scoresText.alignment = TextAlignmentOptions.Top;
            scoresText.enableWordWrapping = true;
            scoresText.overflowMode = TextOverflowModes.Overflow;

            _leaderboardBackButton = CreateButton(_leaderboardPanel.transform, "LeaderboardBackButton", "Back", new Vector2(0.5f, 0.14f), () =>
            {
                AudioManager.EnsureExists().PlayUiConfirm();
                ShowLeaderboard(false);
            });
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
            rect.sizeDelta = new Vector2(1300f, 140f);
            TMP_Text text = go.AddComponent<TextMeshProUGUI>();
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.87f, 0.96f, 1f, 1f);
            text.raycastTarget = false;
            // Avoid ArgumentNullException when TMP material is not ready yet.
            if (text.fontSharedMaterial != null)
            {
                text.outlineWidth = 0.24f;
                text.outlineColor = new Color(0.02f, 0.03f, 0.08f, 1f);
            }
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
            rect.sizeDelta = new Vector2(420f, 36f);

            Image bg = go.AddComponent<Image>();
            bg.color = new Color(0.12f, 0.16f, 0.24f, 0.8f);
            Slider slider = go.AddComponent<Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = value;
            slider.targetGraphic = bg;
            ColorBlock sliderColors = slider.colors;
            sliderColors.normalColor = new Color(0.16f, 0.20f, 0.30f, 0.9f);
            sliderColors.highlightedColor = new Color(0.35f, 0.62f, 0.96f, 1f);
            sliderColors.selectedColor = new Color(0.45f, 0.78f, 1f, 1f);
            sliderColors.pressedColor = new Color(0.96f, 0.72f, 0.28f, 1f);
            sliderColors.colorMultiplier = 1f;
            sliderColors.fadeDuration = 0.05f;
            slider.colors = sliderColors;

            GameObject fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(go.transform, false);
            RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0f, 0f);
            fillAreaRect.anchorMax = new Vector2(1f, 1f);
            fillAreaRect.offsetMin = new Vector2(12f, 7f);
            fillAreaRect.offsetMax = new Vector2(-12f, -7f);

            GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            RectTransform fillRect = fill.AddComponent<RectTransform>();
            Image fillImage = fill.AddComponent<Image>();
            fillImage.color = new Color(0.2f, 0.72f, 1f, 0.9f);
            slider.fillRect = fillRect;

            GameObject handle = new GameObject("Handle");
            handle.transform.SetParent(go.transform, false);
            RectTransform handleRect = handle.AddComponent<RectTransform>();
            handleRect.sizeDelta = new Vector2(26f, 46f);
            Image handleImage = handle.AddComponent<Image>();
            handleImage.color = new Color(0.88f, 0.96f, 1f, 1f);
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
            image.color = new Color(0.1f, 0.19f, 0.36f, 0.95f);
            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(callback);
            ColorBlock colors = button.colors;
            colors.normalColor = new Color(0.14f, 0.24f, 0.42f, 0.96f);
            colors.highlightedColor = new Color(0.34f, 0.62f, 0.95f, 1f);
            colors.selectedColor = new Color(0.45f, 0.78f, 1f, 1f);
            colors.pressedColor = new Color(0.95f, 0.70f, 0.30f, 1f);
            colors.disabledColor = new Color(0.15f, 0.18f, 0.23f, 0.45f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.05f;
            button.colors = colors;
            go.AddComponent<UiSelectOnPointerEnter>();

            GameObject textGo = new GameObject("Label");
            textGo.transform.SetParent(go.transform, false);
            RectTransform textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            TMP_Text text = textGo.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 46f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.88f, 0.97f, 1f, 1f);
            text.raycastTarget = false;
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

            sb.Append("Current Top Scores\n\n");
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
