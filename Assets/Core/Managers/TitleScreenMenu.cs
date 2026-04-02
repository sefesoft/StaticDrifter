using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem;
using TMPro;

namespace StaticDrift.Managers
{
    public class TitleScreenMenu : MonoBehaviour
    {
        [SerializeField] private string _gameplaySceneName = "Gameplay";
        private GameObject _mainPanel;
        private GameObject _optionsPanel;
        private TMP_Text _volumeValueText;
        private TMP_Text _sensitivityValueText;
        private Button _startButton;
        private Button _optionsButton;
        private Button _backButton;
        private Slider _volumeSlider;

        private void Start()
        {
            GameSettings.Load();
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

            GameObject panel = new GameObject("Panel");
            panel.transform.SetParent(canvasGo.transform, false);
            RectTransform panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            Image panelImage = panel.AddComponent<Image>();
            panelImage.color = new Color(0.03f, 0.05f, 0.09f, 1f);

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

            BuildMainMenu();
            BuildOptionsMenu();
            SetSelected(_startButton != null ? _startButton.gameObject : null);
        }

        private void BuildMainMenu()
        {
            CreateText(_mainPanel.transform, "Title", "STATIC DRIFT", new Vector2(0.5f, 0.72f), 92f);
            CreateText(_mainPanel.transform, "Subtitle", "ASTEROIDS ROGUELIKE", new Vector2(0.5f, 0.64f), 36f);

            _startButton = CreateButton(_mainPanel.transform, "StartButton", "Start", new Vector2(0.5f, 0.44f), () =>
            {
                Time.timeScale = 1f;
                SceneManager.LoadScene(_gameplaySceneName);
            });

            _optionsButton = CreateButton(_mainPanel.transform, "OptionsButton", "Options", new Vector2(0.5f, 0.33f), () =>
            {
                ShowOptions(true);
            });
        }

        private void BuildOptionsMenu()
        {
            CreateText(_optionsPanel.transform, "OptionsTitle", "OPTIONS", new Vector2(0.5f, 0.72f), 72f);

            CreateText(_optionsPanel.transform, "VolumeLabel", "Master Volume", new Vector2(0.35f, 0.56f), 34f);
            Slider volumeSlider = CreateSlider(_optionsPanel.transform, "VolumeSlider", new Vector2(0.58f, 0.56f), 0f, 1f, GameSettings.MasterVolume);
            _volumeSlider = volumeSlider;
            _volumeValueText = CreateText(_optionsPanel.transform, "VolumeValue", "", new Vector2(0.83f, 0.56f), 30f);
            volumeSlider.onValueChanged.AddListener(value =>
            {
                GameSettings.SetMasterVolume(value);
                RefreshOptionValueLabels();
            });

            CreateText(_optionsPanel.transform, "SensitivityLabel", "Rotation Sensitivity", new Vector2(0.35f, 0.46f), 34f);
            Slider sensitivitySlider = CreateSlider(_optionsPanel.transform, "SensitivitySlider", new Vector2(0.58f, 0.46f), 0.5f, 2f, GameSettings.RotationSensitivity);
            _sensitivityValueText = CreateText(_optionsPanel.transform, "SensitivityValue", "", new Vector2(0.83f, 0.46f), 30f);
            sensitivitySlider.onValueChanged.AddListener(value =>
            {
                GameSettings.SetRotationSensitivity(value);
                RefreshOptionValueLabels();
            });

            CreateText(
                _optionsPanel.transform,
                "ControlHints",
                "Controls\nKeyboard: W accelerate, A rotate left, D rotate right\nGamepad: L/R rotate, A accelerate",
                new Vector2(0.5f, 0.30f),
                30f);

            _backButton = CreateButton(_optionsPanel.transform, "BackButton", "Back", new Vector2(0.5f, 0.14f), () => ShowOptions(false));
            RefreshOptionValueLabels();
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

            SetSelected(show ? (_volumeSlider != null ? _volumeSlider.gameObject : null) : (_startButton != null ? _startButton.gameObject : null));
        }

        private void RefreshOptionValueLabels()
        {
            if (_volumeValueText != null)
            {
                _volumeValueText.text = Mathf.RoundToInt(GameSettings.MasterVolume * 100f) + "%";
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
        }

        private static void SetSelected(GameObject selectedObject)
        {
            if (selectedObject == null)
            {
                return;
            }

            if (EventSystem.current == null)
            {
                return;
            }

            EventSystem.current.SetSelectedGameObject(selectedObject);
        }
    }
}
