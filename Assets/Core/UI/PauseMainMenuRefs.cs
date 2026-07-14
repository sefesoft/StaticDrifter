using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace StaticDrift.UI
{
    /// <summary>Prefab root for the pause overlay main column (under the dimmed panel).</summary>
    public sealed class PauseMainMenuRefs : MonoBehaviour
    {
        public TMP_Text PauseTitle;
        public TMP_Text PauseHint;
        public Button ResumeButton;
        public Button AchievementsFromPauseButton;
        public Button InfoFromPauseButton;
        public Button RetryFromPauseButton;
        public Button TitleFromPauseButton;
    }
}
