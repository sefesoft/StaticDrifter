using UnityEngine;
using UnityEngine.UI;
using TMPro;
using StaticDrift.Achievements;

namespace StaticDrift.UI
{
    /// <summary>Runtime-built scrollable list of achievement status lines with optional scrollbar.</summary>
    public static class AchievementListPanel
    {
        private static int _lastBodyFontSize = 38;
        private static int _lastDescriptionRichSize = 32;

        public readonly struct Style
        {
            public readonly float BodyFontSize;
            public readonly int DescriptionRichSize;
            public readonly float ScrollbarWidth;

            public Style(float bodyFontSize, int descriptionRichSize, float scrollbarWidth = 36f)
            {
                BodyFontSize = bodyFontSize;
                DescriptionRichSize = descriptionRichSize;
                ScrollbarWidth = scrollbarWidth;
            }
        }

        public static TMP_Text CreateScrollingBody(Transform parent, Layout layout, Style style)
        {
            _lastBodyFontSize = Mathf.RoundToInt(style.BodyFontSize);
            _lastDescriptionRichSize = style.DescriptionRichSize;

            GameObject block = new GameObject("AchievementScrollBlock");
            block.transform.SetParent(parent, false);
            RectTransform blockRt = block.AddComponent<RectTransform>();
            ApplyLayout(blockRt, layout);

            float sbw = Mathf.Max(0f, style.ScrollbarWidth);

            GameObject scrollGo = new GameObject("AchievementScroll");
            scrollGo.transform.SetParent(block.transform, false);
            RectTransform scrollAreaRt = scrollGo.AddComponent<RectTransform>();
            scrollAreaRt.anchorMin = Vector2.zero;
            scrollAreaRt.anchorMax = Vector2.one;
            scrollAreaRt.offsetMin = Vector2.zero;
            scrollAreaRt.offsetMax = new Vector2(-(sbw + 6f), 0f);

            ScrollRect scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 42f;
            scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

            Scrollbar scrollbar = null;
            if (sbw > 0.5f)
            {
                scrollbar = BuildVerticalScrollbar(block.transform, sbw);
                scroll.verticalScrollbar = scrollbar;
            }

            GameObject viewportGo = new GameObject("Viewport");
            viewportGo.transform.SetParent(scrollGo.transform, false);
            RectTransform viewportRt = viewportGo.AddComponent<RectTransform>();
            viewportRt.anchorMin = Vector2.zero;
            viewportRt.anchorMax = Vector2.one;
            viewportRt.offsetMin = new Vector2(10f, 8f);
            viewportRt.offsetMax = new Vector2(-10f, -8f);
            Image viewportMask = viewportGo.AddComponent<Image>();
            viewportMask.color = new Color(0.04f, 0.07f, 0.12f, 0.35f);
            viewportMask.raycastTarget = true;
            Mask mask = viewportGo.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            GameObject contentGo = new GameObject("Content");
            contentGo.transform.SetParent(viewportGo.transform, false);
            RectTransform contentRt = contentGo.AddComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = new Vector2(0f, 0f);

            TMP_Text tmp = contentGo.AddComponent<TextMeshProUGUI>();
            tmp.text = AchievementProgress.BuildScrollBodyText(style.DescriptionRichSize);
            GameFontLibrary.Apply(tmp);
            tmp.fontSize = style.BodyFontSize;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.TopLeft;
            tmp.color = new Color(0.9f, 0.96f, 1f, 1f);
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.raycastTarget = false;
            GameFontLibrary.ApplyOutline(tmp, 0.22f, new Color(0.02f, 0.04f, 0.08f, 1f));

            ContentSizeFitter fitter = contentGo.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            scroll.viewport = viewportRt;
            scroll.content = contentRt;

            return tmp;
        }

        private static Scrollbar BuildVerticalScrollbar(Transform blockParent, float width)
        {
            GameObject sbGo = new GameObject("Scrollbar");
            sbGo.transform.SetParent(blockParent, false);
            RectTransform sbRt = sbGo.AddComponent<RectTransform>();
            sbRt.anchorMin = new Vector2(1f, 0f);
            sbRt.anchorMax = new Vector2(1f, 1f);
            sbRt.pivot = new Vector2(1f, 0.5f);
            sbRt.sizeDelta = new Vector2(width, 0f);
            sbRt.offsetMin = new Vector2(-width, 10f);
            sbRt.offsetMax = new Vector2(-2f, -10f);

            Image track = sbGo.AddComponent<Image>();
            track.color = new Color(0.07f, 0.11f, 0.18f, 0.96f);
            track.raycastTarget = true;

            Scrollbar scrollbar = sbGo.AddComponent<Scrollbar>();
            scrollbar.direction = Scrollbar.Direction.BottomToTop;

            GameObject sliding = new GameObject("SlidingArea");
            sliding.transform.SetParent(sbGo.transform, false);
            RectTransform slidingRt = sliding.AddComponent<RectTransform>();
            slidingRt.anchorMin = new Vector2(0.1f, 0.04f);
            slidingRt.anchorMax = new Vector2(0.9f, 0.96f);
            slidingRt.offsetMin = Vector2.zero;
            slidingRt.offsetMax = Vector2.zero;

            GameObject handle = new GameObject("Handle");
            handle.transform.SetParent(sliding.transform, false);
            RectTransform handleRt = handle.AddComponent<RectTransform>();
            handleRt.anchorMin = new Vector2(0f, 0f);
            handleRt.anchorMax = new Vector2(1f, 1f);
            handleRt.pivot = new Vector2(0.5f, 0.5f);
            handleRt.sizeDelta = new Vector2(0f, 48f);

            Image handleImage = handle.AddComponent<Image>();
            handleImage.color = new Color(0.42f, 0.78f, 1f, 0.92f);
            handleImage.raycastTarget = true;

            scrollbar.handleRect = handleRt;
            scrollbar.targetGraphic = handleImage;
            scrollbar.size = 0.25f;

            return scrollbar;
        }

        public static void RefreshBodyText(TMP_Text text)
        {
            if (text != null)
            {
                text.text = AchievementProgress.BuildScrollBodyText(_lastDescriptionRichSize);
                text.fontSize = _lastBodyFontSize;
            }
        }

        public readonly struct Layout
        {
            public readonly Vector2 AnchorMin;
            public readonly Vector2 AnchorMax;
            public readonly Vector2 OffsetMin;
            public readonly Vector2 OffsetMax;

            public Layout(Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
            {
                AnchorMin = anchorMin;
                AnchorMax = anchorMax;
                OffsetMin = offsetMin;
                OffsetMax = offsetMax;
            }
        }

        private static void ApplyLayout(RectTransform rt, Layout layout)
        {
            rt.anchorMin = layout.AnchorMin;
            rt.anchorMax = layout.AnchorMax;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = layout.OffsetMin;
            rt.offsetMax = layout.OffsetMax;
            rt.localScale = Vector3.one;
        }
    }
}
