using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace StaticDrift.Managers
{
    /// <summary>
    /// Enables URP post-processing on the main camera and drives a global volume (bloom, vignette, etc.).
    /// Profile: Resources/PostProcess/GameplayVfxVolumeProfile.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class GameplayPostProcessBootstrap : MonoBehaviour
    {
        private const string ProfileResourcePath = "PostProcess/GameplayVfxVolumeProfile";

        private void Awake()
        {
            VolumeProfile profile = Resources.Load<VolumeProfile>(ProfileResourcePath);
            if (profile == null)
            {
                Debug.LogWarning($"[GameplayPostProcess] Missing VolumeProfile at Resources/{ProfileResourcePath}.asset");
            }

            Volume volume = gameObject.GetComponent<Volume>();
            if (volume == null)
            {
                volume = gameObject.AddComponent<Volume>();
            }

            volume.isGlobal = true;
            volume.priority = 0f;
            volume.weight = 1f;
            if (profile != null)
            {
                volume.profile = profile;
            }

            Camera cam = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            if (cam != null)
            {
                UniversalAdditionalCameraData data = cam.GetUniversalAdditionalCameraData();
                if (data != null)
                {
                    data.renderPostProcessing = true;
                }
            }
        }
    }
}
