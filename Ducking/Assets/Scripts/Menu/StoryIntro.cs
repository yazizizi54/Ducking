using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DuckKing.Menu
{
    /// <summary>
    /// Black-screen story intro played between the title screen and the game.
    ///
    /// Each line fades in, holds, then fades out before the next one starts, so the
    /// whole thing runs unattended. A single scene object drives it; there is no
    /// Timeline and no animation clip to maintain.
    ///
    /// The screen deliberately has no clickable elements. Every Image here has
    /// raycastTarget off, so the intro can never block input the way the menu
    /// backdrop once did.
    /// </summary>
    public class StoryIntro : MonoBehaviour
    {
        [Header("Scenes")]
        [Tooltip("Scene loaded once the story finishes")]
        [SerializeField] private string nextSceneName = "SampleScene";

        [Header("Text")]
        [Tooltip("One entry per screen. Each fades in, holds, then fades out.")]
        [TextArea(2, 6)]
        [SerializeField]
        private string[] lines =
        {
            "An old legend says the Duck King's mountain\nhides a fortune in treasure.",
            "But a curse guards the foot of the mountain:\na lone duck can never move forward.",
            "The curse also swapped their jump keys.\nEach duck now presses the other's key.",
            "Only two ducks, bound as one,\ncan reach the summit."
        };

        [Header("Timing (seconds)")]
        [Tooltip("Black screen before the first line appears")]
        [SerializeField] private float leadIn = 0.8f;
        [Tooltip("Fade in duration for each line")]
        [SerializeField] private float fadeIn = 0.6f;
        [Tooltip("How long each line stays fully visible")]
        [SerializeField] private float holdTime = 2.2f;
        [Tooltip("Fade out duration for each line")]
        [SerializeField] private float fadeOut = 0.5f;
        [Tooltip("Pause between lines")]
        [SerializeField] private float gapBetweenLines = 0.35f;
        [Tooltip("Black screen at the end before loading the game")]
        [SerializeField] private float leadOut = 0.6f;

        [Header("References")]
        [Tooltip("Full screen black Image")]
        [SerializeField] private Image blackOverlay;
        [Tooltip("The Text that shows the story")]
        [SerializeField] private Text storyText;

        [Header("Behaviour")]
        [Tooltip("Ignore the next scene name and load the current scene index instead")]
        [SerializeField] private bool loadNextBuildIndex = false;

        [Tooltip("Seconds to block input after the intro opens")]
        [SerializeField] private float inputGraceTime = 0.3f;

        [Tooltip("Allow Space or Escape to skip the whole intro")]
        [SerializeField] private bool allowSkip = true;

        private bool _finished;
        private float _openTime;

        private void Awake()
        {
            _openTime = Time.unscaledTime;

            // The intro owns the screen: solid black behind the text, and neither
            // element may intercept clicks.
            if (blackOverlay != null)
            {
                blackOverlay.raycastTarget = false;
                blackOverlay.color = new Color(0f, 0f, 0f, 1f);
            }

            if (storyText != null)
            {
                storyText.raycastTarget = false;
                SetTextAlpha(0f);
                storyText.text = string.Empty;
            }
        }

        private void Start()
        {
            StartCoroutine(PlayRoutine());
        }

        private void Update()
        {
            if (!allowSkip || _finished) return;
            if (Time.unscaledTime - _openTime < inputGraceTime) return;

            if (UnityEngine.Input.GetKeyDown(KeyCode.Space) ||
                UnityEngine.Input.GetKeyDown(KeyCode.Escape))
            {
                Finish();
            }
        }

        private IEnumerator PlayRoutine()
        {
            if (blackOverlay != null) blackOverlay.color = new Color(0f, 0f, 0f, 1f);

            yield return Wait(leadIn);

            for (int i = 0; i < lines.Length; i++)
            {
                if (_finished) yield break;

                if (storyText != null) storyText.text = lines[i];

                yield return FadeText(0f, 1f, fadeIn);
                if (_finished) yield break;

                yield return Wait(holdTime);
                if (_finished) yield break;

                yield return FadeText(1f, 0f, fadeOut);
                if (_finished) yield break;

                yield return Wait(gapBetweenLines);
            }

            yield return Wait(leadOut);
            Finish();
        }

        /// <summary>Fades the story text between two alpha values.</summary>
        private IEnumerator FadeText(float from, float to, float duration)
        {
            if (storyText == null || duration <= 0f)
            {
                SetTextAlpha(to);
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                SetTextAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(t / duration)));
                yield return null;
            }

            SetTextAlpha(to);
        }

        private void SetTextAlpha(float a)
        {
            if (storyText == null) return;

            Color c = storyText.color;
            c.a = a;
            storyText.color = c;
        }

        /// <summary>
        /// Unscaled wait. The intro runs during scene transitions where timeScale may
        /// be zero, so scaled waits would stall it forever.
        /// </summary>
        private IEnumerator Wait(float seconds)
        {
            if (seconds <= 0f) yield break;

            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        /// <summary>Ends the intro and loads the game scene. Safe to call twice.</summary>
        public void Finish()
        {
            if (_finished) return;
            _finished = true;

            StopAllCoroutines();
            Time.timeScale = 1f;

            if (loadNextBuildIndex)
            {
                int index = SceneManager.GetActiveScene().buildIndex + 1;
                if (index < SceneManager.sceneCountInBuildSettings)
                {
                    SceneManager.LoadScene(index);
                    return;
                }
            }

            if (string.IsNullOrEmpty(nextSceneName))
            {
                Debug.LogError("[DuckKing] StoryIntro has no next scene configured.");
                return;
            }

            SceneManager.LoadScene(nextSceneName);
        }
    }
}