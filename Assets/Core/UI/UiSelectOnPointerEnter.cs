using UnityEngine;
using UnityEngine.EventSystems;

namespace StaticDrift.UI
{
    /// <summary>
    /// When the pointer enters this GameObject (mouse hover or touch), make it the sole UI selection
    /// so "selected" and "highlighted" do not stack on two different controls at once.
    /// </summary>
    public class UiSelectOnPointerEnter : MonoBehaviour, IPointerEnterHandler
    {
        public void OnPointerEnter(PointerEventData eventData)
        {
            EventSystem es = EventSystem.current;
            if (es == null)
            {
                return;
            }

            if (es.currentSelectedGameObject == gameObject)
            {
                return;
            }

            es.SetSelectedGameObject(gameObject);
        }
    }
}
