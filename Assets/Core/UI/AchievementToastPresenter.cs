using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using StaticDrift.Achievements;

namespace StaticDrift.UI
{
    /// <summary>
    /// Toasts live on a separate overlay canvas so wave interludes (which disable GameplayHUD_Canvas)
    /// do not stop coroutines — fixes Clean Sector and other end-of-wave unlocks appearing "stuck".
    /// </summary>
    [DisallowMultipleComponent]
    public class AchievementToastPresenter : MonoBehaviour
    {
        private const string OverlayObjectName = "AchievementToastOverlay";

        private static AchievementToastPresenter _instance;

        [SerializeField] private float _stripWidth = 300f;
        [SerializeField] private float _marginFromRight = 12f;
        [SerializeField] private float _verticalAnchorMin = 0.24f;
        [SerializeField] private float _verticalAnchorMax = 0.74f;
        [SerializeField] private float _slideInSeconds = 0.32f;
        [SerializeField] private float _holdSeconds = 2.35f;
        [SerializeField] private float _slideOutSeconds = 0.22f;

        private RectTransform _stripRect;
        private readonly Queue<AchievementId> _queue = new Queue<AchievementId>(4);
        private Coroutine _runner;
        private bool _built;

        /// <summary>Creates the overlay canvas + presenter if missing. Safe to call from GameplayHUD.Start.</summary>
        public static void EnsureGlobally()
        {
            if (_instance != null)
            {
                return;
            }

            GameObject existing = GameObject.Find(OverlayObjectName);
            if (existing != null)
            {
                _instance = existing.GetComponent<AchievementToastPresenter>();
                if (_instance == null)
                {
                    _instance = existing.AddComponent<AchievementToastPresenter>();
                }

                return;
            }

            GameObject go = new GameObject(OverlayObjectName);
            Canvas canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 560;
            CanvasScaler scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            // Canvas replaces Transform with RectTransform; do not AddComponent<RectTransform> (returns null).
            RectTransform rootRt = go.GetComponent<RectTransform>();
            if (rootRt != null)
            {
                rootRt.anchorMin = Vector2.zero;
                rootRt.anchorMax = Vector2.one;
                rootRt.offsetMin = Vector2.zero;
                rootRt.offsetMax = Vector2.zero;
            }

            _instance = go.AddComponent<AchievementToastPresenter>();
        }

        private void Awake()
        {
            if (_instance == null)
            {
                _instance = this;
            }

            BuildStripIfNeeded();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void OnEnable()
        {
            AchievementProgress.NewlyUnlocked += OnAchievementUnlocked;
        }

        private void OnDisable()
        {
            AchievementProgress.NewlyUnlocked -= OnAchievementUnlocked;
        }

        private void BuildStripIfNeeded()
        {
            if (_built)
            {
                return;
            }

            Transform existing = transform.Find("AchievementToastStrip");
            if (existing != null)
            {
                _stripRect = existing.GetComponent<RectTransform>();
                _built = _stripRect != null;
                return;
            }

            GameObject stripGo = new GameObject("AchievementToastStrip");
            stripGo.transform.SetParent(transform, false);
            stripGo.transform.SetAsLastSibling();
            RectTransform rect = stripGo.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, _verticalAnchorMin);
            rect.anchorMax = new Vector2(1f, _verticalAnchorMax);
            rect.pivot = new Vector2(1f, 0.5f);
            rect.offsetMin = new Vector2(-_stripWidth - _marginFromRight, 0f);
            rect.offsetMax = new Vector2(-_marginFromRight, 0f);
            _stripRect = rect;
            _built = true;
        }

        private void OnAchievementUnlocked(AchievementId id)
        {
            if (_stripRect == null)
            {
                BuildStripIfNeeded();
            }

            if (_stripRect == null)
            {
                return;
            }

            _queue.Enqueue(id);
            if (_runner == null)
            {
                _runner = StartCoroutine(ProcessQueueRoutine());
            }
        }

        private IEnumerator ProcessQueueRoutine()
        {
            while (_queue.Count > 0)
            {
                AchievementId id = _queue.Dequeue();
                yield return ShowOneToast(id);
            }

            _runner = null;
        }

        private IEnumerator ShowOneToast(AchievementId id)
        {
            float w = Mathf.Min(_stripWidth - 8f, 292f);
            float h = UseCompactMobileLayout() ? 128f : 112f;

            GameObject card = new GameObject("AchievementToast");
            card.transform.SetParent(_stripRect, false);
            RectTransform crt = card.AddComponent<RectTransform>();
            crt.anchorMin = new Vector2(1f, 0.5f);
            crt.anchorMax = new Vector2(1f, 0.5f);
            crt.pivot = new Vector2(1f, 0.5f);
            crt.sizeDelta = new Vector2(w, h);
            crt.anchoredPosition = new Vector2(72f, 0f);

            Image bg = card.AddComponent<Image>();
            bg.color = new Color(0.05f, 0.09f, 0.14f, 0.96f);
            bg.raycastTarget = false;
            Outline outline = card.AddComponent<Outline>();
            outline.effectColor = new Color(0.65f, 0.86f, 1f, 0.5f);
            outline.effectDistance = new Vector2(2f, -2f);
            outline.useGraphicAlpha = false;

            CanvasGroup cg = card.AddComponent<CanvasGroup>();
            cg.blocksRaycasts = false;
            cg.interactable = false;

            GameObject headerGo = new GameObject("Header");
            headerGo.transform.SetParent(card.transform, false);
            RectTransform headerRt = headerGo.AddComponent<RectTransform>();
            headerRt.anchorMin = new Vector2(0f, 0.58f);
            headerRt.anchorMax = new Vector2(1f, 1f);
            headerRt.offsetMin = new Vector2(14f, 0f);
            headerRt.offsetMax = new Vector2(-12f, -8f);
            TMP_Text header = headerGo.AddComponent<TextMeshProUGUI>();
            header.text = "ACHIEVEMENT UNLOCKED";
            GameFontLibrary.Apply(header);
            header.fontSize = UseCompactMobileLayout() ? 22f : 20f;
            header.fontStyle = FontStyles.Bold;
            header.alignment = TextAlignmentOptions.Left;
            header.color = new Color(0.65f, 0.88f, 1f, 0.95f);
            header.textWrappingMode = TextWrappingModes.NoWrap;
            header.raycastTarget = false;
            GameFontLibrary.ApplyOutline(header, 0.18f, new Color(0.02f, 0.04f, 0.08f, 1f));

            GameObject titleGo = new GameObject("Title");
            titleGo.transform.SetParent(card.transform, false);
            RectTransform titleRt = titleGo.AddComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 0f);
            titleRt.anchorMax = new Vector2(1f, 0.58f);
            titleRt.offsetMin = new Vector2(14f, 10f);
            titleRt.offsetMax = new Vector2(-12f, -4f);
            TMP_Text title = titleGo.AddComponent<TextMeshProUGUI>();
            title.text = AchievementProgress.GetTitle(id);
            GameFontLibrary.Apply(title);
            title.fontSize = UseCompactMobileLayout() ? 30f : 26f;
            title.fontStyle = FontStyles.Bold;
            title.alignment = TextAlignmentOptions.Left;
            title.color = new Color(0.92f, 0.98f, 1f, 1f);
            title.textWrappingMode = TextWrappingModes.Normal;
            title.overflowMode = TextOverflowModes.Ellipsis;
            title.raycastTarget = false;
            GameFontLibrary.ApplyOutline(title, 0.2f, new Color(0.02f, 0.04f, 0.08f, 1f));

            float startX = 86f;
            float endX = 0f;
            float t = 0f;
            while (t < _slideInSeconds)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / Mathf.Max(0.01f, _slideInSeconds));
                float e = 1f - Mathf.Pow(1f - u, 3f);
                crt.anchoredPosition = new Vector2(Mathf.Lerp(startX, endX, e), 0f);
                yield return null;
            }

            crt.anchoredPosition = new Vector2(endX, 0f);

            yield return new WaitForSecondsRealtime(_holdSeconds);

            t = 0f;
            while (t < _slideOutSeconds)
            {
                t += Time.unscaledDeltaTime;
                float u = Mathf.Clamp01(t / Mathf.Max(0.01f, _slideOutSeconds));
                crt.anchoredPosition = new Vector2(Mathf.Lerp(endX, startX + 48f, u), 0f);
                cg.alpha = 1f - u;
                yield return null;
            }

            if (card != null)
            {
                Destroy(card);
            }
        }

        private static bool UseCompactMobileLayout()
        {
            int shortSide = Mathf.Min(Screen.width, Screen.height);
            return Application.isMobilePlatform || shortSide <= 900;
        }
    }
}
