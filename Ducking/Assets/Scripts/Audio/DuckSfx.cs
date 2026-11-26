using DuckKing.Player;
using UnityEngine;

namespace DuckKing.Audio
{
    /// <summary>
    /// Ear candy for one duck: a jump sound when it really jumps, a landing sound
    /// when it hits the ground, and a quack on a key.
    ///
    /// It listens to the duck instead of being called from DuckController, so the
    /// movement code stays free of audio. Missing clips simply play nothing, which
    /// keeps a half-wired scene playable.
    /// </summary>
    [RequireComponent(typeof(DuckController))]
    public class DuckSfx : MonoBehaviour
    {
        [Header("Clips")]
        [SerializeField] private AudioClip jumpClip;
        [SerializeField] private AudioClip landClip;
        [SerializeField] private AudioClip quackClip;

        [Header("Volume")]
        [Range(0f, 2f)]
        [SerializeField] private float jumpVolume = 0.3f;
        [Range(0f, 2f)]
        [SerializeField] private float landVolume = 0.5f;
        [Range(0f, 2f)]
        [SerializeField] private float quackVolume = 0.7f;

        [Header("Feel")]
        [Tooltip("Random pitch spread so repeated sounds do not feel mechanical")]
        [Range(0f, 0.3f)]
        [SerializeField] private float pitchJitter = 0.06f;

        [Tooltip("Falling faster than this makes the landing sound louder")]
        [SerializeField] private float hardLandingSpeed = 12f;

        [Header("Quack")]
        [Tooltip("Key that makes this duck quack. Set a different one per duck.")]
        [SerializeField] private KeyCode quackKey = KeyCode.None;

        [Tooltip("Minimum seconds between two quacks from the same duck")]
        [SerializeField] private float quackCooldown = 0.25f;

        private DuckController _duck;

        private bool _wasGrounded = true;
        private float _lastFallSpeed;
        private float _nextQuackTime;

        // Time stamp of the jump that already has its sound, so a jump that spans
        // more than one rendered frame still plays exactly once.
        private float _lastJumpPlayed = -1f;

        private void Awake()
        {
            _duck = GetComponent<DuckController>();
        }

        private void Update()
        {
            if (_duck == null) return;

            bool grounded = _duck.IsGrounded;

            // Landing: airborne last frame, touching ground now.
            if (grounded && !_wasGrounded && _lastFallSpeed > 2f)
            {
                float scale = landVolume * Mathf.Clamp01(_lastFallSpeed / Mathf.Max(1f, hardLandingSpeed));
                AudioDirector.PlaySfxClip(landClip, Mathf.Max(0.15f, scale), pitchJitter);
            }

            if (!grounded)
            {
                // Track the fall speed so the landing knows how hard it was.
                Vector2 v = _duck.BodyVelocity;
                if (v.y < 0f) _lastFallSpeed = Mathf.Abs(v.y);
            }
            else
            {
                _lastFallSpeed = 0f;
            }

            _wasGrounded = grounded;

            // Jump: the controller raises this on the step a jump actually launched,
            // so a buffered key press that failed to fire stays silent.
            //
            // JumpedThisFrame is set inside FixedUpdate and stays true until the next
            // physics step, which may be several rendered frames away. Update runs once
            // per rendered frame, so a slow frame would play the clip twice. The stamp
            // makes one jump produce exactly one sound.
            if (_duck.JumpedThisFrame && _duck.LastJumpTime != _lastJumpPlayed)
            {
                _lastJumpPlayed = _duck.LastJumpTime;
                AudioDirector.PlaySfxClip(jumpClip, jumpVolume, pitchJitter);
            }

            if (quackKey != KeyCode.None
                && UnityEngine.Input.GetKeyDown(quackKey)
                && Time.unscaledTime >= _nextQuackTime)
            {
                _nextQuackTime = Time.unscaledTime + quackCooldown;
                PlayQuack();
            }
        }

        /// <summary>Plays the quack clip once. Used by the ending, which calls it per duck.</summary>
        public void PlayQuack()
        {
            AudioDirector.PlaySfxClip(quackClip, quackVolume, pitchJitter);
        }

        /// <summary>
        /// Click sound for a pause or settings panel that lives in a scene with no
        /// ButtonClickSfx of its own. Wired through a Button's OnClick list.
        /// </summary>
        public void PlayUiClick()
        {
            AudioDirector.PlaySfxClip(jumpClip != null ? jumpClip : quackClip, 0.5f, pitchJitter);
        }
    }
}