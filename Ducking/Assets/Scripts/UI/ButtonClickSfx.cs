using System.Collections.Generic;
using DuckKing.Audio;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DuckKing.UI
{
    /// <summary>
    /// Plays one click sound for every button in the scene, so nobody has to wire
    /// an OnClick event per button.
    ///
    /// Buttons live in two very different situations. The level's buttons sit on a
    /// canvas that is already there, so a single pass on scene load finds them. The
    /// title screen is the opposite: its canvas is built and its panels are switched
    /// between, all after this object has already hooked what it could see. So the
    /// hook is retried on panel changes and on a timer, and anything missing is
    /// picked up then. A button that already has the listener is skipped by id.
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public class ButtonClickSfx : MonoBehaviour
    {
        public static ButtonClickSfx Instance { get; private set; }

        [Header("Clip")]
        [Tooltip("Short click sound, played on every button press")]
        [SerializeField] private AudioClip clickClip;

        [Header("Volume")]
        [Tooltip("Played on top of the SFX slider, which is how a click is felt more than heard")]
        [Range(0f, 4f)]
        [SerializeField] private float volume = 2.6f;

        [Header("Feel")]
        [Tooltip("Random pitch spread so repeated clicks do not sound identical")]
        [Range(0f, 0.3f)]
        [SerializeField] private float pitchJitter = 0.04f;

        [Tooltip("Skip buttons that sit on inactive panels when the scene loads")]
        [SerializeField] private bool includeInactive = true;

        // Buttons that already have the listener, keyed by instance id. The event is
        // not saved anywhere, so this is what stops it from being added twice when a
        // scene is reloaded or a panel is rebuilt.
        private readonly HashSet<int> _hooked = new HashSet<int>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;

            // Kept alive so the sound still works in StoryScene and SampleScene,
            // neither of which needs its own copy of this object.
            DontDestroyOnLoad(gameObject);

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Start()
        {
            HookScene(SceneManager.GetActiveScene());
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                SceneManager.sceneLoaded -= OnSceneLoaded;
            }
        }

        private void Update()
        {
            // Cheap sweep for buttons that did not exist yet when the scene loaded:
            // a retry costs one hierarchy walk and stops mattering after the first
            // scene that has nothing new in it.
            if (clickClip == null) return;
            HookScene(SceneManager.GetActiveScene());
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            HookScene(scene);
        }

        /// <summary>Attaches the click sound to every button that does not have it yet.</summary>
        private void HookScene(Scene scene)
        {
            if (clickClip == null || !scene.isLoaded) return;

            var roots = scene.GetRootGameObjects();

            for (int i = 0; i < roots.Length; i++)
            {
                // Inactive panels still contain buttons the player will reach later,
                // so the search includes them by default.
                var buttons = roots[i].GetComponentsInChildren<Button>(includeInactive);
                for (int j = 0; j < buttons.Length; j++)
                {
                    Hook(buttons[j]);
                }
            }
        }

        private void Hook(Button button)
        {
            if (button == null) return;

            int id = button.GetInstanceID();
            if (_hooked.Contains(id)) return;

            _hooked.Add(id);
            button.onClick.AddListener(PlayClick);
        }

        /// <summary>Plays the click sound. Public so other screens can call it directly.</summary>
        public void PlayClick()
        {
            AudioDirector.PlaySfxClip(clickClip, volume, pitchJitter);
        }

        /// <summary>
        /// Static entry point for code that wants the click without a reference.
        /// Does nothing when no director exists, which keeps previews quiet.
        /// </summary>
        public static void Play()
        {
            if (Instance != null) Instance.PlayClick();
        }
    }
}