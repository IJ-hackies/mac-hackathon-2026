using Audio;
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

        // Single funnel for master volume, called by both MainMenuController and
        // SettingsMenuController's sliders. Previously this set AudioListener.volume directly,
        // which double-attenuated everything on top of AudioManager/MusicManager's own
        // category-multiplier system once those existed (both paths scaling the same slider value
        // multiplicatively), and SettingsMenuController's in-gameplay slider never touched
        // AudioManager/MusicManager at all, so it silently had zero effect on any SFX. Routing
        // exclusively through AudioManager/MusicManager (and pinning AudioListener.volume to 1, see
        // AudioManager.Awake) makes this the one place volume is actually applied.
        public static void ApplyMasterVolume(float value)
        {
            float clamped = Mathf.Clamp01(value);
            AudioListener.volume = 1f;
            AudioManager.Instance.SetMasterVolume(clamped);
            MusicManager.Instance.SetMasterVolume(clamped);
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
