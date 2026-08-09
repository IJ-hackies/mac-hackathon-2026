using System.Collections;
using System.Collections.Generic;
using Player.UI;
using UnityEngine;

namespace Audio
{
    /// Simple runtime-pooled one-shot/loop SFX player. Deliberately does NOT use a Unity
    /// AudioMixer asset (too awkward to author outside the Editor GUI for this task) - mixing is
    /// just a handful of linear category multipliers applied on top of each SfxDefinition's own
    /// volume, plus a master multiplier read from the same PlayerPrefs key GameSettings already
    /// uses (see Player.UI.GameSettings.LoadMasterVolume/ApplyMasterVolume).
    public class AudioManager : MonoBehaviour
    {
        private const int PoolSize = 16;
        private const string ResourcesLibraryPath = "SfxLibrary";

        private static AudioManager _instance;

        /// Lazily spins up a pooled AudioManager (with a Resources-loaded SfxLibrary, see
        /// EnsureInstance) the first time anything asks for it, so gameplay code calling
        /// AudioManager.Instance.PlaySfx(...) never null-refs even in a scene that wasn't
        /// pre-wired with an Audio GameObject.
        public static AudioManager Instance
        {
            get
            {
                if (_instance == null) EnsureInstance();
                return _instance;
            }
        }

        [SerializeField] private SfxLibrary library;

        [Header("Category Volume Multipliers")]
        [SerializeField, Range(0f, 1f)] private float weaponsVolume = 0.6f;
        [SerializeField, Range(0f, 1f)] private float impactsVolume = 0.7f;
        [SerializeField, Range(0f, 1f)] private float movementVolume = 0.55f;
        [SerializeField, Range(0f, 1f)] private float voiceVolume = 0.75f;
        [SerializeField, Range(0f, 1f)] private float uiVolume = 0.5f;

        private readonly List<AudioSource> _pool = new List<AudioSource>();
        private readonly Dictionary<SfxId, int> _activeCounts = new Dictionary<SfxId, int>();
        private readonly Dictionary<AudioCategory, int> _categoryActiveCounts = new Dictionary<AudioCategory, int>();
        private readonly Dictionary<GameObject, List<AudioHandle>> _activeLoopsByOwner = new Dictionary<GameObject, List<AudioHandle>>();
        private float _masterVolume = 1f;

        /// Lazy-create fallback (per spec) so gameplay code calling AudioManager.Instance never
        /// null-refs even if the current scene wasn't pre-wired with an Audio GameObject (see
        /// Tools/Audio/Build Sfx Library + MainMenuSceneSetup wiring notes in the final report).
        public static AudioManager EnsureInstance()
        {
            if (_instance != null) return _instance;

            var go = new GameObject("AudioManager (Auto)");
            go.AddComponent<AudioManager>(); // Awake() sets _instance
            return _instance;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);

            if (library == null) library = Resources.Load<SfxLibrary>(ResourcesLibraryPath);

            // Master volume is applied entirely through this multiplier (and MusicManager's own),
            // never through AudioListener.volume - pin it to 1 here so a stale/low value left over
            // from anywhere else can't silently attenuate every AudioSource in the scene.
            AudioListener.volume = 1f;
            _masterVolume = GameSettings.LoadMasterVolume();
            BuildPool();
        }

        private void BuildPool()
        {
            for (int i = 0; i < PoolSize; i++)
            {
                var sourceGo = new GameObject($"SfxSource_{i}");
                sourceGo.transform.SetParent(transform, false);
                var source = sourceGo.AddComponent<AudioSource>();
                source.playOnAwake = false;
                ConfigureRolloff(source);
                _pool.Add(source);
            }
        }

        // Unity's AudioSource defaults to minDistance=1/maxDistance=500 with logarithmic rolloff,
        // which attenuates a 3D one-shot to near-silence by the time it's even a few meters from
        // the listener - at real gameplay distances (player vs. enemy/boss on the planet surface)
        // that made most combat SFX effectively inaudible. Full volume out to minDistance, linear
        // falloff to silence by maxDistance, so nearby combat reads clearly and distant sources
        // still fade out rather than carrying across the whole arena.
        private static void ConfigureRolloff(AudioSource source)
        {
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 8f;
            source.maxDistance = 180f;
        }

        // Re-applied per play (not just once at pool build) so each SfxDefinition's own
        // minDistance/maxDistance can override the general default - pooled sources are shared
        // across every SfxId, so whatever the previous clip needed would otherwise leak forward.
        private static void ConfigureRolloff(AudioSource source, SfxDefinition definition)
        {
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = definition.minDistance;
            source.maxDistance = definition.maxDistance;
        }

        public void SetMasterVolume(float value)
        {
            _masterVolume = Mathf.Clamp01(value);
        }

        // Voice/death category cues are always allowed through even at the per-id concurrency cap
        // (a death sound getting silently dropped reads far worse than a stacked footstep does).
        public void PlaySfx(SfxId id, Vector3? worldPos = null, float volumeScale = 1f)
        {
            var definition = library != null ? library.Get(id) : null;
            if (definition == null || definition.clip == null) return;

            int active = _activeCounts.TryGetValue(id, out int count) ? count : 0;
            if (active >= Mathf.Max(1, definition.maxConcurrent) && definition.category != AudioCategory.Voice)
            {
                return;
            }

            AudioSource source = GetFreeSource();
            if (source == null) return;

            if (definition.category == AudioCategory.Weapons || definition.category == AudioCategory.Impacts
                || definition.category == AudioCategory.Voice)
            {
                MusicManager.Instance.Duck();
            }

            ClearDynamicFilters(source);
            ConfigureRolloff(source, definition);

            source.clip = definition.clip;
            source.loop = false;
            source.spatialBlend = worldPos.HasValue ? 1f : 0f;
            if (worldPos.HasValue) source.transform.position = worldPos.Value;
            source.pitch = Random.Range(definition.pitchMin, definition.pitchMax);

            // Stacking attenuation: a wave of flying enemies all firing at once (or any pile-up of
            // same-category one-shots) previously just summed to full volume per instance and
            // became a wall of noise. Each additional sound already active in this category quietens
            // the new one a bit further, so N simultaneous shots read as "a lot of gunfire" rather
            // than N times louder than one.
            int categoryActive = _categoryActiveCounts.TryGetValue(definition.category, out int catCount) ? catCount : 0;
            float stackAttenuation = 1f / (1f + categoryActive * 0.35f);

            source.volume = definition.volume * CategoryMultiplier(definition.category) * _masterVolume
                * volumeScale * stackAttenuation;

            if (id == SfxId.Boss2JumpSlam)
            {
                ApplyJumpSlamPreset(source);
            }

            source.Play();
            StartCoroutine(TrackActive(id, definition.category, source));
        }

        private IEnumerator TrackActive(SfxId id, AudioCategory category, AudioSource source)
        {
            _activeCounts[id] = (_activeCounts.TryGetValue(id, out int count) ? count : 0) + 1;
            _categoryActiveCounts[category] = (_categoryActiveCounts.TryGetValue(category, out int catCount) ? catCount : 0) + 1;

            float clipLength = source.clip != null ? source.clip.length / Mathf.Max(0.01f, source.pitch) : 0f;
            yield return new WaitForSeconds(clipLength);

            _activeCounts[id] = Mathf.Max(0, (_activeCounts.TryGetValue(id, out int remaining) ? remaining : 1) - 1);
            _categoryActiveCounts[category] = Mathf.Max(0, (_categoryActiveCounts.TryGetValue(category, out int catRemaining) ? catRemaining : 1) - 1);
        }

        // "Loud, add reverb, deeper" per spec - pitched down and given a large-hall style
        // AudioReverbFilter for the duration of this one shot, removed afterward via
        // ClearDynamicFilters so it doesn't leak onto the next unrelated clip this pooled source
        // happens to play.
        private void ApplyJumpSlamPreset(AudioSource source)
        {
            source.pitch = 0.8f;

            var reverb = source.gameObject.GetComponent<AudioReverbFilter>();
            if (reverb == null) reverb = source.gameObject.AddComponent<AudioReverbFilter>();
            reverb.reverbPreset = AudioReverbPreset.User;
            reverb.reverbLevel = 0f;
            reverb.room = -1000;
            reverb.decayTime = 2.5f;
            reverb.enabled = true;

            StartCoroutine(RemoveReverbAfter(source, Mathf.Max(0.5f, source.clip.length / Mathf.Max(0.01f, source.pitch)) + 0.1f));
        }

        private IEnumerator RemoveReverbAfter(AudioSource source, float delay)
        {
            yield return new WaitForSeconds(delay);
            ClearDynamicFilters(source);
        }

        private static void ClearDynamicFilters(AudioSource source)
        {
            var reverb = source.gameObject.GetComponent<AudioReverbFilter>();
            if (reverb != null) reverb.enabled = false;
        }

        public AudioHandle PlayLoop(SfxId id, Transform followTarget = null)
        {
            var definition = library != null ? library.Get(id) : null;
            if (definition == null || definition.clip == null) return AudioHandle.Invalid;

            AudioSource source = GetFreeSource();
            if (source == null) return AudioHandle.Invalid;

            ClearDynamicFilters(source);
            ConfigureRolloff(source, definition);
            source.clip = definition.clip;
            source.pitch = definition.pitchMin;
            source.spatialBlend = followTarget != null ? 1f : 0f;
            if (followTarget != null) source.transform.position = followTarget.position;
            source.volume = definition.volume * CategoryMultiplier(definition.category) * _masterVolume;

            var handle = new AudioHandle(source);

            // followTarget doubles as the "owner" for death cleanup (see StopAllLoopsFor) - every
            // current PlayLoop caller passes its own transform as followTarget, so this needs no
            // separate owner parameter. A loop with no followTarget (e.g. Boss1Cutscene) isn't
            // owned by anything and just runs until its own explicit StopLoop call.
            if (followTarget != null)
            {
                GameObject owner = followTarget.gameObject;
                if (!_activeLoopsByOwner.TryGetValue(owner, out var handles))
                {
                    handles = new List<AudioHandle>();
                    _activeLoopsByOwner[owner] = handles;
                }
                handles.Add(handle);
            }

            // Boss2ShootPrimaryLoop/MechShootPrimaryLoop only ever want the FIRST HALF of the clip
            // looped (the sustained "burst" portion, before its tail decays) - a manual restart
            // coroutine rather than source.loop=true on the whole clip.
            bool halfLoop = id == SfxId.Boss2ShootPrimaryLoop || id == SfxId.MechShootPrimaryLoop;
            if (halfLoop)
            {
                source.loop = false;
                source.Play();
                StartCoroutine(HalfClipLoopRoutine(handle, source));
            }
            else
            {
                source.loop = true;
                if (followTarget != null) StartCoroutine(FollowRoutine(handle, source, followTarget));
                source.Play();
            }

            return handle;
        }

        private IEnumerator HalfClipLoopRoutine(AudioHandle handle, AudioSource source)
        {
            float halfLength = source.clip.length * 0.5f;
            while (handle.IsValid && source != null)
            {
                if (source.time >= halfLength)
                {
                    source.time = 0f;
                    source.Play();
                }
                yield return null;
            }
        }

        private IEnumerator FollowRoutine(AudioHandle handle, AudioSource source, Transform target)
        {
            while (handle.IsValid && source != null && target != null)
            {
                source.transform.position = target.position;
                yield return null;
            }
        }

        public void StopLoop(AudioHandle handle)
        {
            if (!handle.IsValid) return;
            handle.Invalidate();
            var source = handle.Source;
            if (source == null) return;

            source.loop = false;
            source.Stop();
            source.clip = null;
        }

        // Called from Health.ApplyDamage on death - stops every loop this GameObject started via
        // PlayLoop(id, transform), regardless of whether the coroutine that owns it is still
        // running. Fixes loops (e.g. BossMechAI's sustained machine-gun burst) surviving death
        // because base.HandleDeath()'s StopAllCoroutines() abandons the owning coroutine before it
        // reaches its own StopLoop call.
        public void StopAllLoopsFor(GameObject owner)
        {
            if (owner == null) return;
            if (!_activeLoopsByOwner.TryGetValue(owner, out var handles)) return;

            foreach (var handle in handles)
            {
                StopLoop(handle);
            }
            _activeLoopsByOwner.Remove(owner);
        }

        private AudioSource GetFreeSource()
        {
            foreach (var source in _pool)
            {
                if (!source.isPlaying) return source;
            }

            // All busy - steal the least-recently-started one (index 0 rotated to the back) rather
            // than silently dropping the new request.
            AudioSource stolen = _pool[0];
            _pool.RemoveAt(0);
            _pool.Add(stolen);
            stolen.Stop();
            return stolen;
        }

        private float CategoryMultiplier(AudioCategory category)
        {
            return category switch
            {
                AudioCategory.Weapons => weaponsVolume,
                AudioCategory.Impacts => impactsVolume,
                AudioCategory.Movement => movementVolume,
                AudioCategory.Voice => voiceVolume,
                AudioCategory.UI => uiVolume,
                _ => 1f
            };
        }
    }

    /// Lightweight handle for a looping SFX started via AudioManager.PlayLoop - just wraps the
    /// pooled AudioSource plus a validity flag so StopLoop/the internal loop coroutines can tell a
    /// stopped/reused handle apart from a live one without anything more elaborate.
    public readonly struct AudioHandle
    {
        public static readonly AudioHandle Invalid = default;

        public AudioSource Source { get; }
        private readonly ValidityBox _validity;

        public AudioHandle(AudioSource source)
        {
            Source = source;
            _validity = new ValidityBox { Valid = true };
        }

        public bool IsValid => Source != null && _validity != null && _validity.Valid;

        public void Invalidate()
        {
            if (_validity != null) _validity.Valid = false;
        }

        private class ValidityBox
        {
            public bool Valid;
        }
    }
}
