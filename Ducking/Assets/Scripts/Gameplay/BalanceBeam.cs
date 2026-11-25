using System.Collections.Generic;
using DuckKing.Player;
using UnityEngine;

namespace DuckKing.Gameplay
{
    /// <summary>
    /// A see-saw plank that pivots on a fixed fulcrum, like the balance beams in
    /// Fireboy and Watergirl.
    ///
    /// The plank is a genuine physics body: a Dynamic Rigidbody2D pinned in place by a
    /// HingeJoint2D. Tipping, inertia and the way a duck's weight pulls one end down are
    /// all produced by the solver, which is why this version cannot jitter the way a
    /// scripted plank did.
    ///
    /// The script's only job is bookkeeping. Physics does not tell a duck that it is
    /// standing on a see-saw, so the plank reports boarding and leaving, and the duck
    /// controller uses that to express its movement along the tilted surface and to hand
    /// its vertical axis to the engine while it is up there.
    ///
    /// Deliberately NOT done here:
    ///  - the duck is not parented to the plank. Rigidbody2D simulation runs in world
    ///    space and ignores the transform hierarchy, so parenting changes nothing
    ///    physically and only breaks the duck's own world-space velocity
    ///  - no velocity is written to anybody. Every write here would be undone by
    ///    DuckController the following step, which is what made the scripted plank
    ///    buzz continuously at the edges
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(HingeJoint2D))]
    public class BalanceBeam : MonoBehaviour
    {
        [Header("Shape")]
        [Tooltip("Half the plank length, in world units. Match this to the collider.")]
        [SerializeField] private float halfLength = 1.9f;

        [Tooltip("Plank thickness, in world units. Match this to the collider.")]
        [SerializeField] private float plankThickness = 0.4f;

        [Tooltip("How far above the plank surface a duck's feet may rest and still count " +
                 "as standing on it")]
        [SerializeField] private float boardTolerance = 0.25f;

        [Header("Balance")]
        [Tooltip("Steepest the plank may tilt, in degrees away from level")]
        [Range(0f, 89f)]
        [SerializeField] private float maxAngle = 18f;

        [Tooltip("Mass of the plank. Heavier planks resist tipping, which makes them " +
                 "easier to stand on and calmer to use.")]
        [SerializeField] private float plankMass = 6f;

        [Tooltip("How strongly world gravity pulls the plank. The plank needs its own " +
                 "gravity because a see-saw must tip under load even in a scene with no " +
                 "global gravity.")]
        [SerializeField] private float plankGravityScale = 3f;

        [Header("Debug")]
        [Tooltip("Log boarding, leaving and the plank angle every step")]
        [SerializeField] private bool debugLog;

        private Rigidbody2D _rb;
        private HingeJoint2D _hinge;

        /// <summary>Ducks currently standing on the plank.</summary>
        private readonly List<DuckController> _riders = new List<DuckController>();

        /// <summary>Scratch buffer so detection does not allocate every step.</summary>
        private readonly List<DuckController> _detected = new List<DuckController>();

        private float _logTimer;

        /// <summary>
        /// Current tilt in degrees. Positive means the right end is down.
        ///
        /// Unity stores rotation as an angle around Z, which is positive counter-clockwise,
        /// so a clockwise tilt (right end down) is a negative Z rotation. The sign is
        /// flipped here so the value reads the way it is drawn.
        /// </summary>
        public float Angle
        {
            get
            {
                float z = transform.eulerAngles.z;
                if (z > 180f) z -= 360f;
                return -z;
            }
        }

        /// <summary>How many ducks are currently standing on the plank.</summary>
        public int RiderCount => _riders.Count;

        /// <summary>Plank length in world units.</summary>
        public float Length => halfLength * 2f;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _hinge = GetComponent<HingeJoint2D>();

            ConfigureBody();
            ConfigureHinge();
        }

        /// <summary>
        /// Puts the body into the one state that behaves like a see-saw: dynamic, with
        /// gravity, and interpolation on so the tilt looks smooth at any frame rate.
        ///
        /// These are forced here rather than left to the Inspector because a plank
        /// created by hand usually inherits whatever the last attempt left behind, and a
        /// kinematic or gravity-less plank simply does not move when a duck stands on it.
        /// </summary>
        private void ConfigureBody()
        {
            _rb.bodyType = RigidbodyType2D.Dynamic;
            _rb.simulated = true;
            _rb.gravityScale = plankGravityScale;
            _rb.mass = Mathf.Max(0.5f, plankMass);
            _rb.drag = 0.4f;

            // Angular drag is the whole difference between a see-saw that swings once and
            // settles, and one that keeps oscillating. The limit clamp alone lets the
            // plank bounce off its stops forever.
            _rb.angularDrag = 4f;

            _rb.freezeRotation = false;
            _rb.constraints = RigidbodyConstraints2D.None;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        }

        /// <summary>
        /// Anchors the hinge to a fixed point in the world at the plank's origin.
        ///
        /// connectedBody stays null on purpose: a 2D hinge with no connected body is
        /// pinned to a point in space, which is exactly a fulcrum. Connecting it to
        /// anything else lets the plank be dragged around instead of just tilted.
        ///
        /// The joint limits are deliberately left off. They are measured against a
        /// connected body, so on a world-anchored hinge they do nothing at all; ClampTilt
        /// enforces the limit instead.
        /// </summary>
        private void ConfigureHinge()
        {
            _hinge.connectedBody = null;
            _hinge.autoConfigureConnectedAnchor = false;
            _hinge.anchor = Vector2.zero;
            _hinge.connectedAnchor = transform.position;

            _hinge.useLimits = false;
            _hinge.useMotor = false;
        }

        private void Start()
        {
            var col = GetComponent<Collider2D>();
            if (col == null)
            {
                Debug.LogWarning($"[BalanceBeam] {name} has no Collider2D, so ducks will " +
                                 "fall straight through it. Add a BoxCollider2D sized to " +
                                 "the plank.", this);
            }
            else if (col.isTrigger)
            {
                Debug.LogWarning($"[BalanceBeam] {name} has Is Trigger on, so ducks will " +
                                 "fall straight through it. Turn Is Trigger off.", this);
            }
        }

        private void FixedUpdate()
        {
            ClampTilt();
            DetectRiders();
            PublishRiders();

            if (!debugLog) return;

            // Logged at a readable rate instead of every step: the fixed step runs at
            // 50 Hz and a full drop of log lines is unreadable while playing.
            _logTimer += Time.fixedDeltaTime;
            if (_logTimer < 0.2f) return;
            _logTimer = 0f;

            // Deg, not rad, and named as such so a limit violation is obvious in the log.
            Debug.Log($"[BalanceBeam] {name} riders={_riders.Count} angle={Angle:F2} " +
                      $"maxAngle={maxAngle:F0} angVel={_rb.angularVelocity:F2}", this);
        }

        /// <summary>
        /// Holds the plank between its tilt limits.
        ///
        /// HingeJoint2D.limits are NOT used for this. A 2D hinge with no connectedBody is
        /// pinned to a point in world space, and in that form Unity ignores the angle
        /// limits entirely: the plank was measured at 71 degrees against an 18 degree
        /// limit while the joint still reported the limits as set. Clamping the body
        /// directly is the only reliable way to stop it.
        ///
        /// The angle is clamped first and the angular velocity is zeroed only when a
        /// limit is actually reached, so the plank still swings freely in between.
        /// </summary>
        private void ClampTilt()
        {
            if (_rb == null) return;

            float angle = Angle;

            if (angle > maxAngle)
            {
                SetTilt(maxAngle);

                // Stop the outward swing, but let it start falling back. Zeroing the whole
                // angular velocity makes the plank stick to the stop instead of returning.
                if (_rb.angularVelocity > 0f) _rb.angularVelocity = 0f;
            }
            else if (angle < -maxAngle)
            {
                SetTilt(-maxAngle);

                if (_rb.angularVelocity < 0f) _rb.angularVelocity = 0f;
            }

            // Safety net for a plank thrown hard enough to tunnel past the check above in
            // a single step: a fast plank is unplayable, so the spin is capped regardless.
            const float maxSpin = 220f;
            if (_rb.angularVelocity > maxSpin) _rb.angularVelocity = maxSpin;
            else if (_rb.angularVelocity < -maxSpin) _rb.angularVelocity = -maxSpin;
        }

        /// <summary>
        /// Sets the plank's tilt in degrees, keeping its position fixed.
        /// Positive angles put the right end down, matching the Angle property.
        /// </summary>
        private void SetTilt(float degrees)
        {
            _rb.MoveRotation(-degrees);
        }

        /// <summary>
        /// Works out which ducks are standing on the plank this step.
        ///
        /// Detection is geometric rather than collision-callback based. A callback only
        /// fires when a duck crosses the boundary, so a duck that walks along the plank
        /// without ever leaving it would never fire one again and the plank would lose
        /// track of who is on it.
        /// </summary>
        private void DetectRiders()
        {
            _detected.Clear();

            DuckController[] all = FindObjectsOfType<DuckController>();
            for (int i = 0; i < all.Length; i++)
            {
                DuckController duck = all[i];
                if (duck == null || !duck.isActiveAndEnabled) continue;
                if (!IsStandingOnPlank(duck)) continue;

                _detected.Add(duck);
            }

            // Boarded: present now, absent last step.
            for (int i = 0; i < _detected.Count; i++)
            {
                DuckController duck = _detected[i];
                if (_riders.Contains(duck)) continue;

                _riders.Add(duck);
                duck.OnBoardPlank(transform);

                if (debugLog) Debug.Log($"[BalanceBeam] {duck.name} boarded {name}", this);
            }

            // Left: present last step, absent now.
            for (int i = _riders.Count - 1; i >= 0; i--)
            {
                DuckController duck = _riders[i];

                if (duck == null)
                {
                    _riders.RemoveAt(i);
                    continue;
                }

                if (_detected.Contains(duck)) continue;

                _riders.RemoveAt(i);
                duck.OnLeavePlank(duck.GetComponent<Rigidbody2D>() != null
                    ? duck.GetComponent<Rigidbody2D>().velocity
                    : Vector2.zero);

                if (debugLog) Debug.Log($"[BalanceBeam] {duck.name} left {name}", this);
            }
        }

        /// <summary>
        /// True when a duck's feet are within the plank's footprint, measured in the
        /// plank's own space so the test keeps working while the plank is tilted.
        /// </summary>
        private bool IsStandingOnPlank(DuckController duck)
        {
            var col = duck.GetComponent<Collider2D>();
            if (col == null) return false;

            Bounds b = col.bounds;

            // Feet: the bottom centre of the duck's collider.
            Vector2 feetWorld = new Vector2(b.center.x, b.min.y);

            // Expressed in plank space, where "along" and "above" do not change as the
            // plank rotates. Doing this in world space was one of the sources of the old
            // version's flicker: a tilted plank's bounds move while the duck does not.
            Vector2 feetLocal = transform.InverseTransformPoint(feetWorld);

            // Must be above the plank's middle line, but not floating high over it.
            if (feetLocal.y < -0.15f) return false;
            if (feetLocal.y > plankThickness * 0.5f + boardTolerance) return false;

            // Must be over the plank's span, with a small margin so a duck standing on the
            // very end still counts.
            const float edgeMargin = 0.2f;
            if (feetLocal.x < -halfLength - edgeMargin) return false;
            if (feetLocal.x > halfLength + edgeMargin) return false;

            return true;
        }

        private void PublishRiders()
        {
            // Remove destroyed entries so the list cannot grow stale between steps.
            for (int i = _riders.Count - 1; i >= 0; i--)
            {
                if (_riders[i] == null) _riders.RemoveAt(i);
            }
        }

        private void OnValidate()
        {
            halfLength = Mathf.Max(0.2f, halfLength);
            plankThickness = Mathf.Max(0.05f, plankThickness);
            plankMass = Mathf.Max(0.5f, plankMass);
            boardTolerance = Mathf.Max(0.02f, boardTolerance);

            if (Application.isPlaying) return;

            // Only the mass is mirrored into the Inspector; the tilt limit is enforced in
            // ClampTilt, because the joint ignores its own limits on a world-anchored
            // hinge and setting them here would give a false impression that they work.
            var body = GetComponent<Rigidbody2D>();
            if (body != null) body.mass = plankMass;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = Color.cyan;

            Vector3 axis = transform.rotation * Vector3.right;
            Gizmos.DrawLine(transform.position - axis * halfLength,
                            transform.position + axis * halfLength);

            Gizmos.DrawWireSphere(transform.position, 0.06f);
        }
    }
}