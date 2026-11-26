using UnityEngine;
using UnityEngine.UI;

namespace DuckKing.Menu
{
    /// <summary>
    /// Settings panel shown over the title screen.
    ///
    /// Only exposes what this game actually has: master, music and SFX volume,
    /// plus fullscreen. Values are stored in PlayerPrefs so they persist between
    /// runs, and applied immediately on change.
    ///
    /// The panel is a plain GameObject that gets toggled; all controls are optional
    /// so art can wire whichever Sliders and Toggles exist without breaking.
    /// </summary>
    public class SettingsPanel : MonoBehaviour
    {
        [Header("Root")]
        [Tooltip("Panel object toggled on and off")]
        [SerializeField] private GameObject panelRoot;

        [Header("Audio Sliders")]
        [SerializeField] private Slider masterSlider;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;

        [Header("Audio Labels")]
        [SerializeField] private Text masterValueText;
        [SerializeField] private Text musicValueText;
        [SerializeField] private Text sfxValueText;

        [Header("Display")]
        [SerializeField] private Toggle fullscreenToggle;

        [Header("Buttons")]
        [SerializeField] private Button backButton;

        [Tooltip("Returns to the title screen. Shown in the in-game settings panel, " +
                 "where leaving the run is more useful than resetting the sliders.")]
        [SerializeField] private Button menuButton;

        [Tooltip("Scene loaded by the menu button")]
        [SerializeField] private string menuSceneName = "TitleScene";

        [Tooltip("Reset the game speed to normal before leaving.\n" +
                 "The panel pauses the game, and a scene load keeps Time.timeScale, " +
                 "so without this the title screen would come up frozen.")]
        [SerializeField] private bool restoreTimeScaleOnMenu = true;

        // PlayerPrefs keys, kept short so they are readable in the registry.
        private const string KeyMaster = "duck_master_vol";
        private const string KeyMusic = "duck_music_vol";
        private const string KeySfx = "duck_sfx_vol";
        private const string KeyFullscreen = "duck_fullscreen";

        private bool _initialized;

        private void Awake()
        {
            // The panel object is disabled in the saved scene, so Awake may run
            // only once Open() activates it. Initialise defensively and let Open()
            // retry, otherwise the buttons would never get their listeners.
            Initialize();
        }

        /// <summary>
        /// Wires the controls and loads saved values. Safe to call more than once.
        /// </summary>
        private void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            if (backButton != null) backButton.onClick.AddListener(Close);
            if (menuButton != null) menuButton.onClick.AddListener(ReturnToMenu);

            if (masterSlider != null) masterSlider.onValueChanged.AddListener(OnMasterChanged);
            if (musicSlider != null) musicSlider.onValueChanged.AddListener(OnMusicChanged);
            if (sfxSlider != null) sfxSlider.onValueChanged.AddListener(OnSfxChanged);
            if (fullscreenToggle != null) fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);

            LoadAndApply();
        }

        private void Start()
        {
            // Start runs the first time the object becomes active, which is exactly
            // when the panel is opened.
            Initialize();
        }

        /// <summary>
        /// Raised whenever the panel hides itself, including via its own Back button.
        /// The menu controller uses it to take the shared dimmer down with it.
        /// </summary>
        public event System.Action Closed;

        public void Open()
        {
            Initialize();

            if (panelRoot != null) panelRoot.SetActive(true);
            RefreshUi();
        }

        public void Close()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
            Save();
            Closed?.Invoke();
        }

        /// <summary>
        /// Hides the panel without writing PlayerPrefs or firing Closed. Used during
        /// startup, where saving would overwrite the player's stored values.
        /// </summary>
        public void HideWithoutSaving()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

        // ------------------------------------------------------------------

        private void LoadAndApply()
        {
            float master = PlayerPrefs.GetFloat(KeyMaster, 1f);
            float music = PlayerPrefs.GetFloat(KeyMusic, 0.8f);
            float sfx = PlayerPrefs.GetFloat(KeySfx, 1f);
            bool fullscreen = PlayerPrefs.GetInt(KeyFullscreen, Screen.fullScreen ? 1 : 0) == 1;

            // SetValueWithoutNotify avoids firing the callbacks while loading.
            if (masterSlider != null) masterSlider.SetValueWithoutNotify(master);
            if (musicSlider != null) musicSlider.SetValueWithoutNotify(music);
            if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(sfx);
            if (fullscreenToggle != null) fullscreenToggle.SetIsOnWithoutNotify(fullscreen);

            AudioListener.volume = master;

            if (Screen.fullScreen != fullscreen)
            {
                Screen.fullScreen = fullscreen;
            }

            UpdateValueLabels(master, music, sfx);
        }

        private void RefreshUi()
        {
            float master = masterSlider != null ? masterSlider.value : 1f;
            float music = musicSlider != null ? musicSlider.value : 0.8f;
            float sfx = sfxSlider != null ? sfxSlider.value : 1f;
            UpdateValueLabels(master, music, sfx);
        }

        private void UpdateValueLabels(float master, float music, float sfx)
        {
            if (masterValueText != null) masterValueText.text = Mathf.RoundToInt(master * 100f) + "%";
            if (musicValueText != null) musicValueText.text = Mathf.RoundToInt(music * 100f) + "%";
            if (sfxValueText != null) sfxValueText.text = Mathf.RoundToInt(sfx * 100f) + "%";
        }

        private void OnMasterChanged(float value)
        {
            AudioListener.volume = value;
            if (masterValueText != null) masterValueText.text = Mathf.RoundToInt(value * 100f) + "%";
            Save();
        }

        private void OnMusicChanged(float value)
        {
            if (musicValueText != null) musicValueText.text = Mathf.RoundToInt(value * 100f) + "%";
            Save();
            NotifyDirector();
        }

        private void OnSfxChanged(float value)
        {
            if (sfxValueText != null) sfxValueText.text = Mathf.RoundToInt(value * 100f) + "%";
            Save();
            NotifyDirector();
        }

        /// <summary>
        /// Hands the new levels to the audio director straight away. Saving alone only
        /// changes what the next launch will sound like; the music already playing on
        /// the title screen would otherwise ignore the slider entirely.
        /// </summary>
        private void NotifyDirector()
        {
            if (DuckKing.Audio.AudioDirector.Instance != null)
            {
                DuckKing.Audio.AudioDirector.Instance.ApplyVolumes();
            }
        }

        private void OnFullscreenChanged(bool value)
        {
            Screen.fullScreen = value;
            Save();
        }

        /// <summary>
        /// Saves the current levels and goes back to the title screen.
        ///
        /// The game speed has to be put back here. A settings panel opened during a run
        /// sets Time.timeScale to zero, and loading a scene does not reset it, so leaving
        /// without this would drop the player on a frozen title screen. The pause guard
        /// also restores its own saved value, so both are cleared before the load.
        /// </summary>
        public void ReturnToMenu()
        {
            Save();

            if (restoreTimeScaleOnMenu)
            {
                Time.timeScale = 1f;
            }

            if (string.IsNullOrEmpty(menuSceneName))
            {
                Debug.LogWarning("[SettingsPanel] Menu Scene Name is empty; staying in this scene.", this);
                Close();
                return;
            }

            DuckKing.UI.PauseStateGuard.ForceResume();

            if (panelRoot != null) panelRoot.SetActive(false);
            UnityEngine.SceneManagement.SceneManager.LoadScene(menuSceneName);
        }

        // ------------------------------------------------------------------

        /// <summary>
        /// Music and SFX volumes are the same two values the in-game AudioSettings
        /// panel owns; both write the duck_music_vol / duck_sfx_vol keys and both push
        /// the result into the audio director, so the two screens cannot drift apart.
        /// </summary>
        public float MusicVolume => musicSlider != null ? musicSlider.value : 0.8f;
        public float SfxVolume => sfxSlider != null ? sfxSlider.value : 1f;

        public static float GetSavedMusicVolume() => PlayerPrefs.GetFloat(KeyMusic, 0.8f);
        public static float GetSavedSfxVolume() => PlayerPrefs.GetFloat(KeySfx, 1f);

        private void Save()
        {
            PlayerPrefs.SetFloat(KeyMaster, masterSlider != null ? masterSlider.value : 1f);
            PlayerPrefs.SetFloat(KeyMusic, musicSlider != null ? musicSlider.value : 0.8f);
            PlayerPrefs.SetFloat(KeySfx, sfxSlider != null ? sfxSlider.value : 1f);
            PlayerPrefs.SetInt(KeyFullscreen,
                (fullscreenToggle != null ? fullscreenToggle.isOn : Screen.fullScreen) ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}