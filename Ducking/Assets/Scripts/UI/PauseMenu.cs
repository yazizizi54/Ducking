using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DuckKing.UI
{
    /// <summary>
    /// In-game pause menu. Deliberately small: it opens a panel, pauses the
    /// clock, and offers two ways out.
    ///
    /// Every field is optional, so a half-built panel still works — a missing
    /// button simply does nothing instead of throwing.
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        [Header("Panel")]
        [Tooltip("Panel object shown while paused")]
        [SerializeField] private GameObject panel;
        [Tooltip("Optional full-screen dimmer behind the panel")]
        [SerializeField] private Image backdrop;

        [Header("Buttons")]
        [Tooltip("Closes the panel and resumes the run")]
        [SerializeField] private Button resumeButton;
        [Tooltip("Leaves the run and returns to the title screen")]
        [SerializeField] private Button menuButton;

        [Header("Scenes")]
        [Tooltip("Scene loaded by the menu button")]
        [SerializeField] private string menuSceneName = "TitleScene";

        [Header("Behaviour")]
        [Tooltip("Freeze the game while the panel is open")]
        [SerializeField] private bool pauseWhileOpen = true;
        [Tooltip("Close the panel with Escape")]
        [SerializeField] private bool closeWithEscape = true;
        [Tooltip("Hide the panel when the scene starts")]
        [SerializeField] private bool hideOnStart = true;

        private bool _open;
        private float _timeScaleBeforeOpen = 1f;

        /// <summary>Optional hook so the HUD can stop its own timer as well.</summary>
        public event System.Action<bool> OpenChanged;

        public bool IsOpen => _open;

        private void Awake()
        {
            // A full-screen dimmer must not swallow clicks, or the buttons inside
            // the panel become unreachable. Same trap as the title screen.
            if (backdrop != null) backdrop.raycastTarget = false;

            if (resumeButton != null)
            {
                resumeButton.onClick.RemoveListener(Resume);
                resumeButton.onClick.AddListener(Resume);
            }

            if (menuButton != null)
            {
                menuButton.onClick.RemoveListener(ReturnToMenu);
                menuButton.onClick.AddListener(ReturnToMenu);
            }
        }

        private void Start()
        {
            if (hideOnStart) SetOpen(false);
        }

        private void Update()
        {
            if (closeWithEscape && _open && UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                Resume();
            }
        }

        /// <summary>Opens the panel. The usual entry point is the HUD's gear button.</summary>
        public void Open()
        {
            SetOpen(true);
        }

        /// <summary>Closes the panel and resumes play.</summary>
        public void Resume()
        {
            SetOpen(false);
        }

        public void Toggle()
        {
            SetOpen(!_open);
        }

        public void SetOpen(bool open)
        {
            if (_open == open) return;
            _open = open;

            if (panel != null) panel.SetActive(open);
            if (backdrop != null) backdrop.gameObject.SetActive(open);

            // Keep the panel above the dimmer no matter how the scene was built.
            if (open && panel != null) panel.transform.SetAsLastSibling();

            if (pauseWhileOpen)
            {
                if (open)
                {
                    _timeScaleBeforeOpen = Time.timeScale;
                    Time.timeScale = 0f;
                }
                else
                {
                    Time.timeScale = _timeScaleBeforeOpen <= 0f ? 1f : _timeScaleBeforeOpen;
                }
            }

            OpenChanged?.Invoke(open);
        }

        /// <summary>Leaves the run. Also used by the result screen.</summary>
        public void ReturnToMenu()
        {
            // A paused game would carry timeScale 0 into the menu and freeze it.
            Time.timeScale = 1f;

            if (!string.IsNullOrEmpty(menuSceneName))
            {
                SceneManager.LoadScene(menuSceneName);
            }
        }

        /// <summary>Reloads the current level from the top.</summary>
        public void RestartLevel()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}