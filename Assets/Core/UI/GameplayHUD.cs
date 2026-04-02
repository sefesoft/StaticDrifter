using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using System.Collections.Generic;
using StaticDrift.Player;
using StaticDrift.Managers;
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
        private static Sprite _circleButtonSprite;

        private const string PlayerTag = "Player";

        private void Start()
        {
            GameObject playerGo = GameObject.FindGameObjectWithTag(PlayerTag);
            if (playerGo != null)
            {
                _playerHealth = playerGo.GetComponent<PlayerHealth>();
                if (_playerHealth == null)
                {
                    _playerHealth = playerGo.GetComponentInChildren<PlayerHealth>();
                }

                _playerController = playerGo.GetComponent<PlayerController>();
                if (_playerController == null)
                {
                    _playerController = playerGo.GetComponentInChildren<PlayerController>();
                }
            }

            if (_createTouchControls)
            {
                EnsureTouchControls();
            }

            EnsureInfoLabels();
            EnsureBossPanel();
            EnsurePlayerHpPanel();
            EnsurePauseButton();

            if (_playerStatsText != null)
            {
                _playerStatsText.gameObject.SetActive(false);
            }

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

            if (_buildText != null && MatchController.Instance != null)
            {
                _buildText.text = "BUILD " + MatchController.Instance.BuildSummary;
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

            if (_playerHpFill != null && _playerHealth != null)
            {
                _playerHpFill.fillAmount = Mathf.Clamp01(_playerHealth.CurrentHealth / Mathf.Max(1f, _playerHealth.MaxHealth));
            }

            if (_playerHpText != null && _playerHealth != null)
            {
                _playerHpText.text = string.Format(_statsFormat, _playerHealth.CurrentHealth, _playerHealth.MaxHealth);
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
                _timerText.color = new Color(0.79f, 0.95f, 1f, 1f);
                _timerText.outlineWidth = 0.22f;
                _timerText.outlineColor = new Color(0.02f, 0.04f, 0.08f, 0.95f);
            }

            if (_playerStatsText != null)
            {
                _playerStatsText.fontStyle = FontStyles.Bold;
                _playerStatsText.color = new Color(0.95f, 0.98f, 1f, 0.98f);
                _playerStatsText.outlineWidth = 0.2f;
                _playerStatsText.outlineColor = new Color(0.04f, 0.06f, 0.1f, 0.9f);
            }

            if (_scoreText != null)
            {
                _scoreText.fontStyle = FontStyles.Bold;
                _scoreText.color = new Color(1f, 0.89f, 0.45f, 0.98f);
                _scoreText.outlineWidth = 0.18f;
                _scoreText.outlineColor = new Color(0.06f, 0.06f, 0.08f, 0.9f);
            }

            if (_waveText != null)
            {
                _waveText.fontStyle = FontStyles.Bold;
                _waveText.color = new Color(0.73f, 1f, 0.86f, 0.98f);
                _waveText.outlineWidth = 0.18f;
                _waveText.outlineColor = new Color(0.04f, 0.08f, 0.07f, 0.9f);
            }

            if (_buildText != null)
            {
                _buildText.fontStyle = FontStyles.Bold;
                _buildText.color = new Color(0.86f, 0.83f, 1f, 0.98f);
                _buildText.outlineWidth = 0.16f;
                _buildText.outlineColor = new Color(0.05f, 0.05f, 0.09f, 0.9f);
                _buildText.enableWordWrapping = true;
                _buildText.fontSize = 28f;
                _buildText.lineSpacing = -20f;
            }

            if (_playerHpText != null)
            {
                _playerHpText.fontStyle = FontStyles.Bold;
                _playerHpText.color = new Color(0.95f, 0.98f, 1f, 1f);
                _playerHpText.outlineWidth = 0.16f;
                _playerHpText.outlineColor = new Color(0.04f, 0.06f, 0.1f, 0.9f);
            }

            if (_bossHpText != null)
            {
                _bossHpText.outlineWidth = 0.16f;
                _bossHpText.outlineColor = new Color(0.1f, 0.04f, 0.04f, 0.95f);
            }
        }

        private void EnsureInfoLabels()
        {
            if (_scoreText == null)
            {
                _scoreText = CreateInfoLabel("ScoreText", new Vector2(0.04f, 0.955f), TextAlignmentOptions.Left, "SCORE 0", new Vector2(320f, 60f));
            }
            if (_waveText == null)
            {
                _waveText = CreateInfoLabel("WaveText", new Vector2(0.965f, 0.955f), TextAlignmentOptions.Right, "WAVE 1", new Vector2(260f, 60f));
            }
            if (_buildText == null)
            {
                _buildText = CreateInfoLabel("BuildText", new Vector2(0.965f, 0.90f), TextAlignmentOptions.Right, "BUILD V0 K0 T0 S0", new Vector2(500f, 92f));
            }
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
            rect.sizeDelta = new Vector2(640f, 34f);

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
            labelText.fontSize = 24f;
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
                return;
            }

            Transform existing = transform.Find("PlayerHpPanel");
            if (existing != null)
            {
                _playerHpPanel = existing.gameObject;
                Transform fill = existing.Find("Fill");
                if (fill != null)
                {
                    _playerHpFill = fill.GetComponent<Image>();
                }

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
            rect.sizeDelta = new Vector2(360f, 42f);

            Image bg = panel.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.12f, 0.18f, 0.92f);

            GameObject fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(panel.transform, false);
            RectTransform fillRect = fillGo.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(4f, 4f);
            fillRect.offsetMax = new Vector2(-4f, -4f);
            Image fillImage = fillGo.AddComponent<Image>();
            fillImage.type = Image.Type.Filled;
            fillImage.fillMethod = Image.FillMethod.Horizontal;
            fillImage.fillOrigin = 0;
            fillImage.fillAmount = 1f;
            fillImage.color = new Color(0.2f, 0.78f, 0.42f, 0.95f);

            GameObject labelGo = new GameObject("Label");
            labelGo.transform.SetParent(panel.transform, false);
            RectTransform labelRect = labelGo.AddComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            TMP_Text labelText = labelGo.AddComponent<TextMeshProUGUI>();
            labelText.fontSize = 24f;
            labelText.fontStyle = FontStyles.Bold;
            labelText.alignment = TextAlignmentOptions.Center;
            labelText.color = new Color(0.95f, 0.98f, 1f, 1f);
            labelText.text = "HP: 100 / 100";
            labelText.raycastTarget = false;

            _playerHpPanel = panel;
            _playerHpFill = fillImage;
            _playerHpText = labelText;
        }

        private void EnsurePauseButton()
        {
            if (_pauseButton != null)
            {
                return;
            }

            Transform existing = transform.Find("PauseButton");
            if (existing != null)
            {
                _pauseButton = existing.GetComponent<Button>();
                return;
            }

            GameObject buttonGo = new GameObject("PauseButton");
            buttonGo.transform.SetParent(transform, false);
            RectTransform rect = buttonGo.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-36f, -32f);
            rect.sizeDelta = new Vector2(100f, 100f);

            Image image = buttonGo.AddComponent<Image>();
            image.sprite = GetCircleButtonSprite();
            image.type = Image.Type.Sliced;
            image.color = new Color(0.12f, 0.16f, 0.25f, 0.62f);

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

            _pauseButton = button;
        }

        private TMP_Text CreateInfoLabel(string name, Vector2 anchor, TextAlignmentOptions alignment, string startText, Vector2 size)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(anchor.x < 0.5f ? 0f : 1f, 0.5f);
            rect.sizeDelta = size;
            TMP_Text text = go.AddComponent<TextMeshProUGUI>();
            text.text = startText;
            text.fontSize = 34f;
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
        }

        private TMP_Text FindTextByName(string name)
        {
            Transform t = transform.Find(name);
            if (t == null)
            {
                return null;
            }
            return t.GetComponent<TMP_Text>();
        }
    }
}
