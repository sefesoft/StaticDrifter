using UnityEngine;

namespace StaticDrift.Managers
{
    public enum TouchRotationMode
    {
        LeftRightButtons = 0,
        VirtualJoystick = 1
    }

    public static class GameSettings
    {
        private const string MusicVolumeKey = "Settings.MusicVolume";
        private const string SfxVolumeKey = "Settings.SfxVolume";
        private const string LegacyMasterVolumeKey = "Settings.MasterVolume";
        private const string RotationSensitivityKey = "Settings.RotationSensitivity";
        private const string TouchRotationModeKey = "Settings.TouchRotationMode";

        public static float MusicVolume { get; private set; } = 1f;
        public static float SfxVolume { get; private set; } = 1f;
        public static float RotationSensitivity { get; private set; } = 1f;
        public static TouchRotationMode TouchRotationMode { get; private set; } = TouchRotationMode.LeftRightButtons;

        public static void Load()
        {
            if (!PlayerPrefs.HasKey(MusicVolumeKey))
            {
                float legacy = Mathf.Clamp01(PlayerPrefs.GetFloat(LegacyMasterVolumeKey, 1f));
                MusicVolume = legacy;
                SfxVolume = legacy;
                PlayerPrefs.SetFloat(MusicVolumeKey, MusicVolume);
                PlayerPrefs.SetFloat(SfxVolumeKey, SfxVolume);
                PlayerPrefs.Save();
            }
            else
            {
                MusicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(MusicVolumeKey, 1f));
                SfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, 1f));
            }

            RotationSensitivity = Mathf.Clamp(PlayerPrefs.GetFloat(RotationSensitivityKey, 1f), 0.5f, 2f);

            int modeInt = PlayerPrefs.GetInt(TouchRotationModeKey, (int)TouchRotationMode.LeftRightButtons);
            TouchRotationMode = modeInt == (int)TouchRotationMode.LeftRightButtons
                ? TouchRotationMode.LeftRightButtons
                : TouchRotationMode.VirtualJoystick;

            ApplyRuntimeSettings();
        }

        public static void SetMusicVolume(float value)
        {
            MusicVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(MusicVolumeKey, MusicVolume);
            PlayerPrefs.Save();
            ApplyRuntimeSettings();
        }

        public static void SetSfxVolume(float value)
        {
            SfxVolume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(SfxVolumeKey, SfxVolume);
            PlayerPrefs.Save();
            ApplyRuntimeSettings();
        }

        public static void SetRotationSensitivity(float value)
        {
            RotationSensitivity = Mathf.Clamp(value, 0.5f, 2f);
            PlayerPrefs.SetFloat(RotationSensitivityKey, RotationSensitivity);
            PlayerPrefs.Save();
        }

        public static void SetTouchRotationMode(TouchRotationMode mode)
        {
            TouchRotationMode = mode;
            PlayerPrefs.SetInt(TouchRotationModeKey, (int)mode);
            PlayerPrefs.Save();
        }

        public static void ApplyRuntimeSettings()
        {
            AudioListener.volume = 1f;
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.ApplyVolumeFromSettings();
            }
        }
    }
}
