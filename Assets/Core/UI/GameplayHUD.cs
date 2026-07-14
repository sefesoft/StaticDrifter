using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using System.Collections.Generic;
using System.Text;
using StaticDrift.Player;
using StaticDrift.Managers;
using StaticDrift.Cards;
using TMPro;

namespace StaticDrift.UI
{
    [System.Serializable]
    public class LoadoutSlotHudEntry
    {
        public Image Diamond;
        public TMP_Text Letter;
        public TMP_Text Count;
    }

    /// <summary>
    /// Updates timer (top center) and player stats (center bottom). Timer comes from MatchController when present.
    /// HUD hierarchy is authored on the GameplayHUD prefab; use menu Static Drift / Setup Gameplay HUD Prefab.
    /// </summary>
    public class GameplayHUD : MonoBehaviour
    {
        [SerializeField] private TMP_Text _timerText;
        [SerializeField] private TMP_Text _playerStatsText;
        [SerializeField] private TMP_Text _scoreText;
        [SerializeField] private TMP_Text _waveText;
        [SerializeField] private TMP_Text _buildText;
        [SerializeField] private LoadoutSlotHudEntry[] _loadoutSlots;
        [SerializeField] private GameObject _bossPanel;
        [SerializeField] private Image _bossHpFill;
        [SerializeField] private RectTransform _bossHpFillRect;
        [SerializeField] private TMP_Text _bossHpText;
        [SerializeField] private GameObject _playerHpPanel;
        [SerializeField] private Image _playerHpFill;
        [SerializeField] private RectTransform _playerHpFillRect;
        [SerializeField] private TMP_Text _playerHpText;
        [SerializeField] private Button _pauseButton;
        [SerializeField] private GameObject _rotateLeftButton;
        [SerializeField] private GameObject _rotateRightButton;
        [SerializeField] private GameObject _accelerateButton;
        [SerializeField] private string _statsFormat = "HP: {0:F0} / {1:F0}";
        [SerializeField] private bool _createTouchControls = true;
        [SerializeField] private bool _applyPauseLayoutFromCode;
        [SerializeField] private float _rotateJoystickDeadZone = 0.12f;
        [SerializeField, Range(0.2f, 0.8f)] private float _rotateJoystickZoneScreenWidth = 0.5f;
        [SerializeField] private Vector2 _rotateJoystickHomeNormalized = new Vector2(0.25f, 0.25f);
        [SerializeField] private float _rotateJoystickLimitRingScale = 2.2f;

        private PlayerHealth _playerHealth;
        private PlayerController _playerController;
        private float _localElapsed;
        private bool _rotateLeftHeld;
        private bool _rotateRightHeld;
        private bool _accelerateHeld;
        private int _rotateJoystickPointerId = -1;
        private bool _rotateJoystickHeld;
        private Vector2 _rotateJoystickCenterLocal;
        private RectTransform _rotateJoystickRingRect;
        private readonly Dictionary<string, RectTransform> _controlVisuals = new Dictionary<string, RectTransform>();
        private readonly Dictionary<string, Vector2> _controlVisualHomePositions = new Dictionary<string, Vector2>();
        private readonly List<GameObject> _touchGameplayButtonRoots = new List<GameObject>(3);
        private bool _touchGameplayButtonsVisible = true;
        private static Sprite _circleButtonSprite;
        private static Sprite _circleRingSprite;

        private const string PlayerTag = "Player";

        private void Start()
        {
            TryBindPlayerReferences();
            EnsureEventSystem();
            CachePlayerHpFillRectIfNeeded();
            CacheBossHpFillRectIfNeeded();
            LogMissingHudReferences();
            if (_createTouchControls)
            {
                WireTouchControlZones();
            }

            WirePauseButton();
            if (_applyPauseLayoutFromCode && _pauseButton != null)
            {
                ApplyPauseButtonLayout(_pauseButton.GetComponent<RectTransform>());
            }

            if (_playerStatsText != null)
            {
                _playerStatsText.gameObject.SetActive(false);
            }

            ApplyFonts();
            ApplyHudVisualStyle();
            EnsureAchievementToastPresenter();
        }

        private void EnsureAchievementToastPresenter()
        {
            AchievementToastPresenter.EnsureGlobally();
        }

        private void CachePlayerHpFillRectIfNeeded()
        {
            if (_playerHpFillRect == null && _playerHpFill != null)
            {
                _playerHpFillRect = _playerHpFill.rectTransform;
            }
        }

        private void CacheBossHpFillRectIfNeeded()
        {
            if (_bossHpFillRect == null && _bossHpFill != null)
            {
                _bossHpFillRect = _bossHpFill.rectTransform;
            }
        }

        private void LogMissingHudReferences()
        {
            var sb = new StringBuilder();
            void Need(string name, Object obj)
            {
                if (obj == null)
                {
                    sb.AppendLine("  - " + name);
                }
            }

            Need("_timerText", _timerText);
            Need("_scoreText", _scoreText);
            Need("_waveText", _waveText);
            Need("_buildText", _buildText);
            Need("_bossPanel", _bossPanel);
            Need("_bossHpFill", _bossHpFill);
            Need("_bossHpText", _bossHpText);
            Need("_playerHpPanel", _playerHpPanel);
            Need("_playerHpFill", _playerHpFill);
            Need("_playerHpText", _playerHpText);
            Need("_pauseButton", _pauseButton);

            if (_loadoutSlots == null || _loadoutSlots.Length < 4)
            {
                sb.AppendLine("  - _loadoutSlots (need 4 entries)");
            }
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    LoadoutSlotHudEntry e = _loadoutSlots[i];
                    if (e == null || e.Diamond == null || e.Letter == null || e.Count == null)
                    {
                        sb.AppendLine("  - _loadoutSlots[" + i + "] (Diamond, Letter, Count)");
                    }
                }
            }

            if (_createTouchControls)
            {
                Need("_rotateLeftButton", _rotateLeftButton);
                Need("_rotateRightButton", _rotateRightButton);
                Need("_accelerateButton", _accelerateButton);
            }

            if (sb.Length > 0)
            {
                Debug.LogWarning(
                    "[GameplayHUD] Missing serialized references on '" + gameObject.name + "'. Assign them on the prefab or run Static Drift / Setup Gameplay HUD Prefab.\n"
                    + sb,
                    this);
            }
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
            if (_loadoutSlots != null && _loadoutSlots.Length >= 4 && runUpgrades != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    LoadoutSlotHudEntry e = _loadoutSlots[i];
                    if (e == null || e.Count == null || e.Letter == null || e.Diamond == null)
                    {
                        continue;
                    }

                    CardTag tag = runUpgrades.GetLoadoutSlotTag(i);
                    e.Count.text = runUpgrades.GetLoadoutSlotStacksText(i);
                    e.Letter.text = GetHudLoadoutLetter(tag);
                    e.Diamond.color = tag == CardTag.None
                        ? new Color(0.32f, 0.36f, 0.42f, 0.55f)
                        : UpgradeHudVisuals.GetTagColor(tag);
                    e.Letter.color = tag == CardTag.None
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
                    float bossHp = MatchController.Instance.BossHealthNormalized;
                    if (_bossHpFill != null)
                    {
                        _bossHpFill.fillAmount = bossHp;
                    }

                    if (_bossHpFillRect != null)
                    {
                        _bossHpFillRect.anchorMin = new Vector2(0f, 0f);
                        _bossHpFillRect.anchorMax = new Vector2(bossHp, 1f);
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

        private void WireTouchControlZones()
        {
            _controlVisuals.Clear();
            _controlVisualHomePositions.Clear();
            _touchGameplayButtonRoots.Clear();

            if (GameSettings.TouchRotationMode == TouchRotationMode.VirtualJoystick)
            {
                if (_rotateRightButton != null)
                {
                    _rotateRightButton.SetActive(false);
                }

                WireRotateJoystickZone(_rotateLeftButton, "RotateLeftButton");
            }
            else
            {
                DeactivateJoystickOnlyVisuals();
                if (_rotateRightButton != null)
                {
                    _rotateRightButton.SetActive(true);
                }

                WireOneTouchZone(_rotateLeftButton, "RotateLeftButton");
                WireOneTouchZone(_rotateRightButton, "RotateRightButton");
            }

            WireOneTouchZone(_accelerateButton, "AccelerateButton");
        }

        private void DeactivateJoystickOnlyVisuals()
        {
            if (_rotateLeftButton == null)
            {
                return;
            }

            Transform ring = _rotateLeftButton.transform.Find("LimitRing");
            if (ring != null)
            {
                ring.gameObject.SetActive(false);
            }
        }

        private void WireRotateJoystickZone(GameObject zoneRoot, string objectName)
        {
            if (zoneRoot == null)
            {
                return;
            }

            ExpandRotateJoystickZoneRect(zoneRoot);

            EventTrigger old = zoneRoot.GetComponent<EventTrigger>();
            if (old != null)
            {
                Destroy(old);
            }

            Transform visualTf = zoneRoot.transform.Find("Visual");
            if (visualTf == null)
            {
                Debug.LogWarning("[GameplayHUD] Rotate joystick zone '" + objectName + "' has no child 'Visual'.", zoneRoot);
                _touchGameplayButtonRoots.Add(zoneRoot);
                return;
            }

            RectTransform visualRect = visualTf.GetComponent<RectTransform>();
            Image visualImg = visualTf.GetComponent<Image>();
            if (visualImg != null && visualImg.sprite == null)
            {
                visualImg.sprite = GetCircleButtonSprite();
                visualImg.type = Image.Type.Sliced;
            }

            if (visualRect != null)
            {
                EnsureJoystickVisualAnchors(visualRect);
                _controlVisuals[objectName] = visualRect;
                ApplyRotateJoystickHomePosition(zoneRoot, visualRect, objectName);
                EnsureRotateJoystickLimitRing(zoneRoot, visualRect, objectName);
                HideJoystickLabel(zoneRoot);
            }

            AddRotateJoystickEvents(zoneRoot, objectName);
            _touchGameplayButtonRoots.Add(zoneRoot);
        }

        private void HideJoystickLabel(GameObject zoneRoot)
        {
            // Remove the "L" (or any label) from the joystick visuals.
            TMP_Text tmp = zoneRoot.GetComponentInChildren<TMP_Text>(true);
            if (tmp != null)
            {
                tmp.text = string.Empty;
                tmp.enabled = false;
            }

            Text uiText = zoneRoot.GetComponentInChildren<Text>(true);
            if (uiText != null)
            {
                uiText.text = string.Empty;
                uiText.enabled = false;
            }
        }

        private void ApplyRotateJoystickHomePosition(GameObject zoneRoot, RectTransform visualRect, string objectName)
        {
            RectTransform zoneRect = zoneRoot.GetComponent<RectTransform>();
            if (zoneRect == null)
            {
                _controlVisualHomePositions[objectName] = visualRect.anchoredPosition;
                return;
            }

            Vector2 n = new Vector2(Mathf.Clamp01(_rotateJoystickHomeNormalized.x), Mathf.Clamp01(_rotateJoystickHomeNormalized.y));
            Vector2 size = zoneRect.rect.size;
            Vector2 local = new Vector2((n.x - 0.5f) * size.x, (n.y - 0.5f) * size.y);
            visualRect.anchoredPosition = local;
            _controlVisualHomePositions[objectName] = local;
        }

        private void EnsureRotateJoystickLimitRing(GameObject zoneRoot, RectTransform visualRect, string objectName)
        {
            Transform ringTf = zoneRoot.transform.Find("LimitRing");
            if (ringTf == null)
            {
                GameObject ringGo = new GameObject("LimitRing", typeof(RectTransform), typeof(Image));
                ringGo.transform.SetParent(zoneRoot.transform, false);
                ringTf = ringGo.transform;
                ringTf.SetSiblingIndex(visualRect.transform.GetSiblingIndex());
            }

            RectTransform ringRect = ringTf.GetComponent<RectTransform>();
            Image ringImg = ringTf.GetComponent<Image>();
            if (ringRect == null || ringImg == null)
            {
                return;
            }

            EnsureJoystickVisualAnchors(ringRect);
            ringImg.raycastTarget = false;
            ringImg.sprite = GetCircleRingSprite();
            ringImg.type = Image.Type.Sliced;
            ringImg.color = new Color(0.2f, 0.55f, 1f, 0.8f);

            float scale = Mathf.Clamp(_rotateJoystickLimitRingScale, 1.2f, 4f);
            ringRect.sizeDelta = visualRect.sizeDelta * scale;
            ringRect.anchoredPosition = _controlVisualHomePositions.TryGetValue(objectName, out Vector2 home) ? home : visualRect.anchoredPosition;
            ringRect.gameObject.SetActive(true);
            _rotateJoystickRingRect = ringRect;
        }

        private void ExpandRotateJoystickZoneRect(GameObject zoneRoot)
        {
            RectTransform zoneRect = zoneRoot.GetComponent<RectTransform>();
            if (zoneRect == null)
            {
                return;
            }

            // Bigger, easier-to-hit left-side rectangle zone.
            float w = Mathf.Clamp01(_rotateJoystickZoneScreenWidth);
            zoneRect.anchorMin = new Vector2(0f, 0f);
            zoneRect.anchorMax = new Vector2(Mathf.Max(0.05f, w), 1f);
            zoneRect.pivot = new Vector2(0.5f, 0.5f);
            zoneRect.offsetMin = Vector2.zero;
            zoneRect.offsetMax = Vector2.zero;
        }

        private static void EnsureJoystickVisualAnchors(RectTransform visualRect)
        {
            // Make anchoredPosition match parent-local touch coordinates (prevents big offsets).
            visualRect.anchorMin = new Vector2(0.5f, 0.5f);
            visualRect.anchorMax = new Vector2(0.5f, 0.5f);
            visualRect.pivot = new Vector2(0.5f, 0.5f);
        }

        private void WireOneTouchZone(GameObject zoneRoot, string objectName)
        {
            if (zoneRoot == null)
            {
                return;
            }

            EventTrigger old = zoneRoot.GetComponent<EventTrigger>();
            if (old != null)
            {
                Destroy(old);
            }

            Transform visualTf = zoneRoot.transform.Find("Visual");
            if (visualTf == null)
            {
                Debug.LogWarning("[GameplayHUD] Touch zone '" + objectName + "' has no child 'Visual'.", zoneRoot);
                _touchGameplayButtonRoots.Add(zoneRoot);
                return;
            }

            RectTransform visualRect = visualTf.GetComponent<RectTransform>();
            Image visualImg = visualTf.GetComponent<Image>();
            if (visualImg != null && visualImg.sprite == null)
            {
                visualImg.sprite = GetCircleButtonSprite();
                visualImg.type = Image.Type.Sliced;
            }

            if (visualRect != null)
            {
                _controlVisuals[objectName] = visualRect;
                _controlVisualHomePositions[objectName] = visualRect.anchoredPosition;
            }

            AddHoldEvents(zoneRoot, objectName);
            _touchGameplayButtonRoots.Add(zoneRoot);
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
            if (!visible)
            {
                ReleaseAllTouchControlInput();
            }

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

        private void AddHoldEvents(GameObject target, string objectName)
        {
            EventTrigger trigger = target.AddComponent<EventTrigger>();
            trigger.triggers = new List<EventTrigger.Entry>();

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

        private void AddRotateJoystickEvents(GameObject target, string objectName)
        {
            EventTrigger trigger = target.AddComponent<EventTrigger>();
            trigger.triggers = new List<EventTrigger.Entry>();

            AddTrigger(trigger, EventTriggerType.PointerDown, eventData =>
            {
                StartRotateJoystickTouch(objectName, eventData as PointerEventData);
            });
            AddTrigger(trigger, EventTriggerType.Drag, eventData =>
            {
                UpdateRotateJoystickTouch(objectName, eventData as PointerEventData);
            });
            AddTrigger(trigger, EventTriggerType.PointerUp, eventData =>
            {
                EndRotateJoystickTouch(objectName, eventData as PointerEventData);
            });
            AddTrigger(trigger, EventTriggerType.PointerExit, eventData =>
            {
                EndRotateJoystickTouch(objectName, eventData as PointerEventData);
            });
        }

        private void StartRotateJoystickTouch(string objectName, PointerEventData pointerData)
        {
            if (pointerData == null)
            {
                return;
            }

            if (!_controlVisuals.TryGetValue(objectName, out RectTransform visualRect) || visualRect == null)
            {
                return;
            }

            RectTransform zoneRect = visualRect.parent as RectTransform;
            if (zoneRect == null)
            {
                return;
            }

            EnsureJoystickVisualAnchors(visualRect);

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(zoneRect, pointerData.position, pointerData.pressEventCamera, out Vector2 localPoint))
            {
                return;
            }

            _rotateJoystickPointerId = pointerData.pointerId;
            _rotateJoystickHeld = true;
            _rotateJoystickCenterLocal = localPoint;
            visualRect.anchoredPosition = localPoint;
            if (_rotateJoystickRingRect != null)
            {
                EnsureJoystickVisualAnchors(_rotateJoystickRingRect);
                _rotateJoystickRingRect.anchoredPosition = localPoint;
            }
            UpdateRotateFromJoystickAxis(0f);
        }

        private void UpdateRotateJoystickTouch(string objectName, PointerEventData pointerData)
        {
            if (!_rotateJoystickHeld || pointerData == null || pointerData.pointerId != _rotateJoystickPointerId)
            {
                return;
            }

            if (!_controlVisuals.TryGetValue(objectName, out RectTransform visualRect) || visualRect == null)
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

            float radius = Mathf.Max(8f, visualRect.sizeDelta.x * 0.5f);
            Vector2 offset = localPoint - _rotateJoystickCenterLocal;
            if (offset.sqrMagnitude > radius * radius)
            {
                offset = offset.normalized * radius;
            }

            visualRect.anchoredPosition = _rotateJoystickCenterLocal + offset;
            float axisX = Mathf.Clamp(offset.x / radius, -1f, 1f);
            UpdateRotateFromJoystickAxis(axisX);
        }

        private void EndRotateJoystickTouch(string objectName, PointerEventData pointerData)
        {
            if (!_rotateJoystickHeld)
            {
                return;
            }

            if (pointerData != null && pointerData.pointerId != _rotateJoystickPointerId)
            {
                return;
            }

            _rotateJoystickHeld = false;
            _rotateJoystickPointerId = -1;
            UpdateRotateFromJoystickAxis(0f);
            ResetControlVisual(objectName);
            if (_rotateJoystickRingRect != null && _controlVisualHomePositions.TryGetValue(objectName, out Vector2 home))
            {
                _rotateJoystickRingRect.anchoredPosition = home;
            }
        }

        private void UpdateRotateFromJoystickAxis(float axisX)
        {
            float deadZone = Mathf.Clamp01(_rotateJoystickDeadZone);
            _rotateLeftHeld = axisX < -deadZone;
            _rotateRightHeld = axisX > deadZone;
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

            if (_loadoutSlots != null)
            {
                for (int i = 0; i < _loadoutSlots.Length; i++)
                {
                    LoadoutSlotHudEntry e = _loadoutSlots[i];
                    if (e == null)
                    {
                        continue;
                    }

                    if (e.Count != null)
                    {
                        e.Count.fontStyle = FontStyles.Bold;
                        e.Count.fontSize = UseMobileHudLayout() ? 30f : 26f;
                        e.Count.color = new Color(0.88f, 0.9f, 1f, 0.95f);
                        GameFontLibrary.ApplyOutline(e.Count, 0.14f, new Color(0.05f, 0.05f, 0.09f, 0.88f));
                    }

                    if (e.Letter != null)
                    {
                        e.Letter.fontStyle = FontStyles.Bold;
                        e.Letter.fontSize = Mathf.Max(22f, (UseMobileHudLayout() ? 52f : 44f) * 0.58f);
                        GameFontLibrary.ApplyOutline(e.Letter, 0.12f, new Color(0.05f, 0.05f, 0.09f, 0.88f));
                    }
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
            if (_loadoutSlots != null)
            {
                for (int i = 0; i < _loadoutSlots.Length; i++)
                {
                    LoadoutSlotHudEntry e = _loadoutSlots[i];
                    if (e == null)
                    {
                        continue;
                    }

                    if (e.Count != null)
                    {
                        GameFontLibrary.Apply(e.Count);
                    }

                    if (e.Letter != null)
                    {
                        GameFontLibrary.Apply(e.Letter);
                    }
                }
            }
        }

        private static bool UseMobileHudLayout()
        {
            int shortSide = Mathf.Min(Screen.width, Screen.height);
            return Application.isMobilePlatform || shortSide <= 1080;
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

        public void SetPauseButtonHidden(bool hidden)
        {
            if (_pauseButton != null)
            {
                _pauseButton.gameObject.SetActive(!hidden);
            }
        }

        private void WirePauseButton()
        {
            if (_pauseButton == null)
            {
                return;
            }

            Image image = _pauseButton.targetGraphic as Image;
            if (image == null)
            {
                image = _pauseButton.GetComponent<Image>();
            }

            TMP_Text label = _pauseButton.GetComponentInChildren<TMP_Text>(true);
            if (image != null && label != null)
            {
                PixelArtUiSkin.ApplyButtonStyle(_pauseButton, image, label);
            }

            _pauseButton.onClick.RemoveAllListeners();
            _pauseButton.onClick.AddListener(() =>
            {
                MatchController match = MatchController.Instance;
                if (match != null)
                {
                    AudioManager.EnsureExists().PlayUiConfirm();
                    match.TogglePause();
                }
            });
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

        private static Sprite GetCircleRingSprite()
        {
            if (_circleRingSprite != null)
            {
                return _circleRingSprite;
            }

            const int size = 256;
            const float radius = 118f;
            const float thickness = 8f;
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
                    float d = Mathf.Abs(dist - radius);
                    float alpha = 0f;
                    if (d <= thickness)
                    {
                        float t = Mathf.InverseLerp(thickness, 0f, d);
                        alpha = Mathf.Clamp01(t);
                    }

                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            _circleRingSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return _circleRingSprite;
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

        private void OnDisable()
        {
            ReleaseAllTouchControlInput();
        }

        private void ReleaseAllTouchControlInput()
        {
            _rotateLeftHeld = false;
            _rotateRightHeld = false;
            _accelerateHeld = false;
            _rotateJoystickHeld = false;
            _rotateJoystickPointerId = -1;

            if (_playerController == null)
            {
                TryBindPlayerReferences();
            }

            if (_playerController != null)
            {
                _playerController.SetRotateLeftHeld(false);
                _playerController.SetRotateRightHeld(false);
                _playerController.SetAccelerateHeld(false);
            }

            ResetControlVisual("RotateLeftButton");
            ResetControlVisual("AccelerateButton");
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
                        _bossHpFillRect = fill.GetComponent<RectTransform>();
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
                    Transform track = playerHp.Find("Track");
                    Transform fillTf = track != null ? track.Find("Fill") : null;
                    if (fillTf != null)
                    {
                        _playerHpFill = fillTf.GetComponent<Image>();
                        _playerHpFillRect = fillTf.GetComponent<RectTransform>();
                    }

                    Transform labelTransform = playerHp.Find("Label");
                    if (labelTransform != null)
                    {
                        _playerHpText = labelTransform.GetComponent<TMP_Text>();
                    }
                }
            }

            if (_pauseButton == null)
            {
                Transform p = transform.Find("PauseButton");
                if (p != null)
                {
                    _pauseButton = p.GetComponent<Button>();
                }
            }

            if (_rotateLeftButton == null)
            {
                Transform t = transform.Find("RotateLeftButton");
                if (t != null)
                {
                    _rotateLeftButton = t.gameObject;
                }
            }

            if (_rotateRightButton == null)
            {
                Transform t = transform.Find("RotateRightButton");
                if (t != null)
                {
                    _rotateRightButton = t.gameObject;
                }
            }

            if (_accelerateButton == null)
            {
                Transform t = transform.Find("AccelerateButton");
                if (t != null)
                {
                    _accelerateButton = t.gameObject;
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
