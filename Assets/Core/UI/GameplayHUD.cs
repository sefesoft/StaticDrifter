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
        [SerializeField] private string _statsFormat = "HP: {0:F0} / {1:F0}";
        [SerializeField] private bool _createTouchControls = true;

        private PlayerHealth _playerHealth;
        private PlayerController _playerController;
        private float _localElapsed;
        private bool _rotateLeftHeld;
        private bool _rotateRightHeld;
        private bool _accelerateHeld;
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

            ApplyHudVisualStyle();
        }

        private void Update()
        {
            float timeToShow = MatchController.Instance != null
                ? MatchController.Instance.MatchTime
                : (_localElapsed += Time.deltaTime);

            if (_timerText != null)
            {
                int totalSeconds = Mathf.FloorToInt(timeToShow);
                int minutes = totalSeconds / 60;
                int seconds = totalSeconds % 60;
                _timerText.text = minutes.ToString("00") + ":" + seconds.ToString("00");
            }

            if (_playerStatsText != null && _playerHealth != null)
            {
                _playerStatsText.text = string.Format(_statsFormat, _playerHealth.CurrentHealth, _playerHealth.MaxHealth);
            }
            else if (_playerStatsText != null && _playerHealth == null)
            {
                _playerStatsText.text = "HP: —";
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
                CreateControlButton("RotateLeftButton", new Vector2(30f, 30f), new Vector2(0f, 0f), "L");
            }

            Transform rotateRight = transform.Find("RotateRightButton");
            if (rotateRight == null)
            {
                CreateControlButton("RotateRightButton", new Vector2(230f, 30f), new Vector2(0f, 0f), "R");
            }

            Transform accelerate = transform.Find("AccelerateButton");
            if (accelerate == null)
            {
                CreateControlButton("AccelerateButton", new Vector2(-30f, 30f), new Vector2(1f, 0f), "A");
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

        private void CreateControlButton(string objectName, Vector2 anchoredPosition, Vector2 anchorPivot, string label)
        {
            GameObject buttonGo = new GameObject(objectName);
            buttonGo.transform.SetParent(transform, false);

            RectTransform rect = buttonGo.AddComponent<RectTransform>();
            rect.anchorMin = anchorPivot;
            rect.anchorMax = anchorPivot;
            rect.pivot = anchorPivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(150f, 150f);

            Image bg = buttonGo.AddComponent<Image>();
            bg.sprite = GetCircleButtonSprite();
            bg.type = Image.Type.Sliced;
            bg.color = new Color(0.12f, 0.16f, 0.25f, 0.45f);

            AddHoldEvents(buttonGo, objectName);

            GameObject textGo = new GameObject("Label");
            textGo.transform.SetParent(buttonGo.transform, false);
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
        }

        private void AddHoldEvents(GameObject target, string objectName)
        {
            EventTrigger trigger = target.AddComponent<EventTrigger>();
            trigger.triggers = new System.Collections.Generic.List<EventTrigger.Entry>();

            AddTrigger(trigger, EventTriggerType.PointerDown, () => SetControlState(objectName, true));
            AddTrigger(trigger, EventTriggerType.PointerUp, () => SetControlState(objectName, false));
            AddTrigger(trigger, EventTriggerType.PointerExit, () => SetControlState(objectName, false));
        }

        private static void AddTrigger(EventTrigger trigger, EventTriggerType type, System.Action callback)
        {
            EventTrigger.Entry entry = new EventTrigger.Entry { eventID = type };
            entry.callback.AddListener(_ => callback());
            trigger.triggers.Add(entry);
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
    }
}
