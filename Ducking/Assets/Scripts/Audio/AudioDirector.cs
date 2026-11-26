using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DuckKing.Audio
{
    /// <summary>
    /// One place that owns every sound in the game.
    ///
    /// Two music sources crossfade into each other so a track change never clicks,
    /// and a small pool of SFX voices lets several sounds overlap. Every level is
    /// read from the same PlayerPrefs keys the settings panel writes, because the
    /// usual bug report is "the slider does nothing", not "this one duck is too loud".
    ///
    /// Survives scene loads, so music started on the title screen keeps playing
    /// through the story intro instead of restarting.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class AudioDirector : MonoBehaviour
    {
        public static AudioDirector Instance { get; private set; }

        private const string KeyMusic = "duck_music_vol";
        private const string KeySfx = "duck_sfx_vol";
        private const float DefaultMusic = 0.8f;
        private const float DefaultSfx = 1f;

        [Header("Music")]
        [Tooltip("Clip played on start, if any")]
        [SerializeField] private AudioClip startMusic;
        [Tooltip("Loops the music by default")]
        [SerializeField] private bool loopMusic = true;
        [Tooltip("Music fade-in when a track starts")]
        [SerializeField] private float musicFadeIn = 0.6f;
        [Tooltip("Seconds to crossfade when switching tracks")]
        [SerializeField] private float musicCrossfade = 0.8f;

        [Header("SFX")]
        [Tooltip("How many sounds may play at once before the oldest is cut")]
        [SerializeField] private int sfxVoices = 8;

        private AudioSource _musicA;
        private AudioSource _musicB;
        private AudioSource _sfx;
        private readonly List<AudioSource> _sfxPool = new List<AudioSource>();

        // Voices with a sound still in them, so a slider move can re-level them.
        private readonly List<AudioSource> _liveSfx = new List<AudioSource>();

        private AudioClip _currentMusic;
        private Coroutine _musicRoutine;

        // A jingle is music the game owns only for a moment: it plays once and then
        // hands the speakers back to whatever loops were running before it.
        private Coroutine _jingleRoutine;
        private AudioClip _loopedClip;
        private bool _loopedEnabled;
        private bool _jinglePlaying;

        private float _sfxVolume = DefaultSfx;
        private float _musicVolume = DefaultMusic;

        // Each track carries its own ceiling (the MusicSceneStarter volume scale), and
        // the slider is applied on top of it every frame. Keeping the two apart is what
        // lets the slider move a track that is already playing: the scale is fixed per
        // track, the slider is live.
        private float _musicTrackScale = 1f;
        private float _jingleScale = 1f;
        private float _fadeOutStartA;
        private float _fadeOutStartB;

        public float MusicVolume => _musicVolume;
        public float SfxVolume => _sfxVolume;

        /// <summary>Loop ceiling times the slider: what the looping track should be at.</summary>
        private float MusicTarget => _musicVolume * _musicTrackScale;

        /// <summary>Jingle ceiling times the slider: what a one-shot theme should be at.</summary>
        private float JingleTarget => _musicVolume * _jingleScale;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                // A second director from a freshly loaded scene would double every
                // sound, so the newcomer removes itself and lets the original rule.
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            BuildSources();
            ApplyVolumes();
        }

        private void Update()
        {
            // Pull the saved sliders every frame. A slider only reports the change it
            // makes itself, and there are two panels (title screen and level) plus a
            // reset button that write the same keys behind the director's back. Polling
            // is the one thing that cannot miss any of them, and it costs two lookups.
            float music = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyMusic, DefaultMusic));
            float sfx = Mathf.Clamp01(PlayerPrefs.GetFloat(KeySfx, DefaultSfx));

            if (!Mathf.Approximately(music, _musicVolume) || !Mathf.Approximately(sfx, _sfxVolume))
            {
                _musicVolume = music;
                _sfxVolume = sfx;
                ApplyVolumes();
                MoveSfxVolumes();
            }

            // Fades and phases finish at a level computed when they started. Re-applying
            // the slider afterwards is what keeps a running track following the control
            // instead of freezing at whatever the value was at that moment.
            if (_musicRoutine == null && !_jinglePlaying)
            {
                if (_musicA != null && _musicA.isPlaying) _musicA.volume = MusicTarget;
                if (_musicB != null && _musicB.isPlaying) _musicB.volume = MusicTarget;
            }
        }

        private void Start()
        {
            if (startMusic != null && _currentMusic == null)
            {
                PlayMusic(startMusic);
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void BuildSources()
        {
            if (_musicA == null)
            {
                _musicA = gameObject.AddComponent<AudioSource>();
                _musicA.loop = loopMusic;
                _musicA.playOnAwake = false;
                _musicA.spatialBlend = 0f;
            }

            if (_musicB == null)
            {
                _musicB = gameObject.AddComponent<AudioSource>();
                _musicB.loop = loopMusic;
                _musicB.playOnAwake = false;
                _musicB.spatialBlend = 0f;
                _musicB.volume = 0f;
            }

            if (_sfx == null)
            {
                _sfx = gameObject.AddComponent<AudioSource>();
                _sfx.loop = false;
                _sfx.playOnAwake = false;
                _sfx.spatialBlend = 0f;
            }

            // A small pool so several sounds can overlap without cutting each other.
            // Two quacks and a landing at once is normal during co-op play.
            while (_sfxPool.Count < Mathf.Max(1, sfxVoices))
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.loop = false;
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                _sfxPool.Add(source);
            }
        }

        // ---- volume -----------------------------------------------------------

        /// <summary>
        /// Pushes the saved slider values into every source. Called on load and by
        /// the settings panel whenever a slider moves.
        /// </summary>
        public void ApplyVolumes()
        {
            // The panes are the ones that own the numbers; re-reading them here is only
            // a safety net for a caller that forgot to save. The polling in Update is
            // what actually reacts to a slider, so this must not overwrite a value the
            // panel has just set in memory but not yet flushed.
            _musicVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(KeyMusic, DefaultMusic));
            _sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(KeySfx, DefaultSfx));
            MoveSfxVolumes();

            // Fades are driven per frame and read the slider themselves, so a running
            // fade is left alone here. Anything idle is set to its target immediately.
            if (_musicRoutine == null && !_jinglePlaying)
            {
                if (_musicA != null && _musicA.isPlaying) _musicA.volume = MusicTarget;
                if (_musicB != null && _musicB.isPlaying) _musicB.volume = MusicTarget;
            }
        }

        private AudioSource ActiveMusicSource => _musicA != null && _musicA.isPlaying ? _musicA : _musicB;
        private AudioSource IdleMusicSource => ActiveMusicSource == _musicA ? _musicB : _musicA;

        // ---- looping music ----------------------------------------------------

        /// <summary>
        /// Starts a loop track, crossfading from whatever is playing.
        /// The request is remembered, so a scene change can ask for the same track
        /// again without interrupting it, and a jingle can restore it afterwards.
        /// </summary>
        public void PlayLoop(AudioClip clip, float fade = -1f, float volumeScale = 1f)
        {
            if (clip == null) return;

            _loopedClip = clip;
            _loopedEnabled = true;

            // The jingle owns the speakers until it finishes.
            if (_jinglePlaying) return;

            if (_currentMusic == clip)
            {
                if (_musicA != null && !_musicA.isPlaying && _musicA.clip == clip) _musicA.UnPause();
                return;
            }

            _musicTrackScale = Mathf.Clamp01(volumeScale);
            CrossfadeTo(clip, fade >= 0f ? fade : musicCrossfade, true);
        }

        /// <summary>Fades the loop music out and stops asking for it.</summary>
        public void StopLoop(float fade = 0.6f)
        {
            _loopedEnabled = false;
            _loopedClip = null;

            if (_jinglePlaying) return;

            StopMusic(fade);
        }

        /// <summary>Plays a music track once, crossfading from whatever is playing.</summary>
        public void PlayMusic(AudioClip clip)
        {
            if (clip == null) return;
            if (_currentMusic == clip)
            {
                // Same track requested again: unpause rather than restart it.
                if (_musicA != null && !_musicA.isPlaying && _musicA.clip == clip) _musicA.UnPause();
                return;
            }

            _musicTrackScale = 1f;
            CrossfadeTo(clip, musicCrossfade, loopMusic);
        }

        private void CrossfadeTo(AudioClip clip, float duration, bool loop)
        {
            if (_musicRoutine != null)
            {
                StopCoroutine(_musicRoutine);
                _musicRoutine = null;
            }

            _currentMusic = clip;
            _musicRoutine = StartCoroutine(CrossfadeRoutine(clip, Mathf.Max(0.05f, duration), loop));
        }

        private IEnumerator CrossfadeRoutine(AudioClip clip, float duration, bool loop)
        {
            AudioSource from = ActiveMusicSource;
            if (from != null && (!from.isPlaying || _currentMusic == from.clip && from.clip == clip))
            {
                from = from.isPlaying ? null : from;
            }

            AudioSource to = from == _musicA ? _musicB : _musicA;
            if (to == null) to = _musicB != null ? _musicB : _musicA;
            if (to == null) { _musicRoutine = null; yield break; }

            to.clip = clip;
            to.loop = loop;
            to.volume = 0f;
            to.Play();

            float target = MusicTarget;
            float fromStart = from != null ? from.volume : 0f;
            float t = 0f;

            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / duration);

                // Read the slider each frame so the fade cannot fight the setting.
                if (to != null) to.volume = p * target;
                if (from != null) from.volume = (1f - p) * fromStart;

                yield return null;
            }

            if (to != null) to.volume = target;
            if (from != null)
            {
                from.Stop();
                from.volume = 0f;
            }

            _musicRoutine = null;
        }

        /// <summary>Fades the music out and stops it.</summary>
        public void StopMusic(float fadeSeconds = 0.6f)
        {
            if (_musicRoutine != null)
            {
                StopCoroutine(_musicRoutine);
                _musicRoutine = null;
            }

            _currentMusic = null;
            StartCoroutine(FadeOutRoutine(fadeSeconds));
        }

        private IEnumerator FadeOutRoutine(float duration)
        {
            _fadeOutStartA = _musicA != null ? _musicA.volume : 0f;
            _fadeOutStartB = _musicB != null ? _musicB.volume : 0f;
            float t = 0f;

            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = 1f - Mathf.Clamp01(t / duration);
                // Scaled against the slider rather than a frozen snapshot, so turning
                // the music down mid-fade actually lands quieter.
                if (_musicA != null) _musicA.volume = _fadeOutStartA * k * (_musicVolume > 0.001f ? 1f : 0f);
                if (_musicB != null) _musicB.volume = _fadeOutStartB * k * (_musicVolume > 0.001f ? 1f : 0f);
                yield return null;
            }

            if (_musicA != null) { _musicA.Stop(); _musicA.volume = 0f; }
            if (_musicB != null) { _musicB.Stop(); _musicB.volume = 0f; }
        }

        // ---- jingle -----------------------------------------------------------

        /// <summary>
        /// Plays a one-shot music clip (an ending theme, for example) and then fades
        /// the previous loop back in. Used instead of a plain music switch so the
        /// game does not end up silent when the jingle stops.
        /// </summary>
        public void PlayJingle(AudioClip clip, float fadeIn = 0.5f, float fadeOut = 1.2f, float volumeScale = 1f)
        {
            if (clip == null) return;

            if (_jingleRoutine != null) StopCoroutine(_jingleRoutine);
            _jingleRoutine = StartCoroutine(JingleRoutine(clip, fadeIn, fadeOut, volumeScale));
        }

        private IEnumerator JingleRoutine(AudioClip clip, float fadeIn, float fadeOut, float volumeScale)
        {
            _jinglePlaying = true;
            _jingleScale = Mathf.Clamp01(volumeScale);

            // Step 1: duck the loop out so the jingle has room.
            AudioSource looping = ActiveMusicSource;
            float loopStart = looping != null ? looping.volume : 0f;
            float t = 0f;
            float duck = Mathf.Max(0.05f, fadeIn);
            while (t < duck)
            {
                t += Time.unscaledDeltaTime;
                if (looping != null) looping.volume = loopStart * (1f - Mathf.Clamp01(t / duck));
                yield return null;
            }
            if (looping != null) looping.volume = 0f;

            // Step 2: fade the jingle up on the free source.
            AudioSource from = ActiveMusicSource;
            AudioSource to = from == _musicA ? _musicB : _musicA;
            if (to == null) to = _musicB != null ? _musicB : _musicA;
            if (to == null) { _jinglePlaying = false; _jingleRoutine = null; yield break; }

            to.clip = clip;
            to.loop = false;
            to.volume = 0f;
            to.Play();

            t = 0f;
            float jingleIn = Mathf.Max(0.05f, fadeIn);
            while (t < jingleIn)
            {
                t += Time.unscaledDeltaTime;
                to.volume = Mathf.Clamp01(t / jingleIn) * JingleTarget;
                yield return null;
            }
            to.volume = JingleTarget;

            // Step 3: let it run to the end, following the slider, then fade it out.
            while (to != null && to.isPlaying)
            {
                to.volume = JingleTarget;
                yield return null;
            }

            t = 0f;
            float outTime = Mathf.Max(0.05f, fadeOut);
            while (t < outTime)
            {
                t += Time.unscaledDeltaTime;
                if (to != null) to.volume = JingleTarget * (1f - Mathf.Clamp01(t / outTime));
                yield return null;
            }
            if (to != null) { to.Stop(); to.volume = 0f; }

            _jinglePlaying = false;
            _currentMusic = null;

            // Step 4: hand the speakers back to whatever should be looping.
            if (_loopedEnabled && _loopedClip != null)
            {
                float restore = Mathf.Max(0.05f, fadeOut);
                CrossfadeTo(_loopedClip, restore, true);

                // Wait for that fade to finish before releasing the flag.
                while (_musicRoutine != null) yield return null;
            }

            _jingleRoutine = null;
        }

        // ---- sound effects ----------------------------------------------------

        /// <summary>Plays a one-shot sound effect at the saved SFX volume.</summary>
        public void PlaySfx(AudioClip clip, float volumeScale = 1f, float pitchJitter = 0f)
        {
            if (clip == null || _sfxVolume <= 0.001f) return;

            AudioSource source = TakeFreeVoice();
            if (source == null) return;

            // PlayOneShot's own volume cannot be changed afterwards, so the level is
            // put on the source and the scale is folded in here. MoveSfxVolumes keeps
            // the source in sync with the slider for as long as the sound lasts.
            source.volume = 1f;

            // No upper clamp: the click clip peaks at 0.36 of full scale and is gone
            // in 15 ms, so a scale above 1 is what makes it audible at all. Unity
            // clamps the final mix itself, and short SFX rarely meet that ceiling.
            source.pitch = pitchJitter > 0f ? 1f + Random.Range(-pitchJitter, pitchJitter) : 1f;
            source.PlayOneShot(clip, Mathf.Max(0f, volumeScale));

            _liveSfx.Add(source);
        }

        /// <summary>
        /// Re-levels every voice that is still sounding. Called whenever the SFX
        /// slider moves, so a jump or a click that is already in the air follows the
        /// control instead of finishing at the old level.
        /// </summary>
        private void MoveSfxVolumes()
        {
            for (int i = _liveSfx.Count - 1; i >= 0; i--)
            {
                AudioSource source = _liveSfx[i];
                if (source == null)
                {
                    _liveSfx.RemoveAt(i);
                    continue;
                }

                // A voice is only busy while it still has something to play; anything
                // else is returned to the pool and must not keep a stale level.
                if (source.isPlaying)
                {
                    source.volume = _sfxVolume;
                }
                else
                {
                    source.volume = 1f;
                    _liveSfx.RemoveAt(i);
                }
            }
        }

        /// <summary>
        /// Returns a free voice from the pool, or recycles the one that has been
        /// silent the longest. A fixed pool avoids allocating a source per sound.
        /// </summary>
        private AudioSource TakeFreeVoice()
        {
            for (int i = 0; i < _sfxPool.Count; i++)
            {
                if (!_sfxPool[i].isPlaying) return _sfxPool[i];
            }

            return _sfxPool.Count > 0 ? _sfxPool[0] : _sfx;
        }

        // ---- Static helpers, so a caller never has to null-check the instance ----

        public static void PlaySfxClip(AudioClip clip, float volumeScale = 1f, float pitchJitter = 0f)
        {
            if (Instance != null)
            {
                Instance.PlaySfx(clip, volumeScale, pitchJitter);
                return;
            }

            // Before the director exists (rare, e.g. an editor preview) the sound is
            // played positionally so it is still audible rather than silently lost.
            if (clip == null) return;
            AudioSource.PlayClipAtPoint(clip, Vector3.zero, volumeScale);
        }

        /// <summary>Plays (or resumes) a looping track.</summary>
        public static void PlayLoopClip(AudioClip clip, float fade = -1f, float volumeScale = 1f)
        {
            if (Instance != null) Instance.PlayLoop(clip, fade, volumeScale);
        }

        /// <summary>Stops the looping track with a fade.</summary>
        public static void StopLoopPlayback(float fadeSeconds = 0.6f)
        {
            if (Instance != null) Instance.StopLoop(fadeSeconds);
        }

        public static void PlayMusicClip(AudioClip clip)
        {
            if (Instance != null) Instance.PlayMusic(clip);
        }

        public static void PlayJingleClip(AudioClip clip, float fadeIn = 0.5f, float fadeOut = 1.2f, float volumeScale = 1f)
        {
            if (Instance != null) Instance.PlayJingle(clip, fadeIn, fadeOut, volumeScale);
        }

        public static void StopMusicPlayback(float fadeSeconds = 0.6f)
        {
            if (Instance != null) Instance.StopMusic(fadeSeconds);
        }
    }
}