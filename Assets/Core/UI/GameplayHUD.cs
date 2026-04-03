using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using System.Collections.Generic;
using StaticDrift.Player;
using StaticDrift.Managers;
using StaticDrift.Cards;
using TMPro;

namespace StaticDrift.UI
{
    /// <summary>
    /// Updates timer (top center) and player stats (center bottom). Timer comes from MatchController when present.
    /// </summary>
    public class GameplayHUD : MonoBehaviour
    {
        [SerializeField] private TMP_Text _timerText;
        [SerializeField] private TMP_Text _playerStatsText;
        [SerializeField] private TMP_Text _scoreText;
        [SerializeField] private TMP_Text _waveText;
        [SerializeField] private TMP_Text _buildText;
        [SerializeField] private GameObject _bossPanel;
        [SerializeField] private Image _bossHpFill;
        [SerializeField] private TMP_Text _bossHpText;
        [SerializeField] private GameObject _playerHpPanel;
        [SerializeField] private Image _playerHpFill;
        [SerializeField] private RectTransform _playerHpFillRect;
        [SerializeField] private TMP_Text _playerHpText;
        [SerializeField] private Button _pauseButton;
        [SerializeField] private string _statsFormat = "HP: {0:F0} / {1:F0}";
        [SerializeField] private bool _createTouchControls = true;

        private PlayerHealth _playerHealth;
        private PlayerController _playerController;
        private float _localElapsed;
        private bool _rotateLeftHeld;
        private bool _rotateRightHeld;
        private bool _accelerateHeld;
        private readonly Dictionary<string, RectTransform> _controlVisuals = new Dictionary<string, RectTransform>();
        private readonly Dictionary<string, Vector2> _controlVisualHomePositions = new Dictionary<string, Vector2>();
        private readonly List<GameObject> _touchGameplayButtonRoots = new List<GameObject>(3);
        private bool _touchGameplayButtonsVisible = true;
        private static Sprite _circleButtonSprite;
        private TMP_Text[] _buildTagCountTexts;
        private Image[] _buildSlotDiamonds;
        private TMP_Text[] _buildSlotLetters;

        private const string PlayerTag = "Player";

        private void Start()
        {
            TryBindPlayerReferences();

            if (_createTouchControls)
            {
                EnsureTouchControls();
            }

            EnsureInfoLabels();
            EnsureTimerWaveCluster();
            EnsureBuildUpgradeRow();
            EnsureBossPanel();
            EnsurePlayerHpPanel();
            EnsurePauseButton();

            if (_playerStatsText != null)
            {
                _playerStatsText.gameObject.SetActive(false);
            }

            ApplyFonts();
            ApplyHudVisualStyle();
        }

        private void Update()
        {
            MatchController match = MatchController.Instance;
            float timeToShow = match != null
                ? match.WaveElapsedTime
                : (_localElapsed += Time.deltaTime);
            float durationToShow = match != null ? match.WaveDuration : 0f;
            bool bossFight = match != null && match.IsBossFight;
            bool waveTimer = match != null && match.IsWaveTimerActive;

            if (_timerText != null)
            {
                if (bossFight)
                {
                    _timerText.text = string.Empty;
                }
                else if (waveTimer && durationToShow > 0.01f)
                {
                    int totalSeconds = Mathf.FloorToInt(timeToShow);
                    int minutes = totalSeconds / 60;
                    int seconds = totalSeconds % 60;
                    int durationSeconds = Mathf.FloorToInt(durationToShow);
                    int durationMinutes = durationSeconds / 60;
                    int durationRemainderSeconds = durationSeconds % 60;
                    _timerText.text = minutes.ToString("00") + ":" + seconds.ToString("00")
                        + " / "
                        + durationMinutes.ToString("00") + ":" + durationRemainderSeconds.ToString("00");
                }
                else
                {
                    int totalSeconds = Mathf.FloorToInt(timeToShow);
                    int minutes = totalSeconds / 60;
                    int seconds = totalSeconds % 60;
                    _timerText.text = minutes.ToString("00") + ":" + seconds.ToString("00");
                }
            }

            if (_scoreText != null && MatchController.Instance != null)
            {
                _scoreText.text = "SCORE " + MatchController.Instance.Score;
            }

            if (_waveText != null && MatchController.Instance != null)
            {
                _waveText.text = "WAVE " + MatchController.Instance.CurrentWave;
            }

            if (_buildText != null)
            {
                _buildText.text = "BUILD";
            }

            RunUpgradeController runUpgrades = RunUpgradeController.Instance;
            if (_buildTagCountTexts != null && _buildTagCountTexts.Length == 4 && runUpgrades != null
                && _buildSlotDiamonds != null && _buildSlotLetters != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    CardTag tag = runUpgrades.GetLoadoutSlotTag(i);
                    _buildTagCountTexts[i].text = runUpgrades.GetLoadoutSlotStacksText(i);
                    _buildSlotLetters[i].text = GetHudLoadoutLetter(tag);
                    _buildSlotDiamonds[i].color = tag == CardTag.None
                        ? new Color(0.32f, 0.36f, 0.42f, 0.55f)
                        : UpgradeHudVisuals.GetTagColor(tag);
                    _buildSlotLetters[i].color = tag == CardTag.None
                        ? new Color(0.82f, 0.86f, 0.92f, 1f)
                        : new Color(0.04f, 0.05f, 0.08f, 1f);
                }
            }

            if (MatchController.Instance != null)
            {
                bool showBoss = MatchController.Instance.IsBossFight;
                if (_bossPanel != null)
                {
                    _bossPanel.SetActive(showBoss);
                }

                if (showBoss)
                {
                    if (_bossHpFill != null)
                    {
                        _bossHpFill.fillAmount = MatchController.Instance.BossHealthNormalized;
                    }

                    if (_bossHpText != null)
                    {
                        _bossHpText.text = "BOSS " + MatchController.Instance.BossCurrentHealth + " / " + MatchController.Instance.BossMaxHealth;
                    }
                }
            }

            if (_playerHealth == null)
            {
                TryBindPlayerReferences();
            }

            if (_playerHpFill != null && _playerHealth != null)
            {
                float hpNormalized = Mathf.Clamp01(_playerHealth.CurrentHealth / Mathf.Max(1f, _playerHealth.MaxHealth));
                _playerHpFill.fillAmount = hpNormalized;
                if (_playerHpFillRect != null)
                {
                    _playerHpFillRect.anchorMax = new Vector2(hpNormalized, 1f);
                }
            }

            if (_playerHpText != null && _playerHealth != null)
            {
                string hpLine = string.Format(_statsFormat, _playerHealth.CurrentHealth, _playerHealth.MaxHealth);
                int lives = _playerHealth.BonusLives;
                if (lives > 0)
                {
                    hpLine += "  +" + lives + "L";
                }

                _playerHpText.text = hpLine;
            }
            else if (_playerHpText != null && _playerHealth == null)
            {
                _playerHpText.text = "HP: --";
            }

            if (_playerController == null)
            {
                GameObject playerGo = GameObject.FindGameObjectWithTag(PlayerTag);
                if (playerGo != null)
                {
                    _playerController = playerGo.GetComponent<PlayerController>();
                    if (_playerController == null)
                    {
                        _playerController = playerGo.GetComponentInChildren<PlayerController>();
                    }
                }
            }

            if (_playerController != null)
            {
                _playerController.SetRotateLeftHeld(_rotateLeftHeld);
                _playerController.SetRotateRightHeld(_rotateRightHeld);
                _playerController.SetAccelerateHeld(_accelerateHeld);
            }

            UpdateTouchGameplayButtonsVisibility();
        }

        private void EnsureTouchControls()
        {
            EnsureEventSystem();

            Transform rotateLeft = transform.Find("RotateLeftButton");
            if (rotateLeft == null)
            {
                CreateControlButton("RotateLeftButton", new Vector2(0f, 0f), new Vector2(0.12f, 1f), "L");
            }

            Transform rotateRight = transform.Find("RotateRightButton");
            if (rotateRight == null)
            {
                CreateControlButton("RotateRightButton", new Vector2(0.12f, 0f), new Vector2(0.24f, 1f), "R");
            }

            Transform accelerate = transform.Find("AccelerateButton");
            if (accelerate == null)
            {
                CreateControlButton("AccelerateButton", new Vector2(0.78f, 0f), new Vector2(1f, 0.88f), "A");
            }

            _touchGameplayButtonRoots.Clear();
            CollectTouchGameplayButtonRoot("RotateLeftButton");
            CollectTouchGameplayButtonRoot("RotateRightButton");
            CollectTouchGameplayButtonRoot("AccelerateButton");
        }

        private void CollectTouchGameplayButtonRoot(string childName)
        {
            Transform t = transform.Find(childName);
            if (t != null)
            {
                _touchGameplayButtonRoots.Add(t.gameObject);
            }
        }

        private void UpdateTouchGameplayButtonsVisibility()
        {
            if (!_createTouchControls || _touchGameplayButtonRoots.Count == 0)
            {
                return;
            }

            Touchscreen ts = Touchscreen.current;
            if (ts != null && ts.primaryTouch.press.wasPressedThisFrame)
            {
                SetTouchGameplayButtonsVisible(true);
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                SetTouchGameplayButtonsVisible(true);
                return;
            }

            if (IsGamepadGameplayInputActive())
            {
                SetTouchGameplayButtonsVisible(false);
                return;
            }

            if (IsKeyboardGameplayInputActive())
            {
                SetTouchGameplayButtonsVisible(false);
            }
        }

        private static bool IsGamepadGameplayInputActive()
        {
            Gamepad gp = Gamepad.current;
            if (gp == null)
            {
                return false;
            }

            return gp.leftShoulder.isPressed
                || gp.rightShoulder.isPressed
                || gp.buttonSouth.isPressed;
        }

        private static bool IsKeyboardGameplayInputActive()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null)
            {
                return false;
            }

            return kb.wKey.isPressed || kb.aKey.isPressed || kb.dKey.isPressed;
        }

        private void SetTouchGameplayButtonsVisible(bool visible)
        {
            if (_touchGameplayButtonsVisible == visible)
            {
                return;
            }

            _touchGameplayButtonsVisible = visible;
            for (int i = 0; i < _touchGameplayButtonRoots.Count; i++)
            {
                GameObject go = _touchGameplayButtonRoots[i];
                if (go != null)
                {
                    go.SetActive(visible);
                }
            }
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            GameObject eventSystemGo = new GameObject("EventSystem");
            eventSystemGo.AddComponent<EventSystem>();
            eventSystemGo.AddComponent<InputSystemUIInputModule>();
        }

        private void CreateControlButton(string objectName, Vector2 anchorMin, Vector2 anchorMax, string label)
        {
            GameObject buttonGo = new GameObject(objectName);
            buttonGo.transform.SetParent(transform, false);

            RectTransform rect = buttonGo.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            Image bg = buttonGo.AddComponent<Image>();
            bg.color = new Color(1f, 1f, 1f, 0.001f);

            GameObject visualGo = new GameObject("Visual");
            visualGo.transform.SetParent(buttonGo.transform, false);
            RectTransform visualRect = visualGo.AddComponent<RectTransform>();
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

            GameObject textGo = new GameObject("Label");
            textGo.transform.SetParent(visualGo.transform, false);
            RectTransform textRect = textGo.AddComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            TMP_Text text = textGo.AddComponent<TextMeshProUGUI>();
            text.text = label;
            text.fontSize = 44f;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.color = new Color(0.76f, 0.93f, 1f, 0.95f);
            text.raycastTarget = false;

            _controlVisuals[objectName] = visualRect;
            _controlVisualHomePositions[objectName] = visualRect.anchoredPosition;
            AddHoldEvents(buttonGo, objectName);
        }

        private void AddHoldEvents(GameObject target, string objectName)
        {
            EventTrigger trigger = target.AddComponent<EventTrigger>();
            trigger.triggers = new System.Collections.Generic.List<EventTrigger.Entry>();

            AddTrigger(trigger, EventTriggerType.PointerDown, eventData =>
            {
                SetControlState(objectName, true);
                MoveControlVisualToPressLocation(objectName, eventData);
            });
            AddTrigger(trigger, EventTriggerType.Drag, eventData =>
            {
                SetControlState(objectName, true);
                MoveControlVisualToPressLocation(objectName, eventData);
            });
            AddTrigger(trigger, EventTriggerType.PointerUp, _ =>
            {
                SetControlState(objectName, false);
                ResetControlVisual(objectName);
            });
            AddTrigger(trigger, EventTriggerType.PointerExit, _ =>
            {
                SetControlState(objectName, false);
                ResetControlVisual(objectName);
            });
        }

        private static void AddTrigger(EventTrigger trigger, EventTriggerType type, System.Action<BaseEventData> callback)
        {
            EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(eventData => callback(eventData));
            trigger.triggers.Add(entry);
        }

        private void MoveControlVisualToPressLocation(string objectName, BaseEventData eventData)
        {
            if (!_controlVisuals.TryGetValue(objectName, out RectTransform visualRect) || visualRect == null)
            {
                return;
            }

            PointerEventData pointerData = eventData as PointerEventData;
            if (pointerData == null)
            {
                return;
            }

            RectTransform zoneRect = visualRect.parent as RectTransform;
            if (zoneRect == null)
            {
                return;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(zoneRect, pointerData.position, pointerData.pressEventCamera, out Vector2 localPoint))
            {
                return;
            }

            float halfZoneWidth = zoneRect.rect.width * 0.5f;
            float maxX = Mathf.Max(0f, halfZoneWidth - visualRect.sizeDelta.x * 0.5f);
            float x = Mathf.Clamp(localPoint.x, -maxX, maxX);

            float yFromBottom = localPoint.y + (zoneRect.rect.height * 0.5f);
            float minY = visualRect.sizeDelta.y * 0.2f;
            float maxY = Mathf.Max(minY, zoneRect.rect.height - visualRect.sizeDelta.y);
            float y = Mathf.Clamp(yFromBottom - (visualRect.sizeDelta.y * 0.5f), minY, maxY);

            visualRect.anchoredPosition = new Vector2(x, y);
        }

        private void ResetControlVisual(string objectName)
        {
            if (!_controlVisuals.TryGetValue(objectName, out RectTransform visualRect) || visualRect == null)
            {
                return;
            }

            if (_controlVisualHomePositions.TryGetValue(objectName, out Vector2 homePosition))
            {
                visualRect.anchoredPosition = homePosition;
            }
        }

        private void ApplyHudVisualStyle()
        {
            if (_timerText != null)
            {
                _timerText.fontStyle = FontStyles.Bold;
                _timerText.fontSize = 52f;
                _timerText.color = new Color(0.79f, 0.95f, 1f, 1f);
                GameFontLibrary.ApplyOutline(_timerText, 0.22f, new Color(0.02f, 0.04f, 0.08f, 0.95f));
            }

            if (_playerStatsText != null)
            {
                _playerStatsText.fontStyle = FontStyles.Bold;
                _playerStatsText.color = new Color(0.95f, 0.98f, 1f, 0.98f);
                GameFontLibrary.ApplyOutline(_playerStatsText, 0.2f, new Color(0.04f, 0.06f, 0.1f, 0.9f));
            }

            if (_scoreText != null)
            {
                _scoreText.fontStyle = FontStyles.Bold;
                _scoreText.fontSize = 48f;
                _scoreText.color = new Color(1f, 0.89f, 0.45f, 0.98f);
                GameFontLibrary.ApplyOutline(_scoreText, 0.18f, new Color(0.06f, 0.06f, 0.08f, 0.9f));
            }

            if (_waveText != null)
            {
                _waveText.fontStyle = FontStyles.Bold;
                _waveText.fontSize = 48f;
                _waveText.color = new Color(0.73f, 1f, 0.86f, 0.98f);
                GameFontLibrary.ApplyOutline(_waveText, 0.18f, new Color(0.04f, 0.08f, 0.07f, 0.9f));
            }

            if (_buildText != null)
            {
                _buildText.fontStyle = FontStyles.Bold;
                _buildText.color = new Color(0.86f, 0.83f, 1f, 0.98f);
                GameFontLibrary.ApplyOutline(_buildText, 0.16f, new Color(0.05f, 0.05f, 0.09f, 0.9f));
                _buildText.textWrappingMode = TextWrappingModes.Normal;
                _buildText.fontSize = 40f;
                _buildText.lineSpacing = -8f;
            }

            if (_buildTagCountTexts != null)
            {
                for (int i = 0; i < _buildTagCountTexts.Length; i++)
                {
                    TMP_Text ct = _buildTagCountTexts[i];
                    if (ct == null)
                    {
                        continue;
                    }

                    ct.fontStyle = FontStyles.Bold;
                    ct.fontSize = UseMobileHudLayout() ? 30f : 26f;
                    ct.color = new Color(0.88f, 0.9f, 1f, 0.95f);
                    GameFontLibrary.ApplyOutline(ct, 0.14f, new Color(0.05f, 0.05f, 0.09f, 0.88f));
                }
            }

            if (_buildSlotLetters != null)
            {
                for (int i = 0; i < _buildSlotLetters.Length; i++)
                {
                    TMP_Text lt = _buildSlotLetters[i];
                    if (lt == null)
                    {
                        continue;
                    }

                    lt.fontStyle = FontStyles.Bold;
                    lt.fontSize = Mathf.Max(22f, (UseMobileHudLayout() ? 52f : 44f) * 0.58f);
                    GameFontLibrary.ApplyOutline(lt, 0.12f, new Color(0.05f, 0.05f, 0.09f, 0.88f));
                }
            }

            if (_playerHpText != null)
            {
                _playerHpText.fontStyle = FontStyles.Bold;
                _playerHpText.fontSize = 34f;
                _playerHpText.color = new Color(0.95f, 0.98f, 1f, 1f);
                GameFontLibrary.ApplyOutline(_playerHpText, 0.16f, new Color(0.04f, 0.06f, 0.1f, 0.9f));
            }

            if (_bossHpText != null)
            {
                _bossHpText.fontSize = 36f;
                GameFontLibrary.ApplyOutline(_bossHpText, 0.16f, new Color(0.1f, 0.04f, 0.04f, 0.95f));
            }
        }

        private void ApplyFonts()
        {
            GameFontLibrary.Apply(_timerText);
            GameFontLibrary.Apply(_playerStatsText);
            GameFontLibrary.Apply(_scoreText);
            GameFontLibrary.Apply(_waveText);
            GameFontLibrary.Apply(_buildText);
            GameFontLibrary.Apply(_bossHpText);
            GameFontLibrary.Apply(_playerHpText);
            if (_buildTagCountTexts != null)
            {
                for (int i = 0; i < _buildTagCountTexts.Length; i++)
                {
                    if (_buildTagCountTexts[i] != null)
                    {
                        GameFontLibrary.Apply(_buildTagCountTexts[i]);
                    }
                }
            }

            if (_buildSlotLetters != null)
            {
                for (int i = 0; i < _buildSlotLetters.Length; i++)
                {
                    if (_buildSlotLetters[i] != null)
                    {
                        GameFontLibrary.Apply(_buildSlotLetters[i]);
                    }
                }
            }
        }

        private void EnsureInfoLabels()
        {
            if (_scoreText == null)
            {
                _scoreText = CreateInfoLabel("ScoreText", new Vector2(0.04f, 0.955f), TextAlignmentOptions.Left, "SCORE 0", new Vector2(520f, 86f));
            }
            if (_waveText == null)
            {
                _waveText = CreateInfoLabel("WaveText", new Vector2(0.5f, 0.955f), TextAlignmentOptions.Center, "WAVE 1", new Vector2(400f, 86f));
            }
            if (_buildText == null)
            {
                _buildText = CreateInfoLabel("BuildText", new Vector2(0.965f, 0.90f), TextAlignmentOptions.Right, "BUILD", new Vector2(760f, 132f));
            }
        }

        private void EnsureTimerWaveCluster()
        {
            if (_timerText == null)
            {
                return;
            }

            const string clusterName = "TimerWaveCluster";
            Transform clusterTf = transform.Find(clusterName);
            RectTransform clusterRt;
            if (clusterTf == null)
            {
                GameObject clusterGo = new GameObject(clusterName, typeof(RectTransform));
                clusterTf = clusterGo.transform;
                clusterTf.SetParent(transform, false);
                clusterRt = clusterGo.GetComponent<RectTransform>();
                clusterRt.anchorMin = new Vector2(0.5f, 1f);
                clusterRt.anchorMax = new Vector2(0.5f, 1f);
                clusterRt.pivot = new Vector2(0.5f, 1f);
                clusterRt.anchoredPosition = new Vector2(0f, -14f);
                clusterRt.sizeDelta = new Vector2(440f, 108f);
            }
            else
            {
                clusterRt = clusterTf.GetComponent<RectTransform>();
            }

            _timerText.transform.SetParent(clusterRt, false);
            RectTransform timerRt = _timerText.rectTransform;
            timerRt.anchorMin = new Vector2(0.5f, 1f);
            timerRt.anchorMax = new Vector2(0.5f, 1f);
            timerRt.pivot = new Vector2(0.5f, 1f);
            timerRt.anchoredPosition = Vector2.zero;
            timerRt.sizeDelta = new Vector2(400f, 58f);

            if (_waveText != null)
            {
                _waveText.transform.SetParent(clusterRt, false);
                RectTransform waveRt = _waveText.rectTransform;
                waveRt.anchorMin = new Vector2(0.5f, 1f);
                waveRt.anchorMax = new Vector2(0.5f, 1f);
                waveRt.pivot = new Vector2(0.5f, 1f);
                waveRt.anchoredPosition = new Vector2(0f, -56f);
                waveRt.sizeDelta = new Vector2(420f, 46f);
            }
        }

        private static bool UseMobileHudLayout()
        {
            int shortSide = Mathf.Min(Screen.width, Screen.height);
            return Application.isMobilePlatform || shortSide <= 1080;
        }

        /// <summary>
        /// Horizontal offset (px) to inset the build row from the screen edge so it does not sit under the top-right pause button (margin + button width + gap).
        /// </summary>
        private static float GetBuildHudPauseHorizontalClearancePx()
        {
            return UseMobileHudLayout() ? 152f : 136f;
        }

        private static string GetHudLoadoutLetter(CardTag tag)
        {
            switch (tag)
            {
                case CardTag.Volt:
                    return "V";
                case CardTag.Kinetic:
                    return "K";
                case CardTag.Thermal:
                    return "T";
                case CardTag.Static:
                    return "S";
                case CardTag.Repair:
                    return "R";
                case CardTag.Reach:
                    return "E";
                case CardTag.Volley:
                    return "C";
                case CardTag.Vitality:
                    return "L";
                default:
                    return "?";
            }
        }

        private void EnsureBuildUpgradeRow()
        {
            if (_buildTagCountTexts != null && _buildTagCountTexts.Length == 4 && _buildSlotDiamonds != null && _buildSlotLetters != null)
            {
                return;
            }

            if (_buildText == null)
            {
                return;
            }

            Transform existingRow = transform.Find("BuildHudRow");
            if (existingRow != null)
            {
                Destroy(existingRow.gameObject);
            }

            GameObject rowGo = new GameObject("BuildHudRow", typeof(RectTransform));
            rowGo.transform.SetParent(transform, false);
            RectTransform rowRect = rowGo.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0.965f, 0.90f);
            rowRect.anchorMax = new Vector2(0.965f, 0.90f);
            rowRect.pivot = new Vector2(1f, 0.5f);
            rowRect.sizeDelta = new Vector2(920f, 140f);
            Vector2 buildHome = _buildText.rectTransform.anchoredPosition;
            rowRect.anchoredPosition = new Vector2(buildHome.x - GetBuildHudPauseHorizontalClearancePx(), buildHome.y);

            HorizontalLayoutGroup hlg = rowGo.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment = TextAnchor.MiddleRight;
            hlg.spacing = 10f;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            _buildText.transform.SetParent(rowGo.transform, false);
            LayoutElement buildLe = _buildText.gameObject.GetComponent<LayoutElement>();
            if (buildLe == null)
            {
                buildLe = _buildText.gameObject.AddComponent<LayoutElement>();
            }

            buildLe.preferredWidth = 118f;
            buildLe.minWidth = 96f;
            buildLe.flexibleWidth = 0f;

            RectTransform brt = _buildText.rectTransform;
            brt.anchorMin = new Vector2(0f, 0.5f);
            brt.anchorMax = new Vector2(0f, 0.5f);
            brt.pivot = new Vector2(0f, 0.5f);
            brt.anchoredPosition = Vector2.zero;
            brt.sizeDelta = new Vector2(118f, 72f);

            _buildTagCountTexts = new TMP_Text[4];
            _buildSlotDiamonds = new Image[4];
            _buildSlotLetters = new TMP_Text[4];
            float iconSize = UseMobileHudLayout() ? 52f : 44f;

            for (int i = 0; i < 4; i++)
            {
                CreateBuildTagSlot(rowGo.transform, i, iconSize, out _buildSlotDiamonds[i], out _buildSlotLetters[i], out _buildTagCountTexts[i]);
            }
        }

        private void CreateBuildTagSlot(Transform parent, int slotIndex, float iconSize, out Image diamondImage, out TMP_Text letterTmp, out TMP_Text countText)
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
            diamondImage = iconImage;

            GameObject letterGo = new GameObject("Letter", typeof(RectTransform));
            letterGo.transform.SetParent(iconGo.transform, false);
            RectTransform letterRect = letterGo.GetComponent<RectTransform>();
            letterRect.anchorMin = Vector2.zero;
            letterRect.anchorMax = Vector2.one;
            letterRect.offsetMin = Vector2.zero;
            letterRect.offsetMax = Vector2.zero;
            letterTmp = letterGo.AddComponent<TextMeshProUGUI>();
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
            countText = countTmp;
        }

        private void EnsureBossPanel()
        {
            if (_bossPanel != null && _bossHpFill != null && _bossHpText != null)
            {
                return;
            }

            Transform existing = transform.Find("BossPanel");
            if (existing != null)
            {
                _bossPanel = existing.gameObject;
                Transform fill = existing.Find("Fill");
                if (fill != null)
                {
                    _bossHpFill = fill.GetComponent<Image>();
                }
                Transform labelTransform = existing.Find("Label");
                if (labelTransform != null)
                {
                    _bossHpText = labelTransform.GetComponent<TMP_Text>();
                }
                return;
            }

            GameObject panel = new GameObject("BossPanel");
            panel.transform.SetParent(transform, false);
            RectTransform rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.885f);
            rect.anchorMax = new Vector2(0.5f, 0.885f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(720f, 44f);

            Image bg = panel.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.04f, 0.05f, 0.92f);

            GameObject fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(panel.transform, false);
            RectTransform fillRect = fillGo.AddComponent<RectTransform>();
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

            GameObject labelGo = new GameObject("Label");
            labelGo.transform.SetParent(panel.transform, false);
            RectTransform labelRect = labelGo.AddComponent<RectTransform>();
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

            _bossPanel = panel;
            _bossHpFill = fillImage;
            _bossHpText = labelText;
            _bossPanel.SetActive(false);
        }

        private void EnsurePlayerHpPanel()
        {
            if (_playerHpPanel != null && _playerHpFill != null && _playerHpText != null)
            {
                if (_playerHpFillRect == null)
                {
                    _playerHpFillRect = _playerHpFill.rectTransform;
                }
                return;
            }

            Transform existing = transform.Find("PlayerHpPanel");
            if (existing != null)
            {
                _playerHpPanel = existing.gameObject;
                EnsurePlayerHpBarVisuals(existing);

                Transform label = existing.Find("Label");
                if (label != null)
                {
                    _playerHpText = label.GetComponent<TMP_Text>();
                }

                return;
            }

            GameObject panel = new GameObject("PlayerHpPanel");
            panel.transform.SetParent(transform, false);
            RectTransform rect = panel.AddComponent<RectTransform>();
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

            GameObject trackGo = new GameObject("Track");
            trackGo.transform.SetParent(panel.transform, false);
            RectTransform trackRect = trackGo.AddComponent<RectTransform>();
            trackRect.anchorMin = Vector2.zero;
            trackRect.anchorMax = Vector2.one;
            trackRect.offsetMin = new Vector2(4f, 4f);
            trackRect.offsetMax = new Vector2(-4f, -4f);
            Image trackImage = trackGo.AddComponent<Image>();
            trackImage.color = new Color(0.09f, 0.15f, 0.22f, 1f);
            trackImage.raycastTarget = false;

            GameObject fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(trackGo.transform, false);
            RectTransform fillRect = fillGo.AddComponent<RectTransform>();
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

            GameObject labelGo = new GameObject("Label");
            labelGo.transform.SetParent(panel.transform, false);
            RectTransform labelRect = labelGo.AddComponent<RectTransform>();
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

            _playerHpPanel = panel;
            _playerHpFill = fillImage;
            _playerHpFillRect = fillRect;
            _playerHpText = labelText;
        }

        private void ApplyPauseButtonLayout(RectTransform rect)
        {
            if (rect == null)
            {
                return;
            }

            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            float pauseInsetX = UseMobileHudLayout() ? -40f : -36f;
            float pauseInsetY = UseMobileHudLayout() ? -178f : -158f;
            rect.anchoredPosition = new Vector2(pauseInsetX, pauseInsetY);
            rect.sizeDelta = new Vector2(86f, 86f);
        }

        /// <summary>
        /// Hides the HUD pause control (e.g. while the pause Help overlay is open) so it cannot be focused via gamepad/keyboard.
        /// </summary>
        public void SetPauseButtonHidden(bool hidden)
        {
            EnsurePauseButton();
            if (_pauseButton != null)
            {
                _pauseButton.gameObject.SetActive(!hidden);
            }
        }

        private void EnsurePauseButton()
        {
            if (_pauseButton != null)
            {
                ApplyPauseButtonLayout(_pauseButton.GetComponent<RectTransform>());
                return;
            }

            Transform existing = transform.Find("PauseButton");
            if (existing != null)
            {
                _pauseButton = existing.GetComponent<Button>();
                ApplyPauseButtonLayout(existing.GetComponent<RectTransform>());
                return;
            }

            GameObject buttonGo = new GameObject("PauseButton");
            buttonGo.transform.SetParent(transform, false);
            RectTransform rect = buttonGo.AddComponent<RectTransform>();
            ApplyPauseButtonLayout(rect);

            Image image = buttonGo.AddComponent<Image>();
            Button button = buttonGo.AddComponent<Button>();
            button.targetGraphic = image;
            button.onClick.AddListener(() =>
            {
                MatchController match = MatchController.Instance;
                if (match != null)
                {
                    AudioManager.EnsureExists().PlayUiConfirm();
                    match.TogglePause();
                }
            });

            GameObject textGo = new GameObject("Label");
            textGo.transform.SetParent(buttonGo.transform, false);
            RectTransform textRect = textGo.AddComponent<RectTransform>();
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

            PixelArtUiSkin.ApplyButtonStyle(button, image, text);
            _pauseButton = button;
        }

        private TMP_Text CreateInfoLabel(string name, Vector2 anchor, TextAlignmentOptions alignment, string startText, Vector2 size)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            float pivotX = 0.5f;
            if (anchor.x < 0.33f)
            {
                pivotX = 0f;
            }
            else if (anchor.x > 0.67f)
            {
                pivotX = 1f;
            }

            rect.pivot = new Vector2(pivotX, 0.5f);
            rect.sizeDelta = size;
            TMP_Text text = go.AddComponent<TextMeshProUGUI>();
            text.text = startText;
            GameFontLibrary.Apply(text);
            text.fontSize = 46f;
            text.alignment = alignment;
            text.raycastTarget = false;
            return text;
        }

        private static Sprite GetCircleButtonSprite()
        {
            if (_circleButtonSprite != null)
            {
                return _circleButtonSprite;
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
            _circleButtonSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return _circleButtonSprite;
        }

        private void SetControlState(string objectName, bool isHeld)
        {
            if (objectName == "RotateLeftButton")
            {
                _rotateLeftHeld = isHeld;
            }
            else if (objectName == "RotateRightButton")
            {
                _rotateRightHeld = isHeld;
            }
            else if (objectName == "AccelerateButton")
            {
                _accelerateHeld = isHeld;
            }
        }

        private void OnValidate()
        {
            if (_scoreText == null)
            {
                _scoreText = FindTextByName("ScoreText");
            }
            if (_waveText == null)
            {
                _waveText = FindTextByName("WaveText");
            }
            if (_buildText == null)
            {
                _buildText = FindTextByName("BuildText");
            }
            if (_bossPanel == null)
            {
                Transform boss = transform.Find("BossPanel");
                if (boss != null)
                {
                    _bossPanel = boss.gameObject;
                    Transform fill = boss.Find("Fill");
                    if (fill != null)
                    {
                        _bossHpFill = fill.GetComponent<Image>();
                    }
                    Transform labelTransform = boss.Find("Label");
                    if (labelTransform != null)
                    {
                        _bossHpText = labelTransform.GetComponent<TMP_Text>();
                    }
                }
            }

            if (_playerHpPanel == null)
            {
                Transform playerHp = transform.Find("PlayerHpPanel");
                if (playerHp != null)
                {
                    _playerHpPanel = playerHp.gameObject;
                    EnsurePlayerHpBarVisuals(playerHp);
                    Transform labelTransform = playerHp.Find("Label");
                    if (labelTransform != null)
                    {
                        _playerHpText = labelTransform.GetComponent<TMP_Text>();
                    }
                }
            }
        }

        private TMP_Text FindTextByName(string name)
        {
            Transform t = FindDeepChild(transform, name);
            if (t == null)
            {
                return null;
            }

            return t.GetComponent<TMP_Text>();
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

        private void TryBindPlayerReferences()
        {
            GameObject playerGo = GameObject.FindGameObjectWithTag(PlayerTag);
            if (playerGo == null)
            {
                return;
            }

            if (_playerHealth == null)
            {
                _playerHealth = playerGo.GetComponent<PlayerHealth>();
                if (_playerHealth == null)
                {
                    _playerHealth = playerGo.GetComponentInChildren<PlayerHealth>();
                }
            }

            if (_playerController == null)
            {
                _playerController = playerGo.GetComponent<PlayerController>();
                if (_playerController == null)
                {
                    _playerController = playerGo.GetComponentInChildren<PlayerController>();
                }
            }
        }

        private void EnsurePlayerHpBarVisuals(Transform panelTransform)
        {
            if (panelTransform == null)
            {
                return;
            }

            Image panelImage = panelTransform.GetComponent<Image>();
            if (panelImage == null)
            {
                panelImage = panelTransform.gameObject.AddComponent<Image>();
            }
            panelImage.color = new Color(0.04f, 0.07f, 0.12f, 0.98f);
            panelImage.raycastTarget = false;

            Outline outline = panelTransform.GetComponent<Outline>();
            if (outline == null)
            {
                outline = panelTransform.gameObject.AddComponent<Outline>();
            }
            outline.effectColor = new Color(0.7f, 0.9f, 1f, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = false;

            Transform track = panelTransform.Find("Track");
            if (track == null)
            {
                track = panelTransform.Find("Fill");
                if (track != null)
                {
                    track.name = "Track";
                }
            }

            if (track == null)
            {
                GameObject trackGo = new GameObject("Track");
                trackGo.transform.SetParent(panelTransform, false);
                track = trackGo.transform;
            }

            RectTransform trackRect = track as RectTransform;
            if (trackRect == null)
            {
                trackRect = track.gameObject.AddComponent<RectTransform>();
            }
            trackRect.anchorMin = Vector2.zero;
            trackRect.anchorMax = Vector2.one;
            trackRect.offsetMin = new Vector2(4f, 4f);
            trackRect.offsetMax = new Vector2(-4f, -4f);
            trackRect.pivot = new Vector2(0.5f, 0.5f);

            Image trackImage = track.GetComponent<Image>();
            if (trackImage == null)
            {
                trackImage = track.gameObject.AddComponent<Image>();
            }
            trackImage.type = Image.Type.Simple;
            trackImage.color = new Color(0.09f, 0.15f, 0.22f, 1f);
            trackImage.raycastTarget = false;

            Transform fill = track.Find("Fill");
            if (fill == null)
            {
                GameObject fillGo = new GameObject("Fill");
                fillGo.transform.SetParent(track, false);
                fill = fillGo.transform;
            }

            RectTransform fillRect = fill as RectTransform;
            if (fillRect == null)
            {
                fillRect = fill.gameObject.AddComponent<RectTransform>();
            }
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            fillRect.pivot = new Vector2(0f, 0.5f);

            Image fillImage = fill.GetComponent<Image>();
            if (fillImage == null)
            {
                fillImage = fill.gameObject.AddComponent<Image>();
            }
            fillImage.type = Image.Type.Simple;
            fillImage.fillAmount = 1f;
            fillImage.color = new Color(0.2f, 0.78f, 0.42f, 0.95f);
            fillImage.raycastTarget = false;

            _playerHpFill = fillImage;
            _playerHpFillRect = fillRect;
        }
    }

    /// <summary>
    /// Shared diamond sprite and tag colors for upgrade icons (pause help, gameplay HUD).
    /// </summary>
    public static class UpgradeHudVisuals
    {
        private static Sprite _upgradeDiamondSprite;

        public static Sprite GetDiamondSprite()
        {
            if (_upgradeDiamondSprite != null)
            {
                return _upgradeDiamondSprite;
            }

            const int size = 64;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float outer = 26f;
            float inner = 10f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x - center.x);
                    float dy = Mathf.Abs(y - center.y);
                    float diamond = dx + dy;
                    float alpha = 0f;
                    if (diamond <= outer && diamond >= inner)
                    {
                        alpha = 1f;
                    }
                    else if (diamond < inner)
                    {
                        alpha = 1f;
                    }

                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            tex.Apply();
            _upgradeDiamondSprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            return _upgradeDiamondSprite;
        }

        public static Color GetTagColor(CardTag tag)
        {
            switch (tag)
            {
                case CardTag.Volt:
                    return new Color(0.42f, 0.86f, 1f, 0.98f);
                case CardTag.Kinetic:
                    return new Color(1f, 0.82f, 0.28f, 0.98f);
                case CardTag.Thermal:
                    return new Color(1f, 0.5f, 0.28f, 0.98f);
                case CardTag.Static:
                    return new Color(0.73f, 0.48f, 1f, 0.98f);
                case CardTag.Repair:
                    return new Color(0.38f, 0.95f, 0.62f, 0.98f);
                case CardTag.Reach:
                    return new Color(0.55f, 0.82f, 1f, 0.98f);
                case CardTag.Volley:
                    return new Color(1f, 0.55f, 0.78f, 0.98f);
                case CardTag.Vitality:
                    return new Color(1f, 0.42f, 0.42f, 0.98f);
                default:
                    return new Color(0.86f, 0.9f, 1f, 0.98f);
            }
        }

        public static Color GetUpgradeColor(RunUpgradeController.UpgradeId id)
        {
            switch (id)
            {
                case RunUpgradeController.UpgradeId.VoltOverclock:
                case RunUpgradeController.UpgradeId.VoltChainCharge:
                    return GetTagColor(CardTag.Volt);
                case RunUpgradeController.UpgradeId.KineticPayload:
                case RunUpgradeController.UpgradeId.KineticSlinger:
                    return GetTagColor(CardTag.Kinetic);
                case RunUpgradeController.UpgradeId.ThermalFlux:
                case RunUpgradeController.UpgradeId.ThermalCore:
                    return GetTagColor(CardTag.Thermal);
                case RunUpgradeController.UpgradeId.StaticPlating:
                case RunUpgradeController.UpgradeId.StaticField:
                    return GetTagColor(CardTag.Static);
                case RunUpgradeController.UpgradeId.RepairNanites:
                case RunUpgradeController.UpgradeId.RepairWeave:
                    return GetTagColor(CardTag.Repair);
                case RunUpgradeController.UpgradeId.ReachExtender:
                case RunUpgradeController.UpgradeId.ReachCalibrator:
                    return GetTagColor(CardTag.Reach);
                case RunUpgradeController.UpgradeId.VolleySpread:
                    return GetTagColor(CardTag.Volley);
                case RunUpgradeController.UpgradeId.BackupCell:
                case RunUpgradeController.UpgradeId.ReserveHarness:
                    return GetTagColor(CardTag.Vitality);
                default:
                    return new Color(0.86f, 0.9f, 1f, 0.98f);
            }
        }
    }
}
