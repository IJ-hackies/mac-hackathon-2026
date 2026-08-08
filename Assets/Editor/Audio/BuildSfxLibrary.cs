using System.Collections.Generic;
using Audio;
using UnityEditor;
using UnityEngine;

namespace AudioEditor
{
    /// One-off editor command that (re)builds the SfxLibrary ScriptableObject asset from the
    /// clips copied into Assets/Audio/SFX (and the music clips into Assets/Audio/Music), using
    /// the id -> clip -> category -> volume table from the audio system spec. Writes the asset to
    /// Assets/Resources/SfxLibrary.asset so AudioManager/MusicManager's Resources.Load fallback
    /// finds it even in a scene that wasn't pre-wired with an Audio GameObject.
    ///
    /// Must be run manually from inside the Unity Editor (Tools > Audio > Build Sfx Library) -
    /// this cannot be executed from the command line.
    public static class BuildSfxLibrary
    {
        private const string AssetPath = "Assets/Resources/SfxLibrary.asset";
        private const string SfxRoot = "Assets/Audio/SFX";
        private const string FreePackRoot = "Assets/Free Pack";

        private struct Row
        {
            public SfxId id;
            public string clipPath;
            public AudioCategory category;
            public float volume;
            public float pitchMin;
            public float pitchMax;
            public int maxConcurrent;

            public Row(SfxId id, string clipPath, AudioCategory category, float volume, float pitchMin, float pitchMax, int maxConcurrent)
            {
                this.id = id;
                this.clipPath = clipPath;
                this.category = category;
                this.volume = volume;
                this.pitchMin = pitchMin;
                this.pitchMax = pitchMax;
                this.maxConcurrent = maxConcurrent;
            }
        }

        [MenuItem("Tools/Audio/Build Sfx Library")]
        public static void Build()
        {
            var rows = new List<Row>
            {
                new Row(SfxId.PlayerShootPrimary, $"{SfxRoot}/Weapons/laserSmall_000.ogg", AudioCategory.Weapons, 0.6f, 0.95f, 1.05f, 6),
                new Row(SfxId.PlayerShootSecondary, $"{SfxRoot}/Weapons/laserLarge_002.ogg", AudioCategory.Weapons, 0.7f, 0.97f, 1.03f, 4),
                new Row(SfxId.PlayerMelee, $"{SfxRoot}/Weapons/kick.wav", AudioCategory.Weapons, 0.85f, 0.95f, 1.05f, 3),
                new Row(SfxId.PlayerHitReact, $"{SfxRoot}/Impacts/footstep_carpet_001.ogg", AudioCategory.Impacts, 0.55f, 0.9f, 1.1f, 3),
                new Row(SfxId.PlayerFootstep, $"{SfxRoot}/Movement/impactMetal_001.ogg", AudioCategory.Movement, 0.38f, 0.9f, 1.1f, 2),
                new Row(SfxId.PlayerDash, $"{SfxRoot}/Movement/footstep_grass_000.ogg", AudioCategory.Movement, 0.4f, 1f, 1f, 2),
                new Row(SfxId.PlayerJump, $"{SfxRoot}/Movement/whoosh_1.wav", AudioCategory.Movement, 0.55f, 0.95f, 1.05f, 1),
                new Row(SfxId.PlayerLand, $"{SfxRoot}/Impacts/impactSoft_medium_000.ogg", AudioCategory.Impacts, 0.55f, 0.9f, 1.05f, 1),
                new Row(SfxId.PlayerDeath, $"{SfxRoot}/Voice/scream.wav", AudioCategory.Voice, 0.8f, 1f, 1f, 1),
                // pitch fixed at 0.5 (half speed/half rate) per request - also lowers its pitch a
                // touch, which is an acceptable side effect of slowing playback without a
                // time-stretched re-render of the source clip.
                new Row(SfxId.MechShootPrimaryLoop, $"{SfxRoot}/Weapons/LASRGun_AlienHandgunBurst_03.wav", AudioCategory.Weapons, 0.28f, 0.5f, 0.5f, 1),
                new Row(SfxId.MechShootSecondary, $"{SfxRoot}/Weapons/explosionCrunch_001.ogg", AudioCategory.Weapons, 0.75f, 0.97f, 1.03f, 3),
                new Row(SfxId.MechHitReact, $"{SfxRoot}/Impacts/impactMetal_004.ogg", AudioCategory.Impacts, 0.42f, 0.95f, 1.05f, 3),
                new Row(SfxId.MechLegStepA, $"{SfxRoot}/Movement/hydraulic_down.wav", AudioCategory.Movement, 0.2f, 1f, 1f, 2),
                new Row(SfxId.MechLegStepB, $"{SfxRoot}/Movement/hydraulic_up.wav", AudioCategory.Movement, 0.2f, 1f, 1f, 2),
                new Row(SfxId.MechShieldActivate, $"{SfxRoot}/Ambient/forceField_001.ogg", AudioCategory.Weapons, 0.45f, 1f, 1f, 1),
                new Row(SfxId.EnemyFlyingShoot, $"{SfxRoot}/Weapons/GUNSupr_SilencedPistolFireShort_01.wav", AudioCategory.Weapons, 0.4f, 0.95f, 1.1f, 3),
                new Row(SfxId.EnemyHitReact, $"{SfxRoot}/Impacts/footstep_carpet_000.ogg", AudioCategory.Impacts, 0.42f, 0.9f, 1.15f, 4),
                new Row(SfxId.EnemyDeath, $"{SfxRoot}/Impacts/JDSherbert_Cursor_2.mp3", AudioCategory.Impacts, 0.6f, 0.95f, 1.05f, 3),
                // Swapped from BLLTImpt_HitMarker_07 (a UI-style confirmation "bleep", not a
                // weapon sound) - repeated every rangedTickInterval during a burst it read as a
                // continuous alarm rather than gunfire.
                new Row(SfxId.Boss1Shoot, $"{SfxRoot}/Weapons/GUNPis_PistolFireLongerTail_05.wav", AudioCategory.Weapons, 0.28f, 0.95f, 1.05f, 4),
                new Row(SfxId.Boss1HitReact, $"{SfxRoot}/Impacts/footstep_carpet_000.ogg", AudioCategory.Impacts, 0.35f, 0.95f, 1.05f, 3),
                new Row(SfxId.Boss1Cutscene, $"{SfxRoot}/Ambient/spaceEngine_000.ogg", AudioCategory.Voice, 0.5f, 1f, 1f, 1),
                new Row(SfxId.Boss2Roar, $"{SfxRoot}/Voice/CREAMnstr_BeastVocalisation_09.wav", AudioCategory.Voice, 0.9f, 0.85f, 0.95f, 1),
                new Row(SfxId.Boss2ShootPrimaryLoop, $"{SfxRoot}/Weapons/GUNAuto_PlasmidMachinegunBurst_07.wav", AudioCategory.Weapons, 0.32f, 1f, 1f, 1),
                new Row(SfxId.Boss2Ricochet, $"{SfxRoot}/Impacts/BLLTRico_RicochetMetallic_04.wav", AudioCategory.Impacts, 0.4f, 0.95f, 1.1f, 4),
                new Row(SfxId.Boss2TopDownV1, $"{SfxRoot}/Weapons/explosionCrunch_002.ogg", AudioCategory.Weapons, 0.65f, 0.97f, 1.03f, 3),
                new Row(SfxId.Boss2TopDownV2, $"{SfxRoot}/Weapons/explosionCrunch_000.ogg", AudioCategory.Weapons, 0.65f, 0.97f, 1.03f, 3),
                new Row(SfxId.Boss2JumpSlam, $"{SfxRoot}/Impacts/footstep_snow_003.ogg", AudioCategory.Impacts, 0.85f, 1f, 1f, 1),
                new Row(SfxId.BossLegStepA, $"{SfxRoot}/Movement/hydraulic_down.wav", AudioCategory.Movement, 0.45f, 1f, 1f, 2),
                new Row(SfxId.BossLegStepB, $"{SfxRoot}/Movement/hydraulic_up.wav", AudioCategory.Movement, 0.45f, 1f, 1f, 2),
                new Row(SfxId.Boss2DeathImpact, $"{FreePackRoot}/Bullet Impacts - Multiple 1.wav", AudioCategory.Voice, 0.6f, 1f, 1f, 1),
                new Row(SfxId.Boss2DeathExplosion, $"{FreePackRoot}/Explosion 1.wav", AudioCategory.Voice, 0.85f, 1f, 1f, 1),
                new Row(SfxId.ItemHealthPickup, $"{SfxRoot}/UI/freesound_community-heal-up-39285.mp3", AudioCategory.UI, 0.5f, 1f, 1f, 3),
                new Row(SfxId.ItemAmmoPickup, $"{SfxRoot}/UI/impactPlank_medium_003.ogg", AudioCategory.UI, 0.4f, 0.95f, 1.05f, 3),
                new Row(SfxId.ItemMechUpgradePickup, $"{SfxRoot}/UI/impactMining_001.ogg", AudioCategory.UI, 0.55f, 1f, 1f, 2),
                new Row(SfxId.ItemSpawn, $"{SfxRoot}/UI/impactGlass_heavy_000.ogg", AudioCategory.UI, 0.4f, 1f, 1f, 2),
            };

            var library = AssetDatabase.LoadAssetAtPath<SfxLibrary>(AssetPath);
            if (library == null)
            {
                library = ScriptableObject.CreateInstance<SfxLibrary>();
                AssetDatabase.CreateAsset(library, AssetPath);
            }

            library.entries = new List<SfxDefinition>();
            int missing = 0;
            foreach (var row in rows)
            {
                var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(row.clipPath);
                if (clip == null)
                {
                    Debug.LogWarning($"[BuildSfxLibrary] Missing clip for {row.id} at {row.clipPath}");
                    missing++;
                }

                library.entries.Add(new SfxDefinition
                {
                    id = row.id,
                    clip = clip,
                    category = row.category,
                    volume = row.volume,
                    pitchMin = row.pitchMin,
                    pitchMax = row.pitchMax,
                    maxConcurrent = row.maxConcurrent,
                });
            }

            EditorUtility.SetDirty(library);
            AssetDatabase.SaveAssets();

            Debug.Log($"[BuildSfxLibrary] Wrote {library.entries.Count} entries to {AssetPath} ({missing} missing clips). " +
                      "Music tracks are loaded separately by MusicManager from Assets/Resources/Music/*.wav.");
        }
    }
}
