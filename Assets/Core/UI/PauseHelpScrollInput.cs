using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace StaticDrift.UI
{
    /// <summary>
    /// Drives pause help ScrollRects with keyboard (Page Up/Down), gamepad left stick; touch drag and mouse wheel use Unity ScrollRect.
    /// </summary>
    public class PauseHelpScrollInput : MonoBehaviour
    {
        [SerializeField] private ScrollRect _itemsScroll;
        [SerializeField] private ScrollRect _upgradesScroll;

        private void Update()
        {
            ScrollRect active = GetActiveScroll();
            if (active == null || !active.vertical)
            {
                return;
            }

            float delta = 0f;
            float dt = Time.unscaledDeltaTime;

            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.pageDownKey.isPressed)
                {
                    delta -= 1.8f * dt;
                }

                if (kb.pageUpKey.isPressed)
                {
                    delta += 1.8f * dt;
                }
            }

            Gamepad gp = Gamepad.current;
            if (gp != null)
            {
                float y = gp.leftStick.ReadValue().y;
                if (Mathf.Abs(y) > 0.18f)
                {
                    delta += y * 2.2f * dt;
                }
            }

            if (Mathf.Abs(delta) < 0.00001f)
            {
                return;
            }

            active.verticalNormalizedPosition = Mathf.Clamp01(active.verticalNormalizedPosition + delta);
        }

        private ScrollRect GetActiveScroll()
        {
            if (_upgradesScroll != null && _upgradesScroll.gameObject.activeInHierarchy)
            {
                return _upgradesScroll;
            }

            if (_itemsScroll != null && _itemsScroll.gameObject.activeInHierarchy)
            {
                return _itemsScroll;
            }

            return null;
        }

        public void SetScrollRects(ScrollRect items, ScrollRect upgrades)
        {
            _itemsScroll = items;
            _upgradesScroll = upgrades;
        }
    }
}
