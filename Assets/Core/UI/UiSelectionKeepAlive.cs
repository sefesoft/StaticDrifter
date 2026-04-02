using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace StaticDrift.UI
{
    /// <summary>
    /// Restores EventSystem selection when a touch/click on empty UI clears it, so gamepad/keyboard navigation keeps working.
    /// Runs late so it wins over default EventSystem processing order.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public class UiSelectionKeepAlive : MonoBehaviour
    {
        [SerializeField] private GameObject _defaultSelection;

        public GameObject DefaultSelection
        {
            get => _defaultSelection;
            set => _defaultSelection = value;
        }

        private void LateUpdate()
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

            if (es.currentSelectedGameObject != null)
            {
                return;
            }

            Selectable sel = _defaultSelection.GetComponent<Selectable>();
            if (sel != null && !sel.IsInteractable())
            {
                return;
            }

            es.firstSelectedGameObject = _defaultSelection;
            es.SetSelectedGameObject(_defaultSelection);
        }
    }
}
