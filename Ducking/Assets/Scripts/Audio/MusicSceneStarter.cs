using UnityEngine;

namespace DuckKing.Audio
{
    /// <summary>
    /// Starts the looping background track for whatever scene this sits in.
    ///
    /// It lives on one object per scene while the director itself survives scene
    /// loads, so the music keeps its position when moving from the title screen
    /// into the story and then into the level.
    ///
    /// Without a director in the scene it spawns one, which is what makes the
    /// title screen work even though the director object only exists in the level.
    /// </summary>
    [DefaultExecutionOrder(-45)]
    public class MusicSceneStarter : MonoBehaviour
    {
        [Tooltip("Track looped in this scene")]
        [SerializeField] private AudioClip music;

        [Tooltip("Seconds of crossfade from whatever was playing")]
        [SerializeField] private float fadeTime = 1.2f;

        [Tooltip("How loud this track sits against the music slider")]
        [Range(0f, 2f)]
        [SerializeField] private float volumeScale = 1f;

        [Tooltip("Skip the fade when nothing was playing at all, useful on the very first scene")]
        [SerializeField] private bool snapWhenSilent = false;

        [Tooltip("Attach the audio logger to the director this scene spawns")]
        [SerializeField] private bool debugLog = false;

        private void Start()
        {
            // Logged because a silent title screen is almost always a setup problem
            // (clip unassigned, no AudioListener in the scene), not a code problem.
            Debug.Log($"[Music] {name} clip={(music != null ? music.name : "<none>")} listener={Object.FindObjectOfType<AudioListener>() != null}");

            if (music == null) return;

            EnsureDirector();
            AudioDirector.PlayLoopClip(music, snapWhenSilent ? 0.05f : fadeTime, volumeScale);
        }

        private void EnsureDirector()
        {
            if (AudioDirector.Instance != null) return;

            var go = new GameObject("Audio");
            DontDestroyOnLoad(go);
            go.AddComponent<AudioDirector>();

            // The logger reads the sources the director owns, so it goes on the
            // same object and one step later than the director itself.
            if (debugLog) go.AddComponent<AudioDebugLog>();
        }
    }
}