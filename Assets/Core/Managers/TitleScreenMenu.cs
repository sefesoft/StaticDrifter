using System.Collections;
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

        [Header("Menu prefabs (Assets/Prefabs/UI — bake via Static Drift menu)")]
        [SerializeField] private GameObject _mainMenuPrefab;
        [SerializeField] private GameObject _settingsMenuPrefab;
        [SerializeField] private GameObject _leaderboardMenuPrefab;
        [SerializeField] private GameObject _achievementsMenuPrefab;

        private GameObject _mainPanel;
        /// <summary>Root of the title settings menu (prefab <c>TitleSettingsMenu</c> or runtime-built).</summary>
        private GameObject _settingsPanel;
        private GameObject _leaderboardPanel;
        private GameObject _achievementsPanel;
        private TitleMainMenuRefs _mainRefs;
        private TitleSettingsMenuRefs _settingsRefs;
        private TitleLeaderboardMenuRefs _leaderboardRefs;
        private TitleAchievementsMenuRefs _achievementsRefs;
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
            // Fullscreen panel Image must not steal raycasts — child buttons/sliders need to receive clicks.
            panelImage.raycastTarget = false;
            _panelPointerGuard = panel.AddComponent<MenuPanelPointerGuard>();

            CreateBackgroundImage(panel.transform);

            _mainPanel = InstantiateOrBuildMainMenu(panel.transform);
            _settingsPanel = InstantiateOrBuildSettingsMenu(panel.transform);
            _leaderboardPanel = InstantiateOrBuildLeaderboardMenu(panel.transform);
            _achievementsPanel = InstantiateOrBuildAchievementsMenu(panel.transform);

            if (_mainMenuPrefab != null && _settingsMenuPrefab != null && _mainMenuPrefab == _settingsMenuPrefab)
            {
                Debug.LogError(
                    "[TitleScreenMenu] The main menu and settings menu prefab slots reference the same asset. " +
                    "The second instance draws on top but only the first gets click listeners — assign TitleSettingsMenu.prefab to the settings slot (TitleScreen scene → TitleScreenMenu).",
                    this);
            }

            // Keep submenus active until refs are resolved. Deactivating the root before GetComponentInChildren
            // can leave TitleSettingsMenuRefs (and similar) unresolved — same symptom as a one-frame delay.
            CacheRefsAndWire();

            _settingsPanel.SetActive(false);
            _leaderboardPanel.SetActive(false);
            _achievementsPanel.SetActive(false);

            GameObject initial = _mainRefs != null && _mainRefs.StartButton != null ? _mainRefs.StartButton.gameObject : null;
            StartCoroutine(DeferInitialSelection(initial));
        }

        private IEnumerator DeferInitialSelection(GameObject initial)
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            SetSelected(initial);
            SyncMenuSelectionTargets(initial);
        }

        private GameObject InstantiateOrBuildMainMenu(Transform panelTransform)
        {
            if (_mainMenuPrefab != null)
            {
                return Instantiate(_mainMenuPrefab, panelTransform);
            }

            return TitleMenuUiRuntimeFactory.CreateMainMenu(panelTransform, _titleSprite);
        }

        private GameObject InstantiateOrBuildSettingsMenu(Transform panelTransform)
        {
            if (_settingsMenuPrefab != null)
            {
                return Instantiate(_settingsMenuPrefab, panelTransform);
            }

            GameObject go = new GameObject("SettingsMenu");
            go.transform.SetParent(panelTransform, false);
            RectTransform r = go.AddComponent<RectTransform>();
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
            TitleMenuUiRuntimeFactory.CreateSettingsMenu(go.transform);
            return go;
        }

        private GameObject InstantiateOrBuildLeaderboardMenu(Transform panelTransform)
        {
            if (_leaderboardMenuPrefab != null)
            {
                return Instantiate(_leaderboardMenuPrefab, panelTransform);
            }

            GameObject go = new GameObject("LeaderboardMenu");
            go.transform.SetParent(panelTransform, false);
            RectTransform r = go.AddComponent<RectTransform>();
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
            TitleMenuUiRuntimeFactory.CreateLeaderboardMenu(go.transform);
            return go;
        }

        private GameObject InstantiateOrBuildAchievementsMenu(Transform panelTransform)
        {
            if (_achievementsMenuPrefab != null)
            {
                return Instantiate(_achievementsMenuPrefab, panelTransform);
            }

            GameObject go = new GameObject("AchievementsMenu");
            go.transform.SetParent(panelTransform, false);
            RectTransform r = go.AddComponent<RectTransform>();
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero;
            r.offsetMax = Vector2.zero;
            TitleMenuUiRuntimeFactory.CreateAchievementsMenu(go.transform);
            return go;
        }

        private void CacheRefsAndWire()
        {
            EnsureTitleSettingsMenuRefs();

            _leaderboardRefs = _leaderboardPanel.GetComponentInChildren<TitleLeaderboardMenuRefs>(true);
            _achievementsRefs = _achievementsPanel.GetComponent<TitleAchievementsMenuRefs>()
                ?? _achievementsPanel.GetComponentInChildren<TitleAchievementsMenuRefs>(true);

            EnsureTitleMainMenuRefsAndButtonsFromHierarchy();

            WireMainMenu();
            WireSettingsMenu();
            WireLeaderboardMenu();
            WireAchievementsMenu();
        }

        /// <summary>
        /// Ensures <see cref="TitleMainMenuRefs"/> exists and button fields point at the actual Button instances
        /// in this hierarchy. Always re-resolves by name so stale/broken serialized prefab references cannot leave
        /// listeners on the wrong object (symptom: button animates on press but onClick does nothing).
        /// </summary>
        private void EnsureTitleMainMenuRefsAndButtonsFromHierarchy()
        {
            if (_mainPanel == null)
            {
                return;
            }

            _mainRefs = _mainPanel.GetComponent<TitleMainMenuRefs>()
                ?? _mainPanel.GetComponentInChildren<TitleMainMenuRefs>(true);
            if (_mainRefs == null)
            {
                _mainRefs = _mainPanel.gameObject.AddComponent<TitleMainMenuRefs>();
            }

            Transform root = _mainPanel.transform;
            _mainRefs.StartButton = FindTitleMenuButton(root, "StartButton");
            _mainRefs.SettingsButton = FindTitleMenuButton(root, "SettingsButton");
            _mainRefs.AchievementsButton = FindTitleMenuButton(root, "AchievementsButton");
            _mainRefs.LeaderboardButton = FindTitleMenuButton(root, "LeaderboardButton");
            _mainRefs.ExitButton = FindTitleMenuButton(root, "ExitButton");
        }

        private static Button FindTitleMenuButton(Transform mainMenuRoot, string objectName)
        {
            if (mainMenuRoot == null || string.IsNullOrEmpty(objectName))
            {
                return null;
            }

            Transform direct = mainMenuRoot.Find(objectName);
            if (direct != null)
            {
                Button b = direct.GetComponent<Button>();
                if (b != null)
                {
                    return b;
                }
            }

            foreach (Transform t in mainMenuRoot.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == objectName)
                {
                    Button b = t.GetComponent<Button>();
                    if (b != null)
                    {
                        return b;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Resolves <see cref="TitleSettingsMenuRefs"/> while the settings hierarchy is still active, then fills
        /// fields from names so prefab serialization cannot point at wrong instances.
        /// </summary>
        private void EnsureTitleSettingsMenuRefs()
        {
            if (_settingsPanel == null)
            {
                return;
            }

            Transform panelRoot = _settingsPanel.transform;
            _settingsRefs = _settingsPanel.GetComponent<TitleSettingsMenuRefs>()
                ?? _settingsPanel.GetComponentInChildren<TitleSettingsMenuRefs>(true);

            if (_settingsRefs == null)
            {
                Transform container = FindChildTransformByName(panelRoot, "SettingsContainer");
                if (container != null)
                {
                    _settingsRefs = container.GetComponent<TitleSettingsMenuRefs>();
                    if (_settingsRefs == null)
                    {
                        _settingsRefs = container.gameObject.AddComponent<TitleSettingsMenuRefs>();
                    }
                }
            }

            if (_settingsRefs == null)
            {
                return;
            }

            Transform scope = _settingsRefs.transform;
            _settingsRefs.MusicVolumeSlider = FindNamedComponent<Slider>(scope, "MusicVolumeSlider");
            _settingsRefs.SfxVolumeSlider = FindNamedComponent<Slider>(scope, "SfxVolumeSlider");
            _settingsRefs.SensitivitySlider = FindNamedComponent<Slider>(scope, "SensitivitySlider");
            _settingsRefs.TouchRotationToggle = FindNamedComponent<Toggle>(scope, "TouchRotationToggle");
            _settingsRefs.MusicVolumeValueText = FindNamedComponent<TMP_Text>(scope, "MusicVolumeValue");
            _settingsRefs.SfxVolumeValueText = FindNamedComponent<TMP_Text>(scope, "SfxVolumeValue");
            _settingsRefs.SensitivityValueText = FindNamedComponent<TMP_Text>(scope, "SensitivityValue");
            _settingsRefs.TouchRotationValueText = FindNamedComponent<TMP_Text>(scope, "TouchRotationValue");
            _settingsRefs.BackButton = FindNamedComponent<Button>(scope, "BackButton");
        }

        private static Transform FindChildTransformByName(Transform root, string objectName)
        {
            if (root == null || string.IsNullOrEmpty(objectName))
            {
                return null;
            }

            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == objectName)
                {
                    return t;
                }
            }

            return null;
        }

        private static T FindNamedComponent<T>(Transform scope, string objectName) where T : Component
        {
            if (scope == null || string.IsNullOrEmpty(objectName))
            {
                return null;
            }

            foreach (Transform t in scope.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == objectName)
                {
                    return t.GetComponent<T>();
                }
            }

            return null;
        }

        private void WireMainMenu()
        {
            if (_mainRefs == null)
            {
                return;
            }

            if (_mainRefs.StartButton != null)
            {
                _mainRefs.StartButton.onClick.RemoveAllListeners();
                _mainRefs.StartButton.onClick.AddListener(() =>
                {
                    AudioManager.EnsureExists().PlayUiConfirm();
                    Time.timeScale = 1f;
                    SceneManager.LoadScene(_gameplaySceneName);
                });
            }

            if (_mainRefs.SettingsButton != null)
            {
                _mainRefs.SettingsButton.onClick.RemoveAllListeners();
                _mainRefs.SettingsButton.onClick.AddListener(() =>
                {
                    AudioManager.EnsureExists().PlayUiConfirm();
                    ShowOptions(true);
                });
            }

            if (_mainRefs.AchievementsButton != null)
            {
                _mainRefs.AchievementsButton.onClick.RemoveAllListeners();
                _mainRefs.AchievementsButton.onClick.AddListener(() =>
                {
                    AudioManager.EnsureExists().PlayUiConfirm();
                    ShowAchievements(true);
                });
            }

            if (_mainRefs.LeaderboardButton != null)
            {
                _mainRefs.LeaderboardButton.onClick.RemoveAllListeners();
                _mainRefs.LeaderboardButton.onClick.AddListener(() =>
                {
                    AudioManager.EnsureExists().PlayUiConfirm();
                    ShowLeaderboard(true);
                });
            }

            if (_mainRefs.ExitButton != null)
            {
                _mainRefs.ExitButton.onClick.RemoveAllListeners();
                _mainRefs.ExitButton.onClick.AddListener(ExitGame);
            }
        }

        private void WireSettingsMenu()
        {
            if (_settingsRefs == null)
            {
                return;
            }

            if (_settingsRefs.MusicVolumeSlider != null)
            {
                _settingsRefs.MusicVolumeSlider.onValueChanged.RemoveAllListeners();
                _settingsRefs.MusicVolumeSlider.onValueChanged.AddListener(value =>
                {
                    GameSettings.SetMusicVolume(value);
                    RefreshOptionValueLabels();
                    AudioManager.EnsureExists().PlayUiMove();
                });
            }

            if (_settingsRefs.SfxVolumeSlider != null)
            {
                _settingsRefs.SfxVolumeSlider.onValueChanged.RemoveAllListeners();
                _settingsRefs.SfxVolumeSlider.onValueChanged.AddListener(value =>
                {
                    GameSettings.SetSfxVolume(value);
                    RefreshOptionValueLabels();
                    AudioManager.EnsureExists().PlayUiMove();
                });
            }

            if (_settingsRefs.SensitivitySlider != null)
            {
                _settingsRefs.SensitivitySlider.onValueChanged.RemoveAllListeners();
                _settingsRefs.SensitivitySlider.onValueChanged.AddListener(value =>
                {
                    GameSettings.SetRotationSensitivity(value);
                    RefreshOptionValueLabels();
                    AudioManager.EnsureExists().PlayUiMove();
                });
            }

            if (_settingsRefs.TouchRotationToggle != null)
            {
                _settingsRefs.TouchRotationToggle.onValueChanged.RemoveAllListeners();
                _settingsRefs.TouchRotationToggle.onValueChanged.AddListener(isOn =>
                {
                    GameSettings.SetTouchRotationMode(isOn ? TouchRotationMode.VirtualJoystick : TouchRotationMode.LeftRightButtons);
                    RefreshOptionValueLabels();
                    AudioManager.EnsureExists().PlayUiMove();
                });
            }

            if (_settingsRefs.BackButton != null)
            {
                _settingsRefs.BackButton.onClick.RemoveAllListeners();
                _settingsRefs.BackButton.onClick.AddListener(() =>
                {
                    AudioManager.EnsureExists().PlayUiConfirm();
                    ShowOptions(false);
                });
            }

            RefreshOptionValueLabels();
        }

        private void WireLeaderboardMenu()
        {
            if (_leaderboardRefs == null)
            {
                return;
            }

            if (_leaderboardRefs.BackButton != null)
            {
                _leaderboardRefs.BackButton.onClick.RemoveAllListeners();
                _leaderboardRefs.BackButton.onClick.AddListener(() =>
                {
                    AudioManager.EnsureExists().PlayUiConfirm();
                    ShowLeaderboard(false);
                });
            }
        }

        private void WireAchievementsMenu()
        {
            if (_achievementsRefs == null)
            {
                return;
            }

            if (_achievementsRefs.CloseButton != null)
            {
                _achievementsRefs.CloseButton.onClick.RemoveAllListeners();
                _achievementsRefs.CloseButton.onClick.AddListener(() =>
                {
                    AudioManager.EnsureExists().PlayUiConfirm();
                    ShowAchievements(false);
                });
            }
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

        private void ShowOptions(bool show)
        {
            if (_mainPanel != null)
            {
                _mainPanel.SetActive(!show);
            }

            if (_settingsPanel != null)
            {
                _settingsPanel.SetActive(show);
            }

            if (_leaderboardPanel != null)
            {
                _leaderboardPanel.SetActive(false);
            }

            if (_achievementsPanel != null)
            {
                _achievementsPanel.SetActive(false);
            }

            GameObject next = show
                ? (_settingsRefs != null && _settingsRefs.MusicVolumeSlider != null ? _settingsRefs.MusicVolumeSlider.gameObject : null)
                : (_mainRefs != null && _mainRefs.StartButton != null ? _mainRefs.StartButton.gameObject : null);
            SetSelected(next);
            SyncMenuSelectionTargets(next);
        }

        private void ShowLeaderboard(bool show)
        {
            if (_mainPanel != null)
            {
                _mainPanel.SetActive(!show);
            }

            if (_settingsPanel != null)
            {
                _settingsPanel.SetActive(false);
            }

            if (_leaderboardPanel != null)
            {
                _leaderboardPanel.SetActive(show);
            }

            if (_achievementsPanel != null)
            {
                _achievementsPanel.SetActive(false);
            }

            if (show && _leaderboardRefs != null && _leaderboardRefs.LeaderboardScoresText != null)
            {
                _leaderboardRefs.LeaderboardScoresText.text = BuildLeaderboardText();
            }

            GameObject next = show
                ? (_leaderboardRefs != null && _leaderboardRefs.BackButton != null ? _leaderboardRefs.BackButton.gameObject : null)
                : (_mainRefs != null && _mainRefs.StartButton != null ? _mainRefs.StartButton.gameObject : null);
            SetSelected(next);
            SyncMenuSelectionTargets(next);
        }

        private void ShowAchievements(bool show)
        {
            if (_mainPanel != null)
            {
                _mainPanel.SetActive(!show);
            }

            if (_settingsPanel != null)
            {
                _settingsPanel.SetActive(false);
            }

            if (_leaderboardPanel != null)
            {
                _leaderboardPanel.SetActive(false);
            }

            if (_achievementsPanel != null)
            {
                _achievementsPanel.SetActive(show);
                if (show)
                {
                    _achievementsPanel.transform.SetAsLastSibling();
                }
            }

            if (show && _achievementsRefs != null && _achievementsRefs.AchievementListText != null)
            {
                AchievementListPanel.RefreshBodyText(_achievementsRefs.AchievementListText);
            }

            GameObject next = show
                ? (_achievementsRefs != null && _achievementsRefs.CloseButton != null ? _achievementsRefs.CloseButton.gameObject : null)
                : (_mainRefs != null && _mainRefs.StartButton != null ? _mainRefs.StartButton.gameObject : null);
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
            if (_settingsRefs == null)
            {
                return;
            }

            if (_settingsRefs.MusicVolumeValueText != null)
            {
                _settingsRefs.MusicVolumeValueText.text = Mathf.RoundToInt(GameSettings.MusicVolume * 100f) + "%";
            }

            if (_settingsRefs.SfxVolumeValueText != null)
            {
                _settingsRefs.SfxVolumeValueText.text = Mathf.RoundToInt(GameSettings.SfxVolume * 100f) + "%";
            }

            if (_settingsRefs.SensitivityValueText != null)
            {
                _settingsRefs.SensitivityValueText.text = GameSettings.RotationSensitivity.ToString("0.00") + "x";
            }

            if (_settingsRefs.TouchRotationValueText != null)
            {
                _settingsRefs.TouchRotationValueText.text = GameSettings.TouchRotationMode == TouchRotationMode.VirtualJoystick
                    ? "Joystick"
                    : "L / R";
            }

            if (_settingsRefs.TouchRotationToggle != null)
            {
                bool wantOn = GameSettings.TouchRotationMode == TouchRotationMode.VirtualJoystick;
                if (_settingsRefs.TouchRotationToggle.isOn != wantOn)
                {
                    _settingsRefs.TouchRotationToggle.SetIsOnWithoutNotify(wantOn);
                }
            }
        }

        private static void EnsureEventSystem()
        {
            // Project uses Input System only; StandaloneInputModule will not drive UI clicks.
            GameObject esGo;
            if (EventSystem.current == null)
            {
                esGo = new GameObject("EventSystem");
                esGo.AddComponent<EventSystem>();
            }
            else
            {
                esGo = EventSystem.current.gameObject;
            }

            if (esGo.GetComponent<InputSystemUIInputModule>() != null)
            {
                return;
            }

            StandaloneInputModule legacy = esGo.GetComponent<StandaloneInputModule>();
            if (legacy != null)
            {
                UnityEngine.Object.Destroy(legacy);
            }

            esGo.AddComponent<InputSystemUIInputModule>();
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

            if (backPressed && _settingsPanel != null && _settingsPanel.activeSelf)
            {
                ShowOptions(false);
            }
            else if (backPressed && _leaderboardPanel != null && _leaderboardPanel.activeSelf)
            {
                ShowLeaderboard(false);
            }
            else if (backPressed && _achievementsPanel != null && _achievementsPanel.activeSelf)
            {
                ShowAchievements(false);
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
