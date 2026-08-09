using System.Collections;
using Player.UI;
using UnityEngine;

namespace Audio
{
    /// Separate singleton (from AudioManager) driving looping background music with a two-source
    /// crossfade. Clip fields are public so an editor script (or SfxLibrary's Music-category
    /// entries) can wire them once; PlayMusic itself just needs an AudioClip, it doesn't know
    /// about SfxLibrary/SfxId at all.
    public class MusicManager : MonoBehaviour
    {
        private static MusicManager _instance;

        public static MusicManager Instance
        {
            get
            {
                if (_instance == null) EnsureInstance();
                return _instance;
            }
        }

        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.1105f;
        [SerializeField, Range(0f, 1f)] private float duckAmount = 0.5f;
        [SerializeField] private float duckRelease = 0.35f;

        private Coroutine _duckRoutine;

        [Header("Tracks")]
        public AudioClip menuMusic;
        public AudioClip baseMusic;
        public AudioClip waveMusic;
        public AudioClip bossMusic;

        private AudioSource _musicA;
        private AudioSource _musicB;
        private AudioSource _activeSource;
        private AudioClip _activeOrQueuedClip;
        private Coroutine _fadeRoutine;
        private float _masterVolume = 1f;

        public static MusicManager EnsureInstance()
        {
            if (_instance != null) return _instance;

            var go = new GameObject("MusicManager (Auto)");
            var manager = go.AddComponent<MusicManager>();
            manager.LoadClipsFromResources();
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

            _masterVolume = GameSettings.LoadMasterVolume();

            _musicA = gameObject.AddComponent<AudioSource>();
            _musicB = gameObject.AddComponent<AudioSource>();
            foreach (var source in new[] { _musicA, _musicB })
            {
                source.playOnAwake = false;
                source.loop = true;
                source.spatialBlend = 0f;
                source.volume = 0f;
            }

            _activeSource = _musicA;
        }

        // Populates menu/base/wave/boss clips from Assets/Resources/Music/*.wav when this manager
        // is auto-created rather than pre-wired in-scene (Resources.Load only finds assets that
        // physically live under a Resources folder, hence the separate copy from Assets/Audio/Music).
        private void LoadClipsFromResources()
        {
            menuMusic = Resources.Load<AudioClip>("Music/menu");
            baseMusic = Resources.Load<AudioClip>("Music/loading");
            waveMusic = Resources.Load<AudioClip>("Music/slow-travel");
            bossMusic = Resources.Load<AudioClip>("Music/battle");
        }

        // Called by AudioManager whenever a punchy combat SFX (weapon fire, melee, hit, death)
        // plays, so that transient dips under the music bed instead of the music just sitting at
        // a permanently low floor. Short dip-and-recover rather than a hard mixer duck.
        public void Duck()
        {
            if (!isActiveAndEnabled) return;
            if (_duckRoutine != null) StopCoroutine(_duckRoutine);
            _duckRoutine = StartCoroutine(DuckRoutine());
        }

        private IEnumerator DuckRoutine()
        {
            float target = musicVolume * _masterVolume;
            float duckedVolume = target * (1f - duckAmount);
            if (_activeSource != null) _activeSource.volume = duckedVolume;

            float elapsed = 0f;
            while (elapsed < duckRelease)
            {
                elapsed += Time.unscaledDeltaTime;
                if (_activeSource != null)
                {
                    _activeSource.volume = Mathf.Lerp(duckedVolume, target, elapsed / duckRelease);
                }
                yield return null;
            }

            if (_activeSource != null) _activeSource.volume = target;
        }

        public void SetMasterVolume(float value)
        {
            _masterVolume = Mathf.Clamp01(value);
            if (_activeSource != null && _activeSource.isPlaying)
            {
                _activeSource.volume = musicVolume * _masterVolume;
            }
        }

        public void PlayMusic(AudioClip clip, float fadeSeconds = 1f)
        {
            if (clip == null) return;
            if (_activeOrQueuedClip == clip) return; // already playing/queued - no-op per spec

            _activeOrQueuedClip = clip;

            AudioSource incoming = _activeSource == _musicA ? _musicB : _musicA;
            AudioSource outgoing = _activeSource;

            incoming.clip = clip;
            incoming.volume = 0f;
            incoming.Play();

            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(CrossfadeRoutine(outgoing, incoming, fadeSeconds));

            _activeSource = incoming;
        }

        private IEnumerator CrossfadeRoutine(AudioSource outgoing, AudioSource incoming, float fadeSeconds)
        {
            float targetVolume = musicVolume * _masterVolume;
            float elapsed = 0f;
            float startOutVolume = outgoing != null ? outgoing.volume : 0f;

            if (fadeSeconds <= 0f)
            {
                if (outgoing != null) outgoing.Stop();
                incoming.volume = targetVolume;
                yield break;
            }

            while (elapsed < fadeSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / fadeSeconds);
                if (outgoing != null) outgoing.volume = Mathf.Lerp(startOutVolume, 0f, t);
                incoming.volume = Mathf.Lerp(0f, targetVolume, t);
                yield return null;
            }

            if (outgoing != null)
            {
                outgoing.volume = 0f;
                outgoing.Stop();
            }
            incoming.volume = targetVolume;
        }
    }
}
