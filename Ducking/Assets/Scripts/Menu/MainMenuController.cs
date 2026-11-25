using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DuckKing.Menu
{
    /// <summary>
    /// Title screen controller.
    ///
    /// Keeps the data side (which scene to load, what the buttons do) separate
    /// from the visuals, so art can restyle the Canvas without touching code.
    ///
    /// Works with either:
    ///  - UI Buttons wired in the Canvas, or
    ///  - a "press any key to start" fallback for a fast jam build.
    /// </summary>
    public class MainMenuController : MonoBehaviour
    {
        [Header("Scenes")]
        [Tooltip("Scene loaded by the Start button")]
        [SerializeField] private string gameSceneName = "SampleScene";

        [Tooltip("Optional story scene played before the game. Leave empty to skip it.")]
        [SerializeField] private string introSceneName = "";

        [Tooltip("Optional trailer or credits scene; leave empty to use the in-menu panel")]
        [SerializeField] private string creditsSceneName = "";

        [Header("Buttons (optional)")]
        [SerializeField] private Button startButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button creditsButton;
        [SerializeField] private Button quitButton;

        [Header("Panels (optional)")]
        [Tooltip("Settings panel opened by the Settings button")]
        [SerializeField] private SettingsPanel settingsPanel;

        [Tooltip("Credits panel opened by the Credits button")]
        [SerializeField] private CreditsPanel creditsPanel;

        [Tooltip("Full screen click-catcher behind an open panel")]
        [SerializeField] private GameObject modalBackdrop;

        [Header("Behaviour")]
        [Tooltip("Start the game when any of the duck keys are pressed")]
        [SerializeField] private bool pressAnyKeyToStart = true;

        [Tooltip("Keys that skip the menu")]
        [SerializeField] private KeyCode[] startKeys =
        {
            KeyCode.Space,
            KeyCode.Return,
            KeyCode.W,
            KeyCode.UpArrow
        };

        [Tooltip("Seconds to block input after the scene opens, so a mashed key does not skip the title")]
        [SerializeField] private float inputGraceTime = 0.35f;

        [Tooltip("Fade out duration before loading")]
        [SerializeField] private float fadeOutDuration = 0.35f;

        [Header("Fade (optional)")]
        [Tooltip("Full screen black Image used for the transition")]
        [SerializeField] private Image fadeOverlay;

        private bool _starting;
        private float _openTime;

        private void Awake()
        {
            _openTime = Time.unscaledTime;

            if (startButton != null) startButton.onClick.AddListener(StartGame);
            if (settingsButton != null) settingsButton.onClick.AddListener(OpenSettings);
            if (creditsButton != null) creditsButton.onClick.AddListener(OpenCredits);
            if (quitButton != null) quitButton.onClick.AddListener(QuitGame);

            if (settingsButton != null && settingsPanel == null)
            {
                settingsButton.gameObject.SetActive(false);
            }

            if (creditsButton != null && creditsPanel == null && string.IsNullOrEmpty(creditsSceneName))
            {
                creditsButton.gameObject.SetActive(false);
            }

            if (modalBackdrop != null) modalBackdrop.SetActive(false);

            // Panels hide themselves only once their own Awake runs. A panel object
            // that is inactive in the saved scene has not run Awake yet, so hide them
            // from here too and guarantee a clean title screen on load.
            if (settingsPanel != null)
            {
                settingsPanel.HideWithoutSaving();
                // The panel's own Back button closes it directly; without this the
                // shared dimmer would stay on screen forever.
                settingsPanel.Closed += OnPanelClosed;
            }

            if (creditsPanel != null)
            {
                creditsPanel.HideWithoutSaving();
                creditsPanel.Closed += OnPanelClosed;
            }

            EnsureLayerOrder();

            if (fadeOverlay != null)
            {
                Color c = fadeOverlay.color;
                c.a = 0f;
                fadeOverlay.color = c;
                fadeOverlay.raycastTarget = false;
            }
        }

        /// <summary>
        /// True while a modal panel is covering the menu. The title screen must not
        /// start the game from a stray keypress in that state.
        /// </summary>
        private bool AnyPanelOpen
        {
            get
            {
                if (settingsPanel != null && settingsPanel.IsOpen) return true;
                if (creditsPanel != null && creditsPanel.IsOpen) return true;
                return false;
            }
        }

        private void Update()
        {
            if (_starting) return;

            // Escape closes whichever panel is open; it never quits from the title.
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                if (settingsPanel != null && settingsPanel.IsOpen)
                {
                    ClosePanels();
                    return;
                }

                if (creditsPanel != null && creditsPanel.IsOpen)
                {
                    ClosePanels();
                    return;
                }
            }

            if (AnyPanelOpen) return;
            if (!pressAnyKeyToStart) return;
            if (Time.unscaledTime - _openTime < inputGraceTime) return;

            for (int i = 0; i < startKeys.Length; i++)
            {
                if (UnityEngine.Input.GetKeyDown(startKeys[i]))
                {
                    StartGame();
                    return;
                }
            }
        }

        public void OpenSettings()
        {
            if (_starting) return;

            ClosePanels();

            if (modalBackdrop != null) modalBackdrop.SetActive(true);
            if (settingsPanel != null)
            {
                settingsPanel.Open();
                // Above the backdrop, or the Back button would be unreachable.
                settingsPanel.transform.SetAsLastSibling();
            }
        }

        /// <summary>
        /// Forces a known draw order inside the shared Canvas on startup.
        ///
        /// UI draws in sibling order, last on top, and the first object under the
        /// pointer wins the click. A scene built by re-running the setup tool can
        /// leave the backdrop or stray buttons above a panel, so the dimmer ends up
        /// swallowing every press and Back never fires. Reordering here removes any
        /// dependency on how the scene happens to be laid out.
        /// </summary>
        private void EnsureLayerOrder()
        {
            // The dimmer only darkens the screen; it must never eat pointer events.
            if (modalBackdrop != null)
            {
                var img = modalBackdrop.GetComponent<Image>();
                if (img != null) img.raycastTarget = false;

                modalBackdrop.transform.SetAsLastSibling();
            }

            // Panels must render above the dimmer.
            if (settingsPanel != null) settingsPanel.transform.SetAsLastSibling();
            if (creditsPanel != null) creditsPanel.transform.SetAsLastSibling();
        }

        /// <summary>
        /// Called when a panel closes itself, for example from its own Back button.
        /// Takes the shared dimmer down so the title screen is not left greyed out.
        /// </summary>
        private void OnPanelClosed()
        {
            if (AnyPanelOpen) return;

            if (modalBackdrop != null) modalBackdrop.SetActive(false);
        }

        public void ClosePanels()
        {
            if (settingsPanel != null && settingsPanel.IsOpen) settingsPanel.Close();
            if (creditsPanel != null && creditsPanel.IsOpen) creditsPanel.Close();
            if (modalBackdrop != null) modalBackdrop.SetActive(false);
        }

        public void StartGame()
        {
            if (_starting) return;

            // Playing from an open panel should close it first, not leak into the game.
            ClosePanels();

            // A configured intro scene plays before the game; otherwise go straight in.
            string target = string.IsNullOrEmpty(introSceneName) ? gameSceneName : introSceneName;
            StartCoroutine(LoadWithFade(target));
        }

        public void OpenCredits()
        {
            if (_starting) return;

            // A dedicated credits scene wins over the in-menu panel when one is set.
            if (!string.IsNullOrEmpty(creditsSceneName))
            {
                StartCoroutine(LoadWithFade(creditsSceneName));
                return;
            }

            ClosePanels();

            if (modalBackdrop != null) modalBackdrop.SetActive(true);
            if (creditsPanel != null)
            {
                creditsPanel.Open();
                // Above the backdrop, or the Back button would be unreachable.
                creditsPanel.transform.SetAsLastSibling();
            }
        }

        public void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private IEnumerator LoadWithFade(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
            {
                Debug.LogError("[DuckKing] Main menu has no scene name configured.");
                yield break;
            }

            _starting = true;

            if (fadeOverlay != null && fadeOutDuration > 0f)
            {
                float t = 0f;
                while (t < fadeOutDuration)
                {
                    t += Time.unscaledDeltaTime;
                    Color c = fadeOverlay.color;
                    c.a = Mathf.Clamp01(t / fadeOutDuration);
                    fadeOverlay.color = c;
                    yield return null;
                }
            }

            Time.timeScale = 1f;
            SceneManager.LoadScene(sceneName);
        }
    }
}