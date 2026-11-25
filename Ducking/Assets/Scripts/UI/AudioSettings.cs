using UnityEngine;
using UnityEngine.UI;

namespace DuckKing.UI
{
    /// <summary>
    /// Music and SFX volume controls shared by the title screen and the in-game
    /// settings panel.
    ///
    /// The values live in PlayerPrefs under the same keys the title screen has
    /// always used, so a change made mid-run is still there on the next launch.
    /// Every reference is optional: a panel that only has one slider still works.
    /// </summary>
    public class AudioSettings : MonoBehaviour
    {
        [Header("Sliders")]
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;

        [Header("Value Labels")]
        [SerializeField] private Text musicValueText;
        [SerializeField] private Text sfxValueText;

        [Header("Audio Sources")]
        [Tooltip("Optional music source; its volume follows the music slider")]
        [SerializeField] private AudioSource musicSource;
        [Tooltip("Optional SFX source; its volume follows the SFX slider")]
        [SerializeField] private AudioSource sfxSource;
        [Tooltip("Fallback music volume when nothing has been saved yet")]
        [SerializeField] private float defaultMusicVolume = 0.8f;
        [Tooltip("Fallback SFX volume when nothing has been saved yet")]
        [SerializeField] private float defaultSfxVolume = 1f;

        // Shared with DuckKing.Menu.SettingsPanel so both screens read one value.
        private const string KeyMusic = "duck_music_vol";
        private const string KeySfx = "duck_sfx_vol";

        private bool _initialized;

        private void Awake()
        {
            Initialize();
        }

        private void Start()
        {
            // The panel object starts disabled in most scenes, so Awake may not have
            // run yet. Initialise again here, which is safe because it is idempotent.
            Initialize();
        }

        /// <summary>Wires the sliders and pushes the saved values into them.</summary>
        public void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            float music = PlayerPrefs.GetFloat(KeyMusic, defaultMusicVolume);
            float sfx = PlayerPrefs.GetFloat(KeySfx, defaultSfxVolume);

            if (musicSlider != null)
            {
                musicSlider.SetValueWithoutNotify(music);
                musicSlider.onValueChanged.AddListener(OnMusicChanged);
            }

            if (sfxSlider != null)
            {
                sfxSlider.SetValueWithoutNotify(sfx);
                sfxSlider.onValueChanged.AddListener(OnSfxChanged);
            }

            UpdateLabels(music, sfx);
            ApplyToSources(music, sfx);
        }

        private void OnMusicChanged(float value)
        {
            UpdateLabels(value, SfxValue);
            ApplyToSources(value, SfxValue);

            PlayerPrefs.SetFloat(KeyMusic, value);
            PlayerPrefs.Save();
        }

        private void OnSfxChanged(float value)
        {
            UpdateLabels(MusicValue, value);
            ApplyToSources(MusicValue, value);

            PlayerPrefs.SetFloat(KeySfx, value);
            PlayerPrefs.Save();
        }

        private void ApplyToSources(float music, float sfx)
        {
            // Source volumes are scaled against the saved sliders rather than set to
            // zero-and-forget, so an unmuted slider restores the original level.
            if (musicSource != null) musicSource.volume = music;
            if (sfxSource != null) sfxSource.volume = sfx;
        }

        private void UpdateLabels(float music, float sfx)
        {
            if (musicValueText != null) musicValueText.text = Mathf.RoundToInt(music * 100f) + "%";
            if (sfxValueText != null) sfxValueText.text = Mathf.RoundToInt(sfx * 100f) + "%";
        }

        public float MusicValue => musicSlider != null ? musicSlider.value : PlayerPrefs.GetFloat(KeyMusic, defaultMusicVolume);
        public float SfxValue => sfxSlider != null ? sfxSlider.value : PlayerPrefs.GetFloat(KeySfx, defaultSfxVolume);

        /// <summary>Saved music volume, for anything that plays music without a panel.</summary>
        public static float SavedMusicVolume => PlayerPrefs.GetFloat(KeyMusic, 0.8f);

        /// <summary>Saved SFX volume, for anything that plays a sound without a panel.</summary>
        public static float SavedSfxVolume => PlayerPrefs.GetFloat(KeySfx, 1f);
    }
}