using DuckKing.Gameplay;
using UnityEngine;

namespace DuckKing.Audio
{
    /// <summary>
    /// Plays the ending theme when the run ends.
    ///
    /// It listens to the game manager instead of being wired to the finish chest,
    /// so the sound fires once the run is really over regardless of how it was
    /// ended: the chest trigger, a debug key, or a future ending screen.
    ///
    /// The jingle takes the speakers for its duration and then crossfades back to
    /// whatever loop was playing, so the level never ends up silent.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public class EndingMusic : MonoBehaviour
    {
        [Tooltip("Ending theme, played once")]
        [SerializeField] private AudioClip endingMusic;

        [Header("Fades")]
        [SerializeField] private float fadeIn = 0.6f;
        [SerializeField] private float fadeOut = 1.5f;

        [Header("Level")]
        [Range(0f, 2f)]
        [SerializeField] private float volumeScale = 1f;

        [Tooltip("Keep the level music quiet instead of returning to it once the ending plays")]
        [SerializeField] private bool stopLevelMusic = false;

        private bool _played;

        private void Start()
        {
            var manager = SceneGameManager.Instance;
            if (manager == null) return;

            // The manager survives until the scene unloads, so this listener cannot
            // outlive the object it belongs to.
            manager.OnFinished += OnFinished;
            if (manager.IsFinished) OnFinished(manager.Result);
        }

        private void OnDestroy()
        {
            var manager = SceneGameManager.Instance;
            if (manager != null) manager.OnFinished -= OnFinished;
        }

        private void OnFinished(SceneGameManager.Outcome outcome)
        {
            if (_played) return;
            _played = true;

            if (stopLevelMusic)
            {
                AudioDirector.StopLoopPlayback(fadeIn);
            }

            AudioDirector.PlayJingleClip(endingMusic, fadeIn, fadeOut, volumeScale);

            // One quick quack per duck. Both ducks are found inside the coroutine
            // because the ending screen may have been triggered from a scene object
            // that is already tearing down.
            StartCoroutine(QuackAfter(0.35f));
            StartCoroutine(QuackAfter(0.6f));
        }

        private static System.Collections.IEnumerator QuackAfter(float delay)
        {
            yield return new WaitForSecondsRealtime(delay);

            var quacks = FindObjectsOfType<DuckSfx>();
            for (int i = 0; i < quacks.Length; i++)
            {
                quacks[i].PlayQuack();
            }
        }

        /// <summary>Lets the scene call this directly if a cutscene needs to force the ending.</summary>
        public void Play()
        {
            OnFinished(SceneGameManager.Instance != null ? SceneGameManager.Instance.Result : SceneGameManager.Outcome.None);
        }
    }
}