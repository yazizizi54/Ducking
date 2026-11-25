using System.Collections;
using DuckKing.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DuckKing.UI
{
    /// <summary>
    /// End-of-run screen.
    ///
    /// Three outcomes share one layout: player 1 wins, player 2 wins, or a tie.
    /// The tie is the "shared rule" ending and gets its own headline, which is
    /// the cheapest way to deliver a second ending in a jam build.
    ///
    /// The screen builds itself from whatever fields are wired, so missing art
    /// only removes visual polish rather than breaking the flow.
    /// </summary>
    public class ResultScreen : MonoBehaviour
    {
        [Header("Root")]
        [Tooltip("Parent object holding the whole result layout; hidden until the run ends")]
        [SerializeField] private GameObject root;
        [Tooltip("Black overlay behind the panel")]
        [SerializeField] private Image dimmer;

        [Header("Text")]
        [SerializeField] private Text headlineText;
        [SerializeField] private Text bodyText;

        [Header("Buttons")]
        [SerializeField] private Button restartButton;
        [SerializeField] private Button menuButton;
        [SerializeField] private string menuSceneName = "TitleScene";

        [Header("Copy")]
        [SerializeField] private string headlinePlayerOne = "DUCK 1 IS THE DUCK KING";
        [SerializeField] private string headlinePlayerTwo = "DUCK 2 IS THE DUCK KING";
        [SerializeField] private string headlineTie = "TWO KINGS, ONE THRONE";
        [Tooltip("Extra line shown on the tie ending")]
        [SerializeField] private string tieFlavor = "Nobody let go. Nobody got left behind.";

        [Header("Timing")]
        [Tooltip("Delay after the run ends before the screen appears")]
        [SerializeField] private float showDelay = 0.45f;
        [SerializeField] private float fadeDuration = 0.35f;

        [Tooltip("Allow Space or Enter to restart")]
        [SerializeField] private bool keyboardRestart = true;

        private bool _shown;
        private bool _restarting;

        private void Awake()
        {
            if (root != null) root.SetActive(false);
            if (dimmer != null) dimmer.gameObject.SetActive(false);

            if (restartButton != null) restartButton.onClick.AddListener(Restart);
            if (menuButton != null) menuButton.onClick.AddListener(GoToMenu);
        }

        private void Start()
        {
            if (SceneGameManager.Instance != null)
            {
                SceneGameManager.Instance.OnFinished += HandleFinished;
            }
        }

        private void OnDestroy()
        {
            if (SceneGameManager.Instance != null)
            {
                SceneGameManager.Instance.OnFinished -= HandleFinished;
            }
        }

        private void HandleFinished(SceneGameManager.Outcome outcome)
        {
            if (_shown) return;
            _shown = true;

            StartCoroutine(ShowRoutine(outcome));
        }

        private IEnumerator ShowRoutine(SceneGameManager.Outcome outcome)
        {
            if (showDelay > 0f) yield return new WaitForSecondsRealtime(showDelay);

            FillText(outcome);

            if (root != null) root.SetActive(true);

            if (dimmer != null)
            {
                dimmer.gameObject.SetActive(true);
                float t = 0f;
                Color c = dimmer.color;
                c.a = 0f;
                dimmer.color = c;

                while (t < fadeDuration)
                {
                    t += Time.unscaledDeltaTime;
                    c.a = Mathf.Clamp01(t / Mathf.Max(0.01f, fadeDuration)) * 0.72f;
                    dimmer.color = c;
                    yield return null;
                }
            }

            // Stop the ducks so the scene freezes behind the result panel.
            Time.timeScale = 0f;
        }

        private void FillText(SceneGameManager.Outcome outcome)
        {
            var gm = SceneGameManager.Instance;

            string headline;
            if (outcome == SceneGameManager.Outcome.PlayerA) headline = headlinePlayerOne;
            else if (outcome == SceneGameManager.Outcome.PlayerB) headline = headlinePlayerTwo;
            else headline = headlineTie;

            if (headlineText != null) headlineText.text = headline;

            if (bodyText != null && gm != null)
            {
                // The clock replaced the coin count as the headline stat, so the
                // body reports the run time rather than the old score line.
                string timeLine = $"Time  {gm.ElapsedDisplay}";

                bodyText.text = outcome == SceneGameManager.Outcome.Shared
                    ? $"{timeLine}\n\n{tieFlavor}"
                    : timeLine;
            }
        }

        private void Update()
        {
            if (!_shown || !keyboardRestart || _restarting) return;

            if (UnityEngine.Input.GetKeyDown(KeyCode.Space) ||
                UnityEngine.Input.GetKeyDown(KeyCode.Return))
            {
                Restart();
            }
        }

        public void Restart()
        {
            if (_restarting) return;
            _restarting = true;
            Time.timeScale = 1f;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        public void GoToMenu()
        {
            if (_restarting) return;
            if (string.IsNullOrEmpty(menuSceneName)) return;

            _restarting = true;
            Time.timeScale = 1f;
            SceneManager.LoadScene(menuSceneName);
        }
    }
}