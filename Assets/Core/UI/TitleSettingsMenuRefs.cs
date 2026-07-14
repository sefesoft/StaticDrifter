using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace StaticDrift.UI
{
    /// <summary>Serialized wiring for the title settings/options panel.</summary>
    public sealed class TitleSettingsMenuRefs : MonoBehaviour
    {
        public Slider MusicVolumeSlider;
        public Slider SfxVolumeSlider;
        public Slider SensitivitySlider;
        public Toggle TouchRotationToggle;
        public TMP_Text MusicVolumeValueText;
        public TMP_Text SfxVolumeValueText;
        public TMP_Text SensitivityValueText;
        public TMP_Text TouchRotationValueText;
        public Button BackButton;
    }
}
