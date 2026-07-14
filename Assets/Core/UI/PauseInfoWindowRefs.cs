using UnityEngine;
using UnityEngine.UI;

namespace StaticDrift.UI
{
    /// <summary>Prefab root for the pause help / reference popup.</summary>
    public sealed class PauseInfoWindowRefs : MonoBehaviour
    {
        public Button CloseInfoButton;
        public Button ItemsTabButton;
        public Button UpgradesTabButton;
        public ScrollRect ItemsScroll;
        public ScrollRect UpgradesScroll;
        public PauseHelpScrollInput ScrollInput;
    }
}
