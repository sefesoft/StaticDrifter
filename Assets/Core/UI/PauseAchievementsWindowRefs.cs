using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace StaticDrift.UI
{
    /// <summary>Prefab root for the pause achievements popup (parented under safe-area host at runtime).</summary>
    public sealed class PauseAchievementsWindowRefs : MonoBehaviour
    {
        /// <summary>Scroll body text (AchievementScrollBlock / Content TMP).</summary>
        public TMP_Text AchievementBodyText;
        public Button CloseAchievementsButton;
    }
}
