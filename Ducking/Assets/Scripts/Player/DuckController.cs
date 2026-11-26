using DuckKing.Gameplay;
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
        [Tooltip("起跳后多少秒内松开跳跃键才算短跳，超过这个时间就不再看按键")]
        [SerializeField] private float jumpCutWindow = 0.12f;

        [Header("手感辅助")]
        [Tooltip("土狼时间：离开平台后仍可起跳的宽限秒数")]
        [SerializeField] private float coyoteTime = 0.15f;
        [Tooltip("跳跃缓冲：落地前提前按跳跃仍然生效的秒数")]
        [SerializeField] private float jumpBufferTime = 0.18f;

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

        [Tooltip("临时诊断：勾上后每 0.1 秒打印一次地面判定结果")]
        [SerializeField] private bool debugGround;

        [Tooltip("临时诊断：把速度写入后立即读回来，用于确认物理引擎是否在覆盖它")]
        [SerializeField] private bool debugVelocity;

        [Tooltip("临时诊断：勾上后 F10 把鸭子抬高一格，F11 压低一格，用来验证是否卡在碰撞体里")]
        [SerializeField] private bool debugNudgeUp;

        [SerializeField] private bool debugNudgeDown;

        [Tooltip("F10/F11 每次移动的距离（单位）")]
        [SerializeField] private float nudgeStep = 1f;

        private float _groundLogTimer;
        private float _moveLogTimer;

        /// <summary>Time of the last successful takeoff, used to bound the short-hop window.</summary>
        private float _jumpStartTime;
        private float _gravity;
        private float _jumpVelocity;

        private bool _inputEnabled = true;

        /// <summary>
        /// The plank this duck is standing on, or null when it is on normal ground.
        /// While riding, the duck is a child of that plank, so the plank's rotation
        /// carries it automatically and nothing has to correct its height.
        /// </summary>
        private Transform _plankRiding;

        /// <summary>本角色当前世界坐标高度，镜头系统依赖此项。</summary>
        public float Height => transform.position.y;

        /// <summary>是否站在地面上。</summary>
        public bool IsGrounded => _isGrounded;

        /// <summary>Current physics velocity, for audio and feedback that must not touch it.</summary>
        public Vector2 BodyVelocity => _rb != null ? _rb.velocity : Vector2.zero;

        /// <summary>
        /// True on the frame a jump actually launched. Read by DuckSfx so the jump
        /// sound fires on the real jump, not on a buffered key press that failed.
        /// </summary>
        public bool JumpedThisFrame { get; private set; }

        /// <summary>
        /// Time.time of the most recent jump. Unlike JumpedThisFrame, which spans a
        /// single physics step, this lets one-shot feedback compare and fire once.
        /// </summary>
        public float LastJumpTime { get; private set; } = -1f;

        /// <summary>True while the duck is standing on a balance plank.</summary>
        public bool IsRidingPlank => _plankRiding != null;

        /// <summary>
        /// The duck's velocity expressed in the plank's local space.
        ///
        /// Rigidbody2D.velocity is always world space, so when the duck is parented to a
        /// tilted plank its velocity has to be rotated into that plank's frame before it
        /// can be handed back after leaving.
        /// </summary>
        public Vector2 CurrentLocalVelocity
        {
            get
            {
                if (_rb == null) return Vector2.zero;
                if (_plankRiding == null) return _rb.velocity;

                return _plankRiding.InverseTransformDirection(_rb.velocity);
            }
        }

        /// <summary>
        /// Called by a balance plank when the duck steps onto it. The duck is not
        /// parented to the plank: Rigidbody2D simulation ignores the transform hierarchy,
        /// so parenting would not actually make the duck rotate with it. Recording the
        /// plank is enough, because the controller only needs it to (a) express its
        /// movement in the plank's frame and (b) hand the vertical axis to the engine.
        /// </summary>
        public void OnBoardPlank(Transform plank)
        {
            if (_plankRiding == plank) return;

            _plankRiding = plank;

            // The engine now supplies gravity along the plank's normal, so vertical
            // velocity must stop being written by this controller. Enabling gravity
            // scale is what makes a duck on a slope actually slide down it.
            if (_rb != null) _rb.gravityScale = PlankGravityScale();
        }

        /// <summary>
        /// Called by a balance plank when the duck leaves it. The vertical axis goes back
        /// to the scripted gravity, which expects the duck to be weightless as far as the
        /// engine is concerned.
        /// </summary>
        public void OnLeavePlank(Vector2 worldVelocity)
        {
            if (_plankRiding == null) return;

            _plankRiding = null;

            if (_rb != null)
            {
                _rb.gravityScale = 0f;
                _rb.velocity = worldVelocity;
            }
        }

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

            // Pick the collider that is actually enabled. GetComponent returns a disabled
            // component too, and a disabled collider reports an empty bounds, which put
            // the ground probe at the duck's centre with a zero-sized footprint. That
            // misreported "grounded" while the duck kept falling, and the duck only kept
            // moving because the large ground acceleration outran the contact solver.
            _collider = GetFirstEnabledCollider();

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
                // Report rather than silently continue: without a collider the ground
                // probe has nothing to measure and every surface reads as "not grounded".
                Debug.LogError($"[Duck] {name} has no enabled 2D collider. Ground detection disabled.", this);
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
        /// Returns the first enabled 2D collider on this object.
        ///
        /// The ducks carry a disabled capsule alongside the real box. GetComponent hands
        /// back disabled components, so asking for the capsule by type silently selected
        /// an empty collider and broke ground detection.
        /// </summary>
        private Collider2D GetFirstEnabledCollider()
        {
            var colliders = GetComponents<Collider2D>();
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] != null && colliders[i].enabled) return colliders[i];
            }

            return null;
        }

        /// <summary>
        /// Gravity used while standing on a plank, expressed as a multiple of Unity's
        /// world gravity.
        ///
        /// On the plank the engine owns the vertical axis, so the duck has to be given a
        /// gravity scale that reproduces the same feel as the scripted gravity above.
        /// The scripted fall is _gravity * fallGravityMultiplier, and Unity's default
        /// world gravity is -9.81, so the scale is that ratio.
        /// </summary>
        private float PlankGravityScale()
        {
            const float worldGravity = 9.81f;
            float effective = _gravity * fallGravityMultiplier;
            return Mathf.Clamp(effective / worldGravity, 0.1f, 60f);
        }

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

            // The buffer only ever has to shrink over real time, and Update is the only
            // place it shrinks, so it is safe to age it here. Coyote time is NOT aged
            // here: it is aged once per physics step in FixedUpdate, because a frame that
            // spans several physics steps would otherwise drain it several times over and
            // turn a generous grace period into a coin flip.
            if (_inputProvider.JumpPressedThisFrame)
            {
                _jumpBufferTimer = jumpBufferTime;
            }
            else if (_jumpBufferTimer > 0f)
            {
                _jumpBufferTimer -= Time.deltaTime;
            }

            // Toggle fall diagnostics with F9. Temporary probe for the air drag.
            if (UnityEngine.Input.GetKeyDown(KeyCode.F9))
            {
                _isDebugFall = !_isDebugFall;
                Debug.Log($"[FallDbg] {name} diagnostics {(_isDebugFall ? "ON" : "OFF")}", this);
            }

            // Temporary probe for "jump does not fire while a direction is held".
            // Logs the raw input state on the exact frame the jump key goes down, so a
            // swallowed press can be told apart from a press that never arrived.
            if (_inputProvider.JumpPressedThisFrame)
            {
                Debug.Log(
                    $"[JumpProbe] {name} jumpKeyDown jumpHeld={_inputProvider.JumpHeld} " +
                    $"grounded={_isGrounded} vy={_rb.velocity.y:F2} " +
                    $"coyote={_coyoteTimer:F3} buffer={_jumpBufferTimer:F3} " +
                    $"moveInput={_moveInput:F1} inputEnabled={_inputEnabled} " +
                    $"ridingPlank={_plankRiding != null}", this);
            }
        }

        private void FixedUpdate()
        {
            // The flag describes a single physics step, so it is cleared before the
            // step that may set it again.
            JumpedThisFrame = false;

            RecalculateJumpPhysics();
            UpdateGrounded();

            // Refreshed every step while standing. Refreshing rather than only starting
            // the countdown on the step the duck leaves the ground is what makes walking
            // off a ledge still count as a jump opportunity for a moment.
            if (_isGrounded && _rb.velocity.y <= 0f)
            {
                _coyoteTimer = coyoteTime;
            }
            else if (_coyoteTimer > 0f)
            {
                _coyoteTimer -= Time.fixedDeltaTime;
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

            if (debugGround)
            {
                _groundLogTimer += Time.fixedDeltaTime;
                if (_groundLogTimer >= 0.1f)
                {
                    _groundLogTimer = 0f;
                    Debug.Log(
                        $"[Ground] {name} grounded={_isGrounded} " +
                        $"vx={_rb.velocity.x:F2} pos=({transform.position.x:F2},{transform.position.y:F2})",
                        this);
                }
            }
        }

        private void TryJump()
        {
            if (_jumpBufferTimer <= 0f || _coyoteTimer <= 0f) return;

            // A jump is always straight up relative to the surface being stood on. On a
            // tilted plank that is the plank's up axis; on flat ground it is world up.
            Vector3 jumpDirection = _plankRiding != null ? _plankRiding.up : Vector3.up;

            Vector2 current = _rb.velocity;
            _rb.velocity = current + (Vector2)jumpDirection * _jumpVelocity;

            _jumpBufferTimer = 0f;
            _coyoteTimer = 0f;
            _isGrounded = false;
            _jumpCutApplied = false;
            _jumpStartTime = Time.time;

            // A jump leaves the plank immediately, so the engine's gravity must be handed
            // back to the scripted gravity in the same step. Otherwise the duck keeps
            // the plank gravity mid-air and drops much faster than it does off a ledge.
            // The plank notices the duck has left on its own next step.
            if (_plankRiding != null)
            {
                _plankRiding = null;
                if (_rb != null) _rb.gravityScale = 0f;
            }

            JumpedThisFrame = true;
            LastJumpTime = Time.time;
        }

        private void ApplyHorizontalMovement()
        {
            float targetSpeed = _moveInput * moveSpeed;
            float currentSpeed = _rb.velocity.x;

            bool isAccelerating = Mathf.Abs(targetSpeed) > 0.01f;

            float accel = isAccelerating
                ? (_isGrounded ? groundAccel : airAccel)
                : (_isGrounded ? groundDecel : airDecel);

            // On a plank the duck still has to walk, but "right" means "along the surface"
            // rather than "along world X". Rather than pushing a force through the solver
            // (which fights the plank contact and, at these accelerations, barely moves
            // the duck at all), the surface component of the velocity is set directly and
            // the normal component is left exactly as the solver produced it. That keeps
            // the plank holding the duck up while giving back the same responsiveness as
            // walking on flat ground.
            if (_plankRiding != null)
            {
                ApplyPlankWalk(accel, targetSpeed);
                return;
            }

            float newSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed, accel * Time.fixedDeltaTime);
            _rb.velocity = new Vector2(newSpeed, _rb.velocity.y);

            if (debugGround)
            {
                _moveLogTimer += Time.fixedDeltaTime;
                if (_moveLogTimer >= 0.1f)
                {
                    _moveLogTimer = 0f;
                    Debug.Log(
                        $"[Move] {name} moveInput={_moveInput:F2} target={targetSpeed:F2} " +
                        $"accel={accel:F1} grounded={_isGrounded} " +
                        $"before={currentSpeed:F2} after={_rb.velocity.x:F2}", this);
                }
            }
        }

        /// <summary>
        /// Walks a duck along a tilted plank.
        ///
        /// The velocity is split into a component along the plank surface and a component
        /// along its normal. Only the along-surface part is rewritten; the normal part is
        /// preserved untouched, because that component is what the plank contact is
        /// holding up. Overwriting it is what made the old scripted plank impossible to
        /// stand on, and leaving it alone is what keeps this version stable.
        ///
        /// Gravity is cancelled along the surface. The plank runs with the engine's
        /// gravity enabled so the contact keeps the duck pinned to it, but that same
        /// gravity also pulls along the slope: at 18 degrees the downhill pull is about
        /// 42 units/s^2 against a 90 unit/s^2 walking acceleration, which is why walking
        /// uphill used to feel like wading through mud. The slope pull is subtracted back
        /// out every step, so the surface behaves like flat ground in both directions.
        /// </summary>
        private void ApplyPlankWalk(float accel, float targetSpeed)
        {
            Vector2 along = _plankRiding.right;
            Vector2 normal = _plankRiding.up;

            Vector2 velocity = _rb.velocity;

            // Split into the two plank axes.
            float alongSpeed = Vector2.Dot(velocity, along);
            float normalSpeed = Vector2.Dot(velocity, normal);

            // Remove the component of gravity that acts along the slope. Without this the
            // duck fights the slope on the way up and is dragged downslope while standing
            // still, which also breaks "stand still and stay put".
            float gravityAlong = Physics2D.gravity.y * _rb.gravityScale * along.y;
            alongSpeed -= gravityAlong * Time.fixedDeltaTime;

            // Accelerate along the surface exactly like flat ground does.
            alongSpeed = Mathf.MoveTowards(alongSpeed, targetSpeed, accel * Time.fixedDeltaTime);

            // Rebuild the velocity from the unchanged normal component.
            _rb.velocity = along * alongSpeed + normal * normalSpeed;
        }

        private void ApplyVerticalPhysics()
        {
            // Temporary: let the designer lift the duck out of whatever it is wedged in
            // without leaving Play mode. Respawn points and hand-placed colliders drift,
            // and rebuilding the scene just to unstick a test duck wastes a minute each
            // time. Remove once the level geometry is final.
            if (debugNudgeUp && UnityEngine.Input.GetKeyDown(KeyCode.F10))
            {
                Vector2 p = _rb.position;
                _rb.position = new Vector2(p.x, p.y + nudgeStep);
                _rb.velocity = Vector2.zero;
                Debug.Log($"[Nudge] {name} moved to y={_rb.position.y:F2}", this);
            }

            if (debugNudgeDown && UnityEngine.Input.GetKeyDown(KeyCode.F11))
            {
                Vector2 p = _rb.position;
                _rb.position = new Vector2(p.x, p.y - nudgeStep);
                _rb.velocity = Vector2.zero;
                Debug.Log($"[Nudge] {name} moved to y={_rb.position.y:F2}", this);
            }
            // On a plank the vertical axis is handed to the physics engine. The plank is
            // a real hinged body and its surface normal pushes the duck up; writing
            // velocity.y here would overwrite that push every step and the duck would
            // sink through the plank, which is exactly what happened with a scripted
            // plank. Gravity for this case comes from the engine instead, so the duck
            // rigidbody's gravity scale is switched on while riding.
            if (_plankRiding != null && _rb != null)
            {
                _rb.gravityScale = PlankGravityScale();
                return;
            }

            float vy = _rb.velocity.y;

            // Short hop: releasing the jump key while still rising cuts the rise short.
            //
            // Only the case where the key is actually released counts. This used to also
            // latch on every step of the rise, which meant one frame where the key read as
            // released would cut a jump in half; that is why it now clears the flag only
            // on a real release and never otherwise.
            if (!_jumpCutApplied && vy > 0f && !_inputProvider.JumpHeld)
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

            if (debugGround && _moveLogTimer == 0f)
            {
                Debug.Log(
                    $"[Vert] {name} vy={vy:F2} rbVx={_rb.velocity.x:F2} rbVy={_rb.velocity.y:F2} " +
                    $"bodyType={_rb.bodyType} constraints={_rb.constraints} " +
                    $"interp={_rb.interpolation} sleeping={_rb.IsSleeping()}", this);
            }
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