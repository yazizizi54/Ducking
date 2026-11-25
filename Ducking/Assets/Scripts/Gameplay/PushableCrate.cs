using DuckKing.Player;
using UnityEngine;

namespace DuckKing.Gameplay
{
    /// <summary>
    /// A crate the ducks can shove around.
    ///
    /// The ducks drive themselves with Rigidbody2D.velocity, which would overwrite
    /// any impulse the physics engine applies. So instead of taking a push, this
    /// crate watches for a duck touching it and carries that duck's speed over to
    /// itself, then lets friction slow it down again.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class PushableCrate : MonoBehaviour
    {
        [Header("Movement")]
        [Tooltip("Share of the duck's speed the crate picks up. Below 1 the crate " +
                 "trails behind the duck instead of matching it exactly.")]
        [Range(0f, 1f)]
        [SerializeField] private float pushTransfer = 0.85f;

        [Tooltip("Fastest the crate can be pushed, in units per second")]
        [SerializeField] private float maxPushSpeed = 4.5f;

        [Tooltip("How quickly the crate stops once nothing is pushing it")]
        [SerializeField] private float friction = 12f;

        [Header("Behaviour")]
        [Tooltip("Minimum duck speed before a touch counts as a push")]
        [SerializeField] private float minPushSpeed = 0.3f;

        [Tooltip("Log what is pushing the crate, for tuning")]
        [SerializeField] private bool debugLog;

        private Rigidbody2D _rb;
        private float _pushVelocity;
        private int _pushFrames;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();

            // Gravity stays on so the crate falls, but it must never tip over or
            // roll, which is what makes a box feel like a box in a platformer.
            _rb.freezeRotation = true;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
        }

        private void FixedUpdate()
        {
            if (_pushFrames > 0)
            {
                _pushFrames--;
            }
            else
            {
                // Nothing touched the crate this frame, so bleed the speed away.
                _pushVelocity = Mathf.MoveTowards(_pushVelocity, 0f, friction * Time.fixedDeltaTime);
            }

            _rb.velocity = new Vector2(_pushVelocity, _rb.velocity.y);
        }

        private void OnCollisionStay2D(Collision2D collision)
        {
            var duck = collision.collider.GetComponent<DuckController>();
            if (duck == null) return;

            // Only a sideways shove counts. Landing on top must not launch the crate.
            Vector2 normal = collision.GetContact(0).normal;

            // The normal points from the crate toward the duck, so a duck standing on
            // top gives a normal close to straight up.
            if (Mathf.Abs(normal.y) > 0.7f) return;

            var duckBody = collision.collider.attachedRigidbody;
            if (duckBody == null) return;

            float duckSpeed = duckBody.velocity.x;
            if (Mathf.Abs(duckSpeed) < minPushSpeed) return;

            // Ignore a duck pushing away from the crate.
            float side = Mathf.Sign(normal.x);
            if (Mathf.Sign(duckSpeed) != side) return;

            float target = Mathf.Clamp(duckSpeed * pushTransfer, -maxPushSpeed, maxPushSpeed);
            _pushVelocity = target;
            _pushFrames = 2;

            if (debugLog)
            {
                Debug.Log($"[Crate] {name} pushed by {duck.name} at {duckSpeed:F2} -> {target:F2}", this);
            }
        }
    }
}