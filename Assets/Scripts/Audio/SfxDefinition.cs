using System;
using UnityEngine;

namespace Audio
{
    /// One entry in a SfxLibrary - the clip plus per-id tuning (base volume before category/master
    /// multipliers, a pitch randomization range, and how many concurrent instances of this exact
    /// id are allowed to play at once before AudioManager.PlaySfx starts skipping requests).
    [Serializable]
    public class SfxDefinition
    {
        public SfxId id;
        public AudioClip clip;
        public AudioCategory category;
        [Range(0f, 1f)] public float volume = 1f;
        [Range(0.5f, 1.5f)] public float pitchMin = 1f;
        [Range(0.5f, 1.5f)] public float pitchMax = 1f;
        public int maxConcurrent = 8;
        // Per-id 3D falloff override (see AudioManager.ConfigureRolloff) - full volume to 8
        // units, linear falloff to silence by 180. Raised 4x from the original 45 across the
        // board (not just boss/mech cues) - most combat SFX were inaudible at normal ranged-combat
        // distances.
        public float minDistance = 8f;
        public float maxDistance = 180f;
    }
}
