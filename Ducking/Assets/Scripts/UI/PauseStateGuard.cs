using UnityEngine;
using UnityEngine.UI;

namespace DuckKing.UI
{
    /// <summary>
    /// Watches the pause state of the level and makes sure time never stays frozen
    /// once nothing is on screen asking for it.
    ///
    /// The level hands out several ways to freeze itself: a settings panel, a pause
    /// menu, a result screen. Each one can take its own panel down without knowing
    /// another one still owns the freeze, and any one of them can forget to give
    /// timeScale back when the panel is closed by hand in the hierarchy or by a
    /// script. Time.timeScale is global, so a single missed release leaves the
    /// whole game stuck while every future pause looks like it works.
    ///
    /// This owns the decision instead: if nothing is visibly paused, time runs. A
    /// script that wants to freeze supplies a reason. Closing the last reason always
    /// restores the level, whatever route the player took out of it.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class PauseStateGuard : MonoBehaviour
    {
        public static PauseStateGuard Instance { get; private set; }

        [Header("Treat these as a paused level")]
        [Tooltip("Panels that freeze the game while they are visible. Assign the level's " +
                 "settings panel and pause menu here.")]
        [SerializeField] private GameObject[] pausePanels;

        [Tooltip("Levels, and every object inside them, ignored by the scan above")]
        [SerializeField] private GameObject[] ignorePanels;

        [Tooltip("Also scan for panels by name, for scenes that were not wired by hand")]
        [SerializeField] private bool scanByName = true;

        [Tooltip("Names that count as a pause panel when scanning (case-insensitive " +
                 "substring). Leave as is unless the panels get renamed.")]
        [SerializeField] private string[] pauseNames = { "settingspanel", "pausemenu" };

        [Tooltip("Print one line on every pause and resume, for tracking down a stuck level")]
        [SerializeField] private bool debugLog = false;

        private float _timeScaleBeforePause = 1f;
        private bool _paused;
        private float _logTimer;
        private GameObject[] _cachedPanels;

        /// <summary>True while anything is holding the game frozen.</summary>
        public bool IsPaused => _paused;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;

                // Leaving a level mid pause must not carry timeScale 0 into the next
                // scene, where nothing would be able to release it.
                if (_paused) RestoreTime();
            }
        }

        private void Update()
        {
            bool shouldPause = AnyPanelVisible();

            if (shouldPause && !_paused) Pause();
            else if (!shouldPause && _paused) RestoreTime();

            if (!debugLog) return;

            _logTimer += Time.unscaledDeltaTime;
            if (_logTimer < 2f) return;
            _logTimer = 0f;
            Debug.Log($"[Pause] timeScale={Time.timeScale:F2} paused={_paused} " +
                      $"visible={DescribeVisiblePanels()}");
        }

        /// <summary>
        /// Freezes the game from code, for a script that owns its own panel. Pair it
        /// with Release, or with a panel the scan already knows about.
        /// </summary>
        public void RequestPause()
        {
            if (_paused) return;
            Pause();
        }

        /// <summary>Lets the game run again if no panel is visible any more.</summary>
        public void ReleasePause()
        {
            if (!_paused) return;
            if (AnyPanelVisible()) return;
            RestoreTime();
        }

        // ---- internals --------------------------------------------------------

        private void Pause()
        {
            _paused = true;

            // Remember what the level was running at, but never remember a zero:
            // that would make the next resume restore the pause itself.
            _timeScaleBeforePause = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;

            if (debugLog)
            {
                Debug.Log($"[Pause] frozen, will resume at {_timeScaleBeforePause:F2}. " +
                          $"visible={DescribeVisiblePanels()}");
            }
        }

        private void RestoreTime()
        {
            _paused = false;
            Time.timeScale = _timeScaleBeforePause > 0f ? _timeScaleBeforePause : 1f;

            if (debugLog) Debug.Log($"[Pause] resumed at {Time.timeScale:F2}.");
        }

        private bool AnyPanelVisible()
        {
            if (pausePanels != null)
            {
                for (int i = 0; i < pausePanels.Length; i++)
                {
                    GameObject panel = pausePanels[i];
                    if (panel != null && panel.activeInHierarchy) return true;
                }
            }

            if (!scanByName) return false;

            if (_cachedPanels == null || !AnyCachedPanelAlive())
            {
                _cachedPanels = BuildScanList();
            }

            for (int i = 0; i < _cachedPanels.Length; i++)
            {
                GameObject panel = _cachedPanels[i];
                if (panel != null && panel.activeInHierarchy) return true;
            }

            return false;
        }

        private string DescribeVisiblePanels()
        {
            if (_cachedPanels == null) return "none";
            string list = string.Empty;
            for (int i = 0; i < _cachedPanels.Length; i++)
            {
                GameObject panel = _cachedPanels[i];
                if (panel == null || !panel.activeInHierarchy) continue;
                list += panel.name + "|";
            }
            return string.IsNullOrEmpty(list) ? "none" : list;
        }

        private bool AnyCachedPanelAlive()
        {
            for (int i = 0; i < _cachedPanels.Length; i++)
            {
                if (_cachedPanels[i] != null) return true;
            }
            return false;
        }

        /// <summary>
        /// Collects the panels that freeze the game by name. The scan runs once per
        /// level and only matches objects whose own name fits, not their children, so
        /// a slider inside the settings panel is never mistaken for the panel itself.
        /// </summary>
        private GameObject[] BuildScanList()
        {
            var found = new System.Collections.Generic.List<GameObject>();
            var transforms = FindObjectsOfType<Transform>(true);

            for (int i = 0; i < transforms.Length; i++)
            {
                Transform candidate = transforms[i];
                if (!MatchesPauseName(candidate.name)) continue;
                if (IsIgnored(candidate)) continue;

                // A "SettingsPanel" inside another "SettingsPanel" is a child of the
                // real one; the parent already covers it.
                Transform parent = candidate.parent;
                bool nestedInPausePanel = false;
                while (parent != null)
                {
                    if (MatchesPauseName(parent.name))
                    {
                        nestedInPausePanel = true;
                        break;
                    }
                    parent = parent.parent;
                }
                if (nestedInPausePanel) continue;

                found.Add(candidate.gameObject);
            }

            if (debugLog)
            {
                Debug.Log($"[Pause] tracking {found.Count} pause panel(s).");
            }

            return found.ToArray();
        }

        private bool MatchesPauseName(string objectName)
        {
            if (string.IsNullOrEmpty(objectName) || pauseNames == null) return false;

            string lower = objectName.ToLowerInvariant();
            for (int i = 0; i < pauseNames.Length; i++)
            {
                string wanted = pauseNames[i];
                if (string.IsNullOrEmpty(wanted)) continue;
                if (lower.Contains(wanted.ToLowerInvariant())) return true;
            }
            return false;
        }

        private bool IsIgnored(Transform candidate)
        {
            if (ignorePanels == null || ignorePanels.Length == 0) return false;

            for (int i = 0; i < ignorePanels.Length; i++)
            {
                GameObject ignored = ignorePanels[i];
                if (ignored == null) continue;
                if (candidate == ignored.transform || candidate.IsChildOf(ignored.transform)) return true;
            }
            return false;
        }
    }
}