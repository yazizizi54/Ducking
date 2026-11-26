using System.Collections;
using UnityEngine;

namespace DuckKing.Gameplay
{
    /// <summary>
    /// A stone platform that crumbles after being stood on, then rebuilds itself.
    ///
    /// The whole sequence is driven by a sprite animation: the frames show the stone
    /// cracking and then breaking apart. The collider is switched off at the moment the
    /// animation starts showing pieces, so what the player sees and what the physics does
    /// line up. Switching it off on the first frame would drop the duck while the stone
    /// still looked solid.
    ///
    /// Nothing has to track who is standing on it. Once the collider is off the duck is
    /// already falling, so by the time the platform rebuilds the surface is clear by
    /// definition. The rebuild steps are kept to a single coroutine for that reason.
    /// </summary>
    /// <remarks>
    /// BoxCollider2D is required rather than the Collider2D base type on purpose:
    /// RequireComponent cannot instantiate an abstract type, and asking for Collider2D
    /// makes Unity refuse to add the component at all.
    /// </remarks>
    [RequireComponent(typeof(SpriteRenderer), typeof(BoxCollider2D))]
    public class CrumblePlatform : MonoBehaviour
    {
        [Header("动画帧")]
        [Tooltip("序列帧，按顺序播放。碎块出现的那一帧由下面的 Break Frame Index 指定")]
        [SerializeField] private Sprite[] frames;

        [Tooltip("从第一帧播到碎块出现所用秒数")]
        [SerializeField] private float crackTime = 0.6f;

        [Tooltip("从碎块出现播到最后一帧所用秒数")]
        [SerializeField] private float shatterTime = 0.3f;

        [Tooltip("第几帧开始掉碎块（从 0 开始数）。这一帧碰撞体被关闭")]
        [SerializeField] private int breakFrameIndex = 8;

        [Header("恢复")]
        [Tooltip("碎掉之后过多久重新长回来")]
        [SerializeField] private float respawnDelay = 2.5f;

        [Tooltip("恢复的淡入时长")]
        [SerializeField] private float respawnFade = 0.25f;

        [Header("抖动")]
        [Tooltip("开裂期间平台左右抖动的幅度，0 为不抖")]
        [SerializeField] private float shakeAmplitude = 0.04f;

        [Tooltip("抖动频率")]
        [SerializeField] private float shakeFrequency = 30f;

        [Tooltip("打开后会在 Console 打印触发与恢复日志，排查用")]
        [SerializeField] private bool debugLog = true;

        private SpriteRenderer _sprite;
        private Collider2D _collider;
        private Vector3 _restPosition;
        private Color _restColor;
        private bool _running;

        private void Awake()
        {
            _sprite = GetComponent<SpriteRenderer>();

            // Fetched through the base type so the script still works if the platform is
            // later rebuilt with a different collider shape.
            _collider = GetComponent<Collider2D>();

            _restPosition = transform.localPosition;
            _restColor = _sprite.color;

            if (frames != null && frames.Length > 0) _sprite.sprite = frames[0];

            // Reported once at startup: the two mistakes that make this script look dead
            // are a missing frame list and a collider that cannot receive contacts.
            if (frames == null || frames.Length == 0)
            {
                Debug.LogError($"[Crumble] {name} has no frames assigned.", this);
            }

            if (_collider == null)
            {
                Debug.LogError($"[Crumble] {name} has no 2D collider.", this);
            }
            else if (_collider.isTrigger)
            {
                Debug.LogWarning(
                    $"[Crumble] {name} collider is a trigger, so OnCollisionEnter2D never runs.", this);
            }
        }

        /// <summary>
        /// Detects a duck landing on top.
        ///
        /// The test is geometric rather than contact-normal based. A 2D contact normal is
        /// expressed from this collider's point of view, and its sign flips depending on
        /// which body the engine reports first, which made the naive normal check miss
        /// landings entirely. Comparing the two bounds cannot get that backwards.
        ///
        /// OnCollisionStay2D is used alongside Enter because a duck that is already
        /// resting when the platform rebuilds produces no new Enter event.
        /// </summary>
        private void OnCollisionEnter2D(Collision2D collision)
        {
            OnLanded(collision.collider);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            OnLanded(collision.collider);
        }

        private void OnLanded(Collider2D other)
        {
            if (_running || other == null || _collider == null) return;

            // Only a body pressing down on the top face counts. Walking into the side or
            // jumping up through it must not start the crumble.
            Bounds mine = _collider.bounds;
            Bounds theirs = other.bounds;

            bool above = theirs.center.y > mine.center.y;
            bool overlappingHorizontally = theirs.min.x < mine.max.x && theirs.max.x > mine.min.x;

            if (above && overlappingHorizontally) Trigger();
        }

        /// <summary>
        /// Starts the crumble if it is not already running.
        ///
        /// Safe to call repeatedly: two ducks can land on the same stone and the timer
        /// must not restart, or the platform would outlast both of them.
        /// </summary>
        public void Trigger()
        {
            if (_running) return;

            _running = true;
            if (debugLog) Debug.Log($"[Crumble] {name} triggered", this);
            StartCoroutine(CrumbleRoutine());
        }

        private IEnumerator CrumbleRoutine()
        {
            yield return PlayCrack();

            // The stone is visibly breaking apart from here on, so the duck must fall.
            // The body keeps playing its pieces with the collider off.
            if (_collider != null) _collider.enabled = false;

            yield return PlayShatter();

            transform.localPosition = _restPosition;
            _sprite.color = WithAlpha(_restColor, 0f);

            yield return new WaitForSeconds(respawnDelay);

            yield return FadeIn();

            if (_framesAvailable) _sprite.sprite = frames[0];
            if (_collider != null) _collider.enabled = true;
            transform.localPosition = _restPosition;

            _running = false;
        }

        /// <summary>Plays the cracking frames up to the break, with a shaking offset.</summary>
        private IEnumerator PlayCrack()
        {
            if (!_framesAvailable)
            {
                yield return new WaitForSeconds(crackTime);
                yield break;
            }

            int last = Mathf.Clamp(breakFrameIndex, 0, frames.Length - 1);
            yield return PlayFrames(0, last, crackTime, true);
        }

        /// <summary>Plays the frames from the break to the end. The stone is already gone.</summary>
        private IEnumerator PlayShatter()
        {
            if (!_framesAvailable)
            {
                yield return new WaitForSeconds(shatterTime);
                yield break;
            }

            int start = Mathf.Clamp(breakFrameIndex, 0, frames.Length - 1);
            yield return PlayFrames(start, frames.Length - 1, shatterTime, false);
        }

        private IEnumerator PlayFrames(int from, int to, float duration, bool shake)
        {
            int count = to - from + 1;
            if (count <= 0) yield break;

            float perFrame = duration / count;
            float elapsed = 0f;

            for (int f = from; f <= to; f++)
            {
                _sprite.sprite = frames[f];

                float t = 0f;
                while (t < perFrame)
                {
                    t += Time.deltaTime;

                    if (shake && shakeAmplitude > 0f)
                    {
                        float offset = Mathf.Sin(Time.time * shakeFrequency) * shakeAmplitude;
                        transform.localPosition = _restPosition + new Vector3(offset, 0f, 0f);
                    }

                    yield return null;
                }

                elapsed += perFrame;
            }

            if (shake) transform.localPosition = _restPosition;
        }

        private IEnumerator FadeIn()
        {
            if (respawnFade <= 0f)
            {
                _sprite.color = _restColor;
                yield break;
            }

            float t = 0f;
            while (t < respawnFade)
            {
                t += Time.deltaTime;
                _sprite.color = WithAlpha(_restColor, Mathf.Clamp01(t / respawnFade));
                yield return null;
            }

            _sprite.color = _restColor;
        }

        private bool _framesAvailable => frames != null && frames.Length > 0;

        private static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }
    }
}