using UnityEngine;

namespace DuckKing.Player
{
    /// <summary>
    /// Drives the duck's sprite frame-by-frame from a handful of frame arrays.
    ///
    /// This replaces Animator on purpose: the character only needs four simple
    /// loops, and swapping sprites on a timer is far less setup than building
    /// clips, parameters and transitions for each one.
    ///
    /// States and their priority:
    ///   Quack  - one shot, triggered by the quack key, plays once then returns
    ///   Jump   - while airborne, holds the jump frame (or loop if frames given)
    ///   Walk   - while moving on the ground
    ///   Idle   - everything else
    ///
    /// Only one walk direction is authored; the sprite is flipped for the other.
    /// </summary>
    public class DuckSpriteAnimator : MonoBehaviour
    {
        private enum AnimState
        {
            Idle,
            Walk,
            Jump,
            Quack
        }

        [Header("待机动画")]
        [SerializeField] private Sprite[] idleFrames;
        [SerializeField] private float idleFrameTime = 0.12f;

        [Header("行走动画")]
        [SerializeField] private Sprite[] walkFrames;
        [SerializeField] private float walkFrameTime = 0.08f;

        [Header("跳跃动画")]
        [Tooltip("空中播放的帧。只放一张就是静态姿势，放多张会循环")]
        [SerializeField] private Sprite[] jumpFrames;
        [SerializeField] private float jumpFrameTime = 0.10f;

        [Header("鸭叫动画 (E 键)")]
        [Tooltip("张嘴叫的帧，播放一次后回到其他状态")]
        [SerializeField] private Sprite[] quackFrames;
        [SerializeField] private float quackFrameTime = 0.08f;

        [Tooltip("鸭叫在这只鸭上的触发键")]
        [SerializeField] private KeyCode quackKey = KeyCode.E;

        [Tooltip("鸭叫时播放的音效（可选）")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip quackSound;

        [Header("引用")]
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private SpriteRenderer sprite;

        [Tooltip("留空会自动在父物体上查找，用于判断是否离地")]
        [SerializeField] private DuckController controller;

        [Tooltip("判定为移动的最小速度")]
        [SerializeField] private float moveThreshold = 0.1f;

        private AnimState _state = AnimState.Idle;
        private float _timer;
        private int _index;
        private bool _quackPlaying;
        private float _quackEndTime;

        private void Awake()
        {
            if (body == null) body = GetComponentInParent<Rigidbody2D>();
            if (sprite == null) sprite = GetComponent<SpriteRenderer>();

            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (controller == null) controller = GetComponentInParent<DuckController>();
        }

        private void Update()
        {
            if (sprite == null) return;

            // Quack interrupts everything, but only for its own duration.
            // Fully qualified: this file lives next to the DuckKing.Input namespace,
            // which would otherwise shadow UnityEngine.Input and fail to compile.
            if (UnityEngine.Input.GetKeyDown(quackKey) && CanPlayQuack())
            {
                StartQuack();
            }

            if (_quackPlaying && Time.time >= _quackEndTime)
            {
                _quackPlaying = false;
            }

            if (_quackPlaying)
            {
                PlayFrames(quackFrames, quackFrameTime, AnimState.Quack);
                return;
            }

            float vx = body != null ? body.velocity.x : 0f;

            // Use the controller's ground check when available: velocity alone
            // reports "airborne" at the apex of a jump and flickers on slopes.
            bool airborne = controller != null
                ? !controller.IsGrounded
                : body != null && Mathf.Abs(body.velocity.y) > 0.1f;

            bool walking = Mathf.Abs(vx) > moveThreshold && !airborne;

            if (airborne && HasFrames(jumpFrames))
            {
                PlayFrames(jumpFrames, jumpFrameTime, AnimState.Jump);
            }
            else if (walking && HasFrames(walkFrames))
            {
                PlayFrames(walkFrames, walkFrameTime, AnimState.Walk);
            }
            else if (HasFrames(idleFrames))
            {
                PlayFrames(idleFrames, idleFrameTime, AnimState.Idle);
            }

            // One authored direction; flip for the other.
            if (Mathf.Abs(vx) > moveThreshold)
            {
                sprite.flipX = vx < 0f;
            }
        }

        private bool CanPlayQuack()
        {
            return HasFrames(quackFrames) && !_quackPlaying;
        }

        private void StartQuack()
        {
            _quackPlaying = true;
            _index = 0;
            _timer = 0f;

            // Total duration of one pass through the quack frames.
            float duration = quackFrames.Length * quackFrameTime;
            _quackEndTime = Time.time + duration;

            if (audioSource != null && quackSound != null)
            {
                audioSource.PlayOneShot(quackSound);
            }
        }

        /// <summary>
        /// Advances the timer and writes the current frame.
        /// Restarts from frame 0 whenever the state changes.
        /// </summary>
        private void PlayFrames(Sprite[] frames, float frameTime, AnimState newState)
        {
            if (newState != _state)
            {
                _state = newState;
                _index = 0;
                _timer = 0f;
            }

            if (frameTime <= 0f || frames.Length <= 1)
            {
                sprite.sprite = frames[0];
                return;
            }

            _timer += Time.deltaTime;
            if (_timer >= frameTime)
            {
                _timer -= frameTime;
                _index = (_index + 1) % frames.Length;
            }

            sprite.sprite = frames[Mathf.Clamp(_index, 0, frames.Length - 1)];
        }

        private static bool HasFrames(Sprite[] frames)
        {
            return frames != null && frames.Length > 0 && frames[0] != null;
        }
    }
}