using DuckKing.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DuckKing.UI
{
    /// <summary>
    /// In-game HUD: a run timer across the top centre and a settings button in the
    /// top-right corner. Scores were removed on purpose — the clock is the only
    /// number that matters now, so the screen stays clean while climbing.
    ///
    /// Every reference is optional. The script never throws on missing art, which
    /// keeps the jam build playable while the layout is still being rebuilt.
    /// </summary>
    public class GameHud : MonoBehaviour
    {
        [Header("Timer (top centre)")]
        [Tooltip("Text that shows the elapsed run time")]
        [SerializeField] private Text timerText;
        [Tooltip("Optional prefix, e.g. \"TIME  \"")]
        [SerializeField] private string timerPrefix = "";
        [Tooltip("Optional small caption above the timer")]
        [SerializeField] private Text timerLabel;

        [Header("Settings (top right)")]
        [Tooltip("Button that opens the settings panel")]
        [SerializeField] private Button settingsButton;
        [Tooltip("Panel toggled by the settings button")]
        [SerializeField] private GameObject settingsPanel;
        [Tooltip("Optional dimmer behind the settings panel")]
        [SerializeField] private Image settingsBackdrop;

        [Header("Waiting to start")]
        [Tooltip("Text shown under the timer until the player presses a key")]
        [SerializeField] private Text waitingHint;
        [Tooltip("Copy shown while waiting for the start key")]
        [SerializeField] private string waitingMessage = "PRESS ANY KEY TO START";

        [Header("Behaviour")]
        [Tooltip("Freeze the game while the settings panel is open. The run clock keeps " +
                 "counting regardless, because the panel should cost time.")]
        [SerializeField] private bool pauseWhileSettingsOpen = true;
        [Tooltip("Start hidden; useful when the settings panel is built in the scene")]
        [SerializeField] private bool hideSettingsOnStart = true;
        [Tooltip("The HUD keeps its own clock, so the timer still runs when the " +
                 "SceneGameManager component is disabled in the scene.")]
        [SerializeField] private bool selfDrivenClock = true;

        private bool _settingsOpen;
        private float _timeScaleBeforeOpen = 1f;
        private float _localTime;
        private bool _localRunning = true;

        private void Awake()
        {
            if (settingsBackdrop != null)
            {
                // A full-screen dimmer must not eat clicks, or the panel inside it
                // becomes unreachable. Same trap as the title screen backdrop.
                settingsBackdrop.raycastTarget = false;
            }

            if (settingsButton != null)
            {
                // Clear first so re-running Awake on a rebuilt object cannot double-bind.
                settingsButton.onClick.RemoveListener(ToggleSettings);
                settingsButton.onClick.AddListener(ToggleSettings);
            }
        }

        private void Start()
        {
            if (hideSettingsOnStart) SetSettingsVisible(false);

            if (timerLabel != null && string.IsNullOrEmpty(timerLabel.text))
            {
                timerLabel.text = "TIME";
            }

            RefreshTimer();
        }

        private void Update()
        {
            var gm = SceneGameManager.Instance;
            bool waiting = gm != null && gm.IsWaitingForStart;

            if (selfDrivenClock && _localRunning && !waiting)
            {
                _localTime += Time.unscaledDeltaTime;
            }

            RefreshTimer();
            RefreshWaitingHint(waiting);

            // Escape closes the settings panel, matching the usual convention.
            if (_settingsOpen && UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                ToggleSettings();
            }
        }

        /// <summary>
        /// Shows the "press any key" prompt while the run is gated, and hides it the
        /// moment the gate opens. Without this the player sees a frozen 00:00.00 and
        /// has no way to know the game is waiting on them.
        /// </summary>
        private void RefreshWaitingHint(bool waiting)
        {
            if (waitingHint == null) return;

            waitingHint.text = waitingMessage;
            waitingHint.enabled = waiting;
        }

        private void RefreshTimer()
        {
            if (timerText == null) return;

            timerText.text = timerPrefix + CurrentTimeDisplay();
        }

        /// <summary>
        /// Prefers the manager's clock, which is the authority on run state, and
        /// falls back to this component's own count while the manager is missing or
        /// disabled. A disabled component never runs Update, so depending on it
        /// alone leaves the timer frozen at zero with no visible cause.
        /// </summary>
        private string CurrentTimeDisplay()
        {
            var gm = SceneGameManager.Instance;

            if (gm != null && IsManagerRunning(gm))
            {
                return gm.ElapsedDisplay;
            }

            return FormatTime(_localTime);
        }

        private static bool IsManagerRunning(SceneGameManager gm)
        {
            // A disabled MonoBehaviour still becomes Instance via Awake only if it
            // was enabled at least once, so check the flag as well as the reference.
            return gm.isActiveAndEnabled;
        }

        private static string FormatTime(float t)
        {
            t = Mathf.Max(0f, t);
            int minutes = (int)(t / 60f);
            int seconds = (int)(t % 60f);
            int hundredths = (int)((t * 100f) % 100f);
            return $"{minutes:00}:{seconds:00}.{hundredths:00}";
        }

        /// <summary>Shows or hides the settings panel, pausing the game if configured.</summary>
        public void ToggleSettings()
        {
            SetSettingsVisible(!_settingsOpen);
        }

        public void SetSettingsVisible(bool visible)
        {
            if (_settingsOpen == visible) return;

            _settingsOpen = visible;

            if (settingsPanel != null) settingsPanel.SetActive(visible);
            if (settingsBackdrop != null) settingsBackdrop.gameObject.SetActive(visible);

            // Keep the panel above the dimmer regardless of how the scene was built.
            if (visible && settingsPanel != null) settingsPanel.transform.SetAsLastSibling();

            if (pauseWhileSettingsOpen)
            {
                if (visible)
                {
                    _timeScaleBeforeOpen = Time.timeScale;
                    Time.timeScale = 0f;
                }
                else
                {
                    Time.timeScale = _timeScaleBeforeOpen <= 0f ? 1f : _timeScaleBeforeOpen;
                }
            }

            // The clock deliberately keeps running while the panel is open: reading the
            // settings screen should cost the player time, not freeze the run for free.
            // Nothing is paused here; the manager clock only stops when the run ends.
        }

        public bool IsSettingsOpen => _settingsOpen;

        /// <summary>Leaves the run and returns to the title screen.</summary>
        public void ReturnToMenu(string menuSceneName = "TitleScene")
        {
            // A paused game would carry timeScale 0 into the menu and freeze it.
            Time.timeScale = 1f;

            if (!string.IsNullOrEmpty(menuSceneName))
            {
                SceneManager.LoadScene(menuSceneName);
            }
        }

        /// <summary>Restarts the current level from the top.</summary>
        public void RestartLevel()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}