using System;
using System.Collections.Generic;
using DuckKing.Player;
using UnityEngine;

namespace DuckKing.Gameplay
{
    /// <summary>
    /// Scene level game manager: registers both ducks, tracks coins, decides the outcome.
    /// Singleton so small objects such as coins can report without dragging references around.
    /// </summary>
    public class SceneGameManager : MonoBehaviour
    {
        public static SceneGameManager Instance { get; private set; }

        [Header("Ducks (order = player 1, player 2)")]
        [SerializeField] private List<DuckController> ducks = new List<DuckController>(2);

        [Header("Finish")]
        [Tooltip("Any duck reaching this height finishes the run")]
        [SerializeField] private float finishHeight = 120f;

        [Tooltip("Allow the height rule when no finish trigger was reached")]
        [SerializeField] private bool useHeightFallback = true;

        private readonly int[] _scores = new int[2];
        private bool _finished;

        [Header("Timer")]
        [Tooltip("Start counting as soon as the scene loads")]
        [SerializeField] private bool startTimerOnLoad = true;

        [Tooltip("Wait for any key press before the run begins. The ducks stay frozen " +
                 "and the clock sits at zero until then.")]
        [SerializeField] private bool waitForAnyKeyToStart = true;

        [Tooltip("Keys ignored by the start gate, so the debug probe never starts a run")]
        [SerializeField] private KeyCode[] ignoredStartKeys = { KeyCode.F9, KeyCode.F1, KeyCode.F2 };

        [Tooltip("Optional Text shown while waiting for the start key")]
        [SerializeField] private string waitingMessage = "PRESS ANY KEY";

        [Tooltip("Seconds the waiting message stays after the run starts, as feedback")]
        [SerializeField] private float startFlashTime = 0.6f;

        private float _elapsedSeconds;
        private bool _timerRunning;
        private bool _waitingForStart;
        private bool _started;

        /// <summary>True until the player presses a key to begin the run.</summary>
        public bool IsWaitingForStart => _waitingForStart;

        /// <summary>True once the run has begun.</summary>
        public bool HasStarted => _started;

        /// <summary>Raised when the run actually begins, after the start key.</summary>
        public event Action OnRunStarted;

        /// <summary>Seconds the run has lasted. Frozen once the run finishes.</summary>
        public float ElapsedSeconds => _elapsedSeconds;

        /// <summary>Time spent on the climb, formatted mm:ss.cc for the HUD.</summary>
        public string ElapsedDisplay
        {
            get
            {
                float t = Mathf.Max(0f, _elapsedSeconds);
                int minutes = (int)(t / 60f);
                int seconds = (int)(t % 60f);
                int hundredths = (int)((t * 100f) % 100f);
                return $"{minutes:00}:{seconds:00}.{hundredths:00}";
            }
        }

        /// <summary>Which side claims the crown.</summary>
        public enum Outcome
        {
            None,
            PlayerA,
            PlayerB,
            Shared
        }

        public IReadOnlyList<DuckController> Ducks => ducks;
        public int ScoreA => _scores[0];
        public int ScoreB => _scores[1];

        /// <summary>Last resolved outcome, valid once IsFinished is true.</summary>
        public Outcome Result { get; private set; } = Outcome.None;

        public bool IsFinished => _finished;

        /// <summary>Raised as (playerIndex, newTotal) whenever a duck gains coins.</summary>
        public event Action<int, int> OnScoreChanged;

        /// <summary>Raised once when the run ends, carrying the resolved outcome.</summary>
        public event Action<Outcome> OnFinished;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            if (waitForAnyKeyToStart)
            {
                // Freeze at Start rather than Awake: the ducks need their own Awake to
                // have run before this can reach them.
                _waitingForStart = true;
                _timerRunning = false;
                SetDuckInputEnabled(false);
                return;
            }

            BeginRun();
        }

        /// <summary>
        /// Opens the start gate: hands input to the ducks and starts the clock.
        /// Safe to call more than once, so a stray trigger cannot restart the timer.
        /// </summary>
        public void BeginRun()
        {
            if (_started || _finished) return;

            _waitingForStart = false;
            _started = true;

            SetDuckInputEnabled(true);

            if (startTimerOnLoad) _timerRunning = true;

            OnRunStarted?.Invoke();
        }

        private void SetDuckInputEnabled(bool enabled)
        {
            for (int i = 0; i < ducks.Count; i++)
            {
                if (ducks[i] != null) ducks[i].InputEnabled = enabled;
            }
        }

        /// <summary>True on the frame any key other than the debug keys went down.</summary>
        private bool AnyKeyPressed()
        {
            // Input.anyKeyDown covers mouse buttons too, which is what "any key" means
            // to a player, but the ignored list has to be checked per key first.
            for (int i = 0; i < ignoredStartKeys.Length; i++)
            {
                if (UnityEngine.Input.GetKeyDown(ignoredStartKeys[i])) return false;
            }

            return UnityEngine.Input.anyKeyDown;
        }

        /// <summary>Begins or resumes the run timer.</summary>
        public void StartTimer()
        {
            if (_finished) return;
            if (_waitingForStart) return;
            _timerRunning = true;
        }

        /// <summary>Pauses the run timer without ending the run.</summary>
        public void PauseTimer()
        {
            _timerRunning = false;
        }

        /// <summary>Clears the clock back to zero, for a restart without a scene reload.</summary>
        public void ResetTimer()
        {
            _elapsedSeconds = 0f;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            if (_waitingForStart)
            {
                if (!AnyKeyPressed()) return;

                BeginRun();

                // Fall through so the start frame does not skip the clock.
            }

            // The timer is the only HUD element now, so it drives the scene clock.
            // Unscaled time keeps it honest: result screens drop timeScale to zero
            // and the count must stop there rather than silently drifting.
            if (_timerRunning && !_finished)
            {
                _elapsedSeconds += Time.unscaledDeltaTime;
            }

            if (_finished || !useHeightFallback) return;
            if (ducks.Count < 2 || ducks[0] == null || ducks[1] == null) return;

            if (ducks[0].Height >= finishHeight || ducks[1].Height >= finishHeight)
            {
                Finish();
            }
        }

        public void RegisterDuck(DuckController duck)
        {
            if (duck == null || ducks.Contains(duck)) return;
            if (ducks.Count < 2) ducks.Add(duck);
        }

        public int GetDuckIndex(DuckController duck)
        {
            return ducks.IndexOf(duck);
        }

        public void AddScore(int playerIndex, int amount)
        {
            if (playerIndex < 0 || playerIndex > 1) return;

            _scores[playerIndex] += amount;
            OnScoreChanged?.Invoke(playerIndex, _scores[playerIndex]);
        }

        /// <summary>
        /// Triggers the ending.
        /// Rule: whoever collected more coins claims the crown. A tie resolves to
        /// a shared rule, which is the second ending.
        /// </summary>
        public void Finish()
        {
            if (_finished) return;
            _finished = true;
            _timerRunning = false;

            // Freeze the ducks on the result screen; the finish trigger and the
            // controls otherwise keep acting behind the panel.
            SetDuckInputEnabled(false);

            if (_scores[0] > _scores[1]) Result = Outcome.PlayerA;
            else if (_scores[1] > _scores[0]) Result = Outcome.PlayerB;
            else Result = Outcome.Shared;

            Debug.Log($"[DuckKing] Finish. P1={_scores[0]} P2={_scores[1]} -> {Result}");
            OnFinished?.Invoke(Result);
        }
    }
}