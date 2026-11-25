using DuckKing.Input;
using UnityEngine;

namespace DuckKing.Player
{
    [RequireComponent(typeof(Rigidbody2D))]
    public class DuckController : MonoBehaviour
    {
        [Header("身份")]
        [SerializeField] private KeyboardDuckInput.Scheme inputScheme = KeyboardDuckInput.Scheme.PlayerOne;

        [Header("横向移动")]
        [Tooltip("最大水平速度（单位/秒）")]
        [SerializeField] private float moveSpeed = 7f;
        [Tooltip("地面加速度，越大越跟手")]
        [SerializeField] private float groundAccel = 90f;
        [Tooltip("空中加速度，略低于地面")]
        [SerializeField] private float airAccel = 55f;
        [Tooltip("松开方向键时的地面减速")]
        [SerializeField] private float groundDecel = 110f;
        [Tooltip("松开方向键时的空中减速")]
        [SerializeField] private float airDecel = 25f;

        [Header("跳跃")]
        [Tooltip("跳跃高度（单位），越大跳得越高")]
        [SerializeField] private float jumpHeight = 3.2f;
        [Tooltip("上升段到达最高点所需时间，越小跳得越快越脆")]
        [SerializeField] private float jumpRiseTime = 0.32f;
        [Tooltip("下落重力倍率，大于1更利落")]
        [SerializeField] private float fallGravityMultiplier = 2.2f;
        [Tooltip("提前松开跳跃键时，上升速度瞬间衰减的比例")]
        [Range(0f, 1f)]
        [SerializeField] private float jumpCutMultiplier = 0.45f;

        [Header("手感辅助")]
        [Tooltip("土狼时间：离开平台后仍可起跳的宽限秒数")]
        [SerializeField] private float coyoteTime = 0.12f;
        [Tooltip("跳跃缓冲：落地前提前按跳跃仍然生效的秒数")]
        [SerializeField] private float jumpBufferTime = 0.12f;

        [Header("下落缓降 Air Drag")]
        [Tooltip("空中下落时的最大下落速度（单位/秒）。\n" +
                 "设大一些会让鸭子下落更利落，设小一些会有轻微飘浮感。")]
        [SerializeField] private float maxFallSpeed = 12f;

        [Tooltip("下落速度超过上限后，减速到上限的速率（单位/秒²）。\n" +
                 "越小越飘，越大越硬")]
        [SerializeField] private float fallSpeedSoftening = 38f;

        // ---- Accessors over the serialized fields above. ----
        // These clamp to safe ranges and are also what the Inspector-edited
        // values flow through, so gameplay code always sees sane numbers.
        // Jump values are read every frame, so editing them takes effect instantly
        // without restarting Play mode.

        public KeyboardDuckInput.Scheme InputScheme
        {
            get => inputScheme;
            set => inputScheme = value;
        }

        public float MoveSpeed { get => moveSpeed; set => moveSpeed = Mathf.Max(0f, value); }
        public float GroundAccel { get => groundAccel; set => groundAccel = Mathf.Max(1f, value); }
        public float AirAccel { get => airAccel; set => airAccel = Mathf.Max(1f, value); }
        public float GroundDecel { get => groundDecel; set => groundDecel = Mathf.Max(1f, value); }
        public float AirDecel { get => airDecel; set => airDecel = Mathf.Max(1f, value); }

        public float JumpHeight { get => jumpHeight; set => jumpHeight = Mathf.Max(0.1f, value); }
        public float JumpRiseTime { get => jumpRiseTime; set => jumpRiseTime = Mathf.Max(0.05f, value); }
        public float FallGravityMultiplier { get => fallGravityMultiplier; set => fallGravityMultiplier = Mathf.Max(0.1f, value); }
        public float JumpCutMultiplier { get => jumpCutMultiplier; set => jumpCutMultiplier = Mathf.Clamp01(value); }

        public float CoyoteTime { get => coyoteTime; set => coyoteTime = Mathf.Max(0f, value); }
        public float JumpBufferTime { get => jumpBufferTime; set => jumpBufferTime = Mathf.Max(0f, value); }

        public float MaxFallSpeed { get => maxFallSpeed; set => maxFallSpeed = Mathf.Max(MinSensibleFallSpeed, value); }
        public float FallSpeedSoftening { get => fallSpeedSoftening; set => fallSpeedSoftening = Mathf.Max(1f, value); }

        /// <summary>Current horizontal speed, shown in the debug panel.</summary>
        public float CurrentSpeedX => _rb != null ? _rb.velocity.x : 0f;

        /// <summary>Current vertical speed, shown in the debug panel.</summary>
        public float CurrentSpeedY => _rb != null ? _rb.velocity.y : 0f;

        [Header("地面检测")]
        [Tooltip("哪些层算作可站立的地面。默认是全部层，鸭子自身的碰撞体会被代码排除，\n" +
                 "但另一只鸭仍会被判为地面。想让鸭子踩不到对方，就把它们放到单独的层并从掩码中去掉。")]
        [SerializeField] private LayerMask groundLayers = ~0;
        [Tooltip("地面检测点相对脚底的偏移")]
        [SerializeField] private Vector2 groundCheckOffset = new Vector2(0f, -0.02f);
        [Tooltip("地面检测宽度缩放，略小于角色宽度避免卡墙")]
        [Range(0.1f, 1f)]
        [SerializeField] private float groundCheckWidth = 0.85f;

        private Rigidbody2D _rb;

        // Base type on purpose: the duck may be built with a Capsule or a Box
        // collider, and only Collider2D.bounds is shared by every 2D collider.
        // Subtype members like size and offset are unavailable here, so bounds is
        // used for ground detection instead.
        private Collider2D _collider;

        private IDuckInput _inputProvider;

        private bool _isDebugFall;

        private float _moveInput;
        private float _coyoteTimer;
        private float _jumpBufferTimer;
        private bool _isGrounded;
        private bool _jumpCutApplied;
        private float _gravity;
        private float _jumpVelocity;

        private bool _inputEnabled = true;

        /// <summary>本角色当前世界坐标高度，镜头系统依赖此项。</summary>
        public float Height => transform.position.y;

        /// <summary>是否站在地面上。</summary>
        public bool IsGrounded => _isGrounded;

        /// <summary>
        /// Gates every input read. The run does not start until the player presses a
        /// key, so the ducks must stand still until then rather than sliding off the
        /// starting platform while the clock is still at zero.
        /// </summary>
        public bool InputEnabled
        {
            get => _inputEnabled;
            set
            {
                _inputEnabled = value;

                if (!value)
                {
                    // Drop buffered intent so a key pressed before the start does not
                    // fire a jump the moment input opens.
                    _moveInput = 0f;
                    _jumpBufferTimer = 0f;
                    _coyoteTimer = 0f;
                }
            }
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _collider = GetComponent<CapsuleCollider2D>();

            // Fall back to any 2D collider: the ducks are built with a BoxCollider2D
            // in some scenes, and requiring the capsule type silently produced a null
            // collider and therefore "never grounded".
            if (_collider == null) _collider = GetComponent<BoxCollider2D>();
            if (_collider == null) _collider = GetComponent<Collider2D>();

            // Build the input immediately. If any line below throws, Update and
            // FixedUpdate would run with a null provider and flood the console
            // with NullReferenceExceptions every single frame.
            _inputProvider = new KeyboardDuckInput(inputScheme);

            if (_rb == null)
            {
                Debug.LogError($"[Duck] {name} has no Rigidbody2D. Movement disabled.", this);
                enabled = false;
                return;
            }

            if (_collider == null)
            {
                // A duck built with only a Box collider still gets ground detection.
                _collider = GetComponent<BoxCollider2D>();
                if (_collider == null)
                {
                    Debug.LogError($"[Duck] {name} has no 2D collider. Ground detection disabled.", this);
                }
            }

            _rb.gravityScale = 0f;
            _rb.freezeRotation = true;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            // Jump physics are derived from jumpHeight / jumpRiseTime every
            // FixedUpdate, so editing those two numbers in the Inspector while in
            // Play mode takes effect immediately.
            RecalculateJumpPhysics();

            // Fields added after this scene was saved load as zero, so restore
            // sensible fall values instead of letting the duck fall instantly.
            // The lower bound also catches a stray 0.5 left by an inspector drag,
            // which would otherwise make the duck float almost motionless.
            if (maxFallSpeed < MinSensibleFallSpeed) maxFallSpeed = DefaultMaxFallSpeed;
            if (fallSpeedSoftening <= 0.01f) fallSpeedSoftening = DefaultFallSpeedSoftening;

            LogResolvedSettings();
        }

        private const float DefaultMaxFallSpeed = 12f;
        private const float DefaultFallSpeedSoftening = 38f;
        private const float MinSensibleFallSpeed = 3f;

        /// <summary>
        /// Derives gravity and launch velocity from the designer-facing values.
        /// h = v0*t/2  =>  v0 = 2h/t ;  g = 2h/t^2
        /// </summary>
        private void RecalculateJumpPhysics()
        {
            float t = Mathf.Max(0.05f, jumpRiseTime);
            _jumpVelocity = 2f * jumpHeight / t;
            _gravity = 2f * jumpHeight / (t * t);
        }

        private void Update()
        {
            if (!_inputEnabled)
            {
                _moveInput = 0f;
                return;
            }

            _moveInput = _inputProvider.Horizontal;

            if (_inputProvider.JumpPressedThisFrame)
            {
                _jumpBufferTimer = jumpBufferTime;
            }
            else
            {
                _jumpBufferTimer -= Time.deltaTime;
            }

            _coyoteTimer -= Time.deltaTime;

            // Toggle fall diagnostics with F9. Temporary probe for the air drag.
            if (UnityEngine.Input.GetKeyDown(KeyCode.F9))
            {
                _isDebugFall = !_isDebugFall;
                Debug.Log($"[FallDbg] {name} diagnostics {(_isDebugFall ? "ON" : "OFF")}", this);
            }
        }

        private void FixedUpdate()
        {
            RecalculateJumpPhysics();
            UpdateGrounded();

            if (_isGrounded && _rb.velocity.y <= 0f)
            {
                _coyoteTimer = coyoteTime;
            }

            TryJump();
            ApplyHorizontalMovement();
            ApplyVerticalPhysics();
        }

        private void UpdateGrounded()
        {
            if (_collider == null)
            {
                _isGrounded = false;
                return;
            }

            // bounds is world-space and already includes the collider offset and the
            // Transform scale, so it works the same for a capsule or a box without
            // touching subtype-only members like size/offset.
            Bounds b = _collider.bounds;

            float halfWidth = b.extents.x * groundCheckWidth;
            float halfHeight = b.extents.y;

            // Start just below the feet. A cast started exactly at the feet can
            // overlap the duck's own collider, which reported "grounded" forever
            // and stopped air drag from ever running.
            const float skin = 0.02f;
            Vector2 origin = new Vector2(b.center.x, b.center.y - halfHeight - skin)
                             + groundCheckOffset;

            Vector2 castSize = new Vector2(halfWidth * 2f, 0.08f);
            const float castDistance = 0.06f;

            // Filter manually instead of using the layer overload: even one hit on
            // the duck's own collider would make the duck think it is standing.
            var hits = Physics2D.BoxCastAll(origin, castSize, 0f, Vector2.down, castDistance, groundLayers);

            _isGrounded = false;
            foreach (var hit in hits)
            {
                if (hit.collider == null) continue;
                if (hit.collider == _collider) continue;
                if (hit.collider.attachedRigidbody == _rb) continue;

                _isGrounded = true;
                break;
            }
        }

        private void TryJump()
        {
            if (_jumpBufferTimer <= 0f || _coyoteTimer <= 0f) return;

            _rb.velocity = new Vector2(_rb.velocity.x, _jumpVelocity);
            _jumpBufferTimer = 0f;
            _coyoteTimer = 0f;
            _isGrounded = false;
            _jumpCutApplied = false;
        }

        private void ApplyHorizontalMovement()
        {
            float targetSpeed = _moveInput * moveSpeed;
            float currentSpeed = _rb.velocity.x;

            bool isAccelerating = Mathf.Abs(targetSpeed) > 0.01f;
            float accel = isAccelerating
                ? (_isGrounded ? groundAccel : airAccel)
                : (_isGrounded ? groundDecel : airDecel);

            float newSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, accel * Time.fixedDeltaTime);
            _rb.velocity = new Vector2(newSpeed, _rb.velocity.y);
        }

        private void ApplyVerticalPhysics()
        {
            float vy = _rb.velocity.y;

            // 提前松开跳跃键：截断上升速度，实现短跳与长跳。
            if (!_inputProvider.JumpHeld && vy > 0f && !_jumpCutApplied)
            {
                vy *= jumpCutMultiplier;
                _jumpCutApplied = true;
            }

            float g = _gravity;
            if (vy <= 0f)
            {
                g *= fallGravityMultiplier;
            }

            vy -= g * Time.fixedDeltaTime;

            float beforeDrag = vy;
            vy = ApplyAirDrag(vy);

            // Temporary diagnostics: shows the real per-frame fall speed so the cap
            // can be verified against the numbers the engine actually uses.
            if (_isDebugFall)
            {
                Debug.Log(
                    $"[FallDbg] {name} grounded={_isGrounded} " +
                    $"gravity={g:F1} before={beforeDrag:F2} after={vy:F2} cap={-Mathf.Abs(maxFallSpeed):F2}",
                    this);
            }

            _rb.velocity = new Vector2(_rb.velocity.x, vy);
        }

        /// <summary>
        /// Softens the fall so the duck settles instead of plummeting.
        ///
        /// This runs automatically in the air; there is no glide button. Gravity
        /// accelerates the duck freely until the fall speed reaches maxFallSpeed,
        /// after which the speed is held at that cap.
        ///
        /// The cap is hard on purpose. An eased version was tried first and did
        /// nothing: with the project's fall gravity around 258 units/s^2, the
        /// speed overshoots a 12 unit cap within 0.05 s, and a 38 unit/s^2 easing
        /// rate loses ground every frame. A hard clamp is the only thing that
        /// actually bounds the fall. Because the cap is reached within a few
        /// hundredths of a second, the transition is invisible.
        /// </summary>
        private float ApplyAirDrag(float vy)
        {
            if (_isGrounded || vy >= 0f) return vy;

            float cap = -Mathf.Abs(maxFallSpeed);
            return Mathf.Max(vy, cap);
        }

        /// <summary>
        /// One-line startup report of the values that actually took effect.
        /// Values in the Inspector can be overridden by the runtime repairs in
        /// Awake, so printing the resolved numbers is the only reliable way to
        /// confirm what the physics is really using.
        /// </summary>
        private void LogResolvedSettings()
        {
            Debug.Log(
                $"[Duck] {name}: jumpHeight={jumpHeight} v0={_jumpVelocity:F2} " +
                $"gravity={_gravity:F1} maxFallSpeed={maxFallSpeed} " +
                $"softening={fallSpeedSoftening}", this);
        }

        private void OnDrawGizmosSelected()
        {
            var col = GetComponent<Collider2D>();
            if (col == null) return;

            // Use bounds so the gizmo works for any collider shape.
            Bounds b = col.bounds;

            Gizmos.color = _isGrounded ? Color.green : Color.red;

            Vector2 origin = new Vector2(b.center.x, b.center.y - b.extents.y) + groundCheckOffset;
            Gizmos.DrawWireCube(origin, new Vector3(b.extents.x * 2f * groundCheckWidth, 0.12f, 0f));
        }
    }
}