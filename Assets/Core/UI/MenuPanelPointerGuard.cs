using UnityEngine;
using UnityEngine.EventSystems;

namespace StaticDrift.UI
{
    /// <summary>
    /// Put on a fullscreen UI panel. When the user presses on that panel (empty areas or non-interactive text),
    /// restores selection immediately so gamepad/keyboard navigation is not lost after EventSystem clears focus.
    /// </summary>
    public class MenuPanelPointerGuard : MonoBehaviour, IPointerDownHandler
    {
        [SerializeField] private GameObject _defaultSelection;

        public GameObject DefaultSelection
        {
            get => _defaultSelection;
            set => _defaultSelection = value;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            ApplySelection();
        }

        public void ApplySelection()
        {
            if (_defaultSelection == null || !_defaultSelection.activeInHierarchy)
            {
                return;
            }

            EventSystem es = EventSystem.current;
            if (es == null)
            {
                return;
            }

            es.firstSelectedGameObject = _defaultSelection;
            es.SetSelectedGameObject(_defaultSelection);
        }
    }
}
