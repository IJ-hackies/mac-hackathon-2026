using UnityEngine;

namespace Player.UI
{
    public static class GameSettings
    {
        public const float DefaultMasterVolume = 1f;
        public const float DefaultMouseSensitivity = 0.08f;

        private const string MasterVolumePreference = "settings.masterVolume";
        private const string MouseSensitivityPreference = "settings.mouseSensitivity";

        public static float LoadMasterVolume()
        {
            return Mathf.Clamp01(PlayerPrefs.GetFloat(
                MasterVolumePreference,
                DefaultMasterVolume));
        }

        public static float LoadMouseSensitivity(float fallback = DefaultMouseSensitivity)
        {
            return Mathf.Clamp(
                PlayerPrefs.GetFloat(MouseSensitivityPreference, fallback),
                global::Player.ThirdPersonCameraController.MinimumMouseSensitivity,
                global::Player.ThirdPersonCameraController.MaximumMouseSensitivity);
        }

        public static void ApplyMasterVolume(float value)
        {
            AudioListener.volume = Mathf.Clamp01(value);
        }

        public static void Save(float masterVolume, float mouseSensitivity)
        {
            PlayerPrefs.SetFloat(MasterVolumePreference, Mathf.Clamp01(masterVolume));
            PlayerPrefs.SetFloat(
                MouseSensitivityPreference,
                Mathf.Clamp(
                    mouseSensitivity,
                    global::Player.ThirdPersonCameraController.MinimumMouseSensitivity,
                    global::Player.ThirdPersonCameraController.MaximumMouseSensitivity));
            PlayerPrefs.Save();
        }
    }
}
