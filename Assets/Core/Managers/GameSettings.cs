using UnityEngine;

namespace StaticDrift.Managers
{
    public static class GameSettings
    {
        private const string MasterVolumeKey = "Settings.MasterVolume";
        private const string RotationSensitivityKey = "Settings.RotationSensitivity";

        public static float MasterVolume { get; private set; } = 1f;
        public static float RotationSensitivity { get; private set; } = 1f;

        public static void Load()
        {
            MasterVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MasterVolumeKey, 1f));
            RotationSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(RotationSensitivityKey, 1f), 0.5f, 2f);
            ApplyRuntimeSettings();
        }

        public static void SetMasterVolume(float value)
        {
            MasterVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MasterVolumeKey, MasterVolume);
            PlayerPrefs.Save();
            ApplyRuntimeSettings();
        }

        public static void SetRotationSensitivity(float value)
        {
            RotationSensitivity = Mathf.Clamp(value, 0.5f, 2f);
            PlayerPrefs.SetFloat(RotationSensitivityKey, RotationSensitivity);
            PlayerPrefs.Save();
        }

        public static void ApplyRuntimeSettings()
        {
            AudioListener.volume = MasterVolume;
        }
    }
}
