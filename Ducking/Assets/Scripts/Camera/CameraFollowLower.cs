using DuckKing.Player;
using UnityEngine;

namespace DuckKing.CameraSystem
{
    /// <summary>
    /// Core mechanic: the camera follows the LOWER of the two ducks.
    /// The leading duck gets pushed out of the top of the screen and must wait
    /// for its partner. No numeric punishment is applied; pressure comes from
    /// not being able to see where you are going.
    /// Horizontal position is locked; only vertical follow is used.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class CameraFollowLower : MonoBehaviour
    {
        [Header("Targets")]
        [SerializeField] private DuckController duckA;
        [SerializeField] private DuckController duckB;

        [Header("Vertical Follow")]
        [Tooltip("Camera Y = lower duck Y + this offset")]
        [SerializeField] private float heightOffset = 2.5f;
        [Tooltip("Smoothing time when the camera moves up")]
        [SerializeField] private float smoothUp = 0.22f;
        [Tooltip("Smoothing time when the camera moves down; keep larger than smoothUp")]
        [SerializeField] private float smoothDown = 0.45f;

        [Header("Horizontal Follow")]
        [Tooltip("Follow the midpoint of both ducks horizontally.\n" +
                 "Turn this OFF for a single-lane vertical shaft: the view stays centred " +
                 "on lockedX and only the camera's vertical position changes.")]
        [SerializeField] private bool followHorizontally = false;
        [Tooltip("Smoothing time for horizontal movement")]
        [SerializeField] private float smoothHorizontal = 0.25f;
        [Tooltip("X position the camera returns to when horizontal follow is off")]
        [SerializeField] private float lockedX = 0f;
        [Tooltip("Half the horizontal dead zone; the camera ignores smaller shifts")]
        [SerializeField] private float horizontalDeadZone = 0.0f;

        [Header("Horizontal After Summit")]
        [Tooltip("Let the camera pan left/right once a duck climbs above the summit height " +
                 "below. Leave off to keep the single-lane vertical view the whole way up.\n" +
                 "This is what makes a wide summit area usable: the shaft keeps the camera " +
                 "locked, the top opens up.")]
        [SerializeField] private bool followHorizontallyAboveSummit = false;

        [Tooltip("World Y a duck must pass before the camera may pan sideways")]
        [SerializeField] private float summitHeight = 100f;

        [Tooltip("Tighten the world X limits at this altitude, so the view never shows " +
                 "past the summit area's own edges.\n" +
                 "The shaft walls are only 10 units wide, which is narrower than the view " +
                 "once the camera pans, so the summit needs its own limits.")]
        [SerializeField] private bool useSummitBounds = true;

        [Tooltip("Leftmost world X the view may reach once the camera pans sideways")]
        [SerializeField] private float summitMinX = -14f;

        [Tooltip("Rightmost world X the view may reach once the camera pans sideways")]
        [SerializeField] private float summitMaxX = 14f;

        [Header("Zoom")]
        [Tooltip("Orthographic size. Must stay an integer for crisp pixel art: " +
                 "size N shows 2N world units tall at 320x180.")]
        [SerializeField] private int orthoSize = 5;
        [Tooltip("Snap camera position to whole pixels to avoid pixel shimmer")]
        [SerializeField] private bool pixelSnap = true;

        [Header("Dead Zone")]
        [Tooltip("Height changes smaller than this are ignored to avoid camera jitter")]
        [SerializeField] private float deadZone = 0.35f;

        [Header("World Bounds (Terraria-style)")]
        [Tooltip("Stop the camera once the view reaches this world edge.\n" +
                 "These are WORLD edges, not camera-centre values: the camera is clamped " +
                 "so its view never shows past them, which is what makes the edge feel solid.")]
        [SerializeField] private bool useWorldBounds = true;

        [Tooltip("Lowest world Y the bottom of the view may reach")]
        [SerializeField] private float worldMinY = 0f;

        [Tooltip("Highest world Y the top of the view may reach")]
        [SerializeField] private float worldMaxY = 200f;

        [Tooltip("Leftmost world X the left of the view may reach")]
        [SerializeField] private float worldMinX = -50f;

        [Tooltip("Rightmost world X the right of the view may reach")]
        [SerializeField] private float worldMaxX = 50f;

        [Tooltip("Add the visible half-width/height automatically instead of clamping the centre.\n" +
                 "Leave on: it is what makes the view stop exactly at the world edge.")]
        [SerializeField] private bool clampByViewEdge = true;

        [Header("Legacy Limits")]
        [Tooltip("Older centre-point limits, ignored when World Bounds is on")]
        [SerializeField] private float minHeight = 0f;
        [SerializeField] private float maxHeight = 200f;

        [Header("Dynamic Zoom")]
        [Tooltip("Pull the camera back as the two ducks separate vertically.\n" +
                 "Keeps both players on screen at the cost of making everything smaller.")]
        [SerializeField] private bool dynamicZoom = true;

        [Tooltip("Height gap where zooming starts, in world units. Below this the view is unchanged.")]
        [SerializeField] private float zoomStartGap = 4f;

        [Tooltip("Height gap where zooming reaches its maximum")]
        [SerializeField] private float zoomFullGap = 14f;

        [Tooltip("Maximum view height multiplier at full gap. 1.4 = 40% wider view.")]
        [Range(1f, 2f)]
        [SerializeField] private float zoomMaxMultiplier = 1.4f;

        [Tooltip("Seconds to blend toward a new zoom level")]
        [SerializeField] private float zoomSmoothTime = 0.35f;

        private float _currentZoomMultiplier = 1f;
        private float _zoomVelocity;
        private float _baseSizeAtStart = -1f;

        private float _currentY;
        private float _smoothVelocity;
        private float _targetY;

        private float _currentX;
        private float _targetX;
        private float _smoothVelocityX;

        private Camera _cam;

        private bool _warnedMissingTargets;

        private bool _summitReached;

        private bool _warnedPixelPerfect;
        private Component _pixelPerfect;

        private void Awake()
        {
            _cam = GetComponent<Camera>();

            // Pixel Perfect Camera lives in a separate package assembly, so it is
            // located by type name rather than a direct reference.
            _pixelPerfect = GetComponent("PixelPerfectCamera");

            // Seed the smooth-damp state from the current transform so the
            // camera settles instead of lerping in from zero on the first frame.
            _currentY = transform.position.y;
            _targetY = _currentY;
            _currentX = transform.position.x;
            _targetX = _currentX;
        }

        // ---- Accessors over the serialized fields above, clamped to safe ranges. ----

        /// <summary>
        /// Orthographic size. Integer only, so pixel art keeps a uniform scale.
        /// Size N shows N*2 world units vertically.
        /// </summary>
        public int OrthoSize
        {
            get => orthoSize;
            set => orthoSize = Mathf.Clamp(value, 2, 20);
        }

        /// <summary>Vertical world units visible on screen.</summary>
        public float VisibleHeight => Mathf.Max(1, orthoSize) * 2f;

        public bool FollowHorizontally
        {
            get => followHorizontally;
            set => followHorizontally = value;
        }

        public float SmoothHorizontal
        {
            get => smoothHorizontal;
            set => smoothHorizontal = Mathf.Max(0.01f, value);
        }

        public bool PixelSnap
        {
            get => pixelSnap;
            set => pixelSnap = value;
        }

        public float HeightOffset
        {
            get => heightOffset;
            set => heightOffset = Mathf.Max(-20f, value);
        }

        public float SmoothUp
        {
            get => smoothUp;
            set => smoothUp = Mathf.Max(0.01f, value);
        }

        public float SmoothDown
        {
            get => smoothDown;
            set => smoothDown = Mathf.Max(0.01f, value);
        }

        public float DeadZone
        {
            get => deadZone;
            set => deadZone = Mathf.Max(0f, value);
        }

        private void Start()
        {
            ApplyZoom();

            // Snap on the first frame so the camera does not fly in from its default spot.
            if (TryGetLowestY(out float lowest))
            {
                _currentY = ClampY(lowest + heightOffset);
                _targetY = _currentY;
            }

            _currentX = ClampX(followHorizontally ? GetMidX() : lockedX);
            _targetX = _currentX;

            _currentX = ClampX(_currentX);

            ApplyPosition(_currentY, _currentX);
        }

        private void LateUpdate()
        {
            if (!TryGetLowestY(out float lowest))
            {
                // A silent return here used to make the camera look "broken" with no
                // explanation. Warn once so the cause is visible in the console.
                WarnMissingTargetsOnce();
                return;
            }

            // Zoom can change at runtime from the tuning panel.
            ApplyZoom();

            UpdateSummitState(lowest);

            float desiredY = ClampY(lowest + heightOffset);

            if (Mathf.Abs(desiredY - _targetY) > deadZone)
            {
                _targetY = desiredY;
            }

            float smoothTime = _targetY > _currentY ? smoothUp : smoothDown;
            _currentY = Mathf.SmoothDamp(_currentY, _targetY, ref _smoothVelocity, smoothTime);

            float desiredX = ClampX(FollowXRaw());
            if (Mathf.Abs(desiredX - _targetX) > horizontalDeadZone)
            {
                _targetX = desiredX;
            }

            float panSmooth = HorizontalFollowActive ? smoothHorizontal : 0f;
            _currentX = panSmooth > 0f
                ? Mathf.SmoothDamp(_currentX, _targetX, ref _smoothVelocityX, panSmooth)
                : _targetX;

            // Clamp again after smoothing: SmoothDamp can overshoot past the limit,
            // and an overshoot would briefly reveal the void beyond the world edge.
            _currentY = ClampY(_currentY);
            _currentX = ClampX(_currentX);

            ApplyPosition(_currentY, _currentX);
        }

        private void WarnMissingTargetsOnce()
        {
            if (_warnedMissingTargets) return;
            _warnedMissingTargets = true;

            Debug.LogWarning(
                "[DuckKing] CameraFollowLower has no duck targets. " +
                "Assign Duck A / Duck B on the Main Camera, or re-run " +
                "DuckKing > 8. Rebuild Current Scene Clean.", this);
        }

        /// <summary>
        /// Horizontal centre of both ducks. Falls back to whichever duck exists.
        /// </summary>
        private float GetMidX()
        {
            bool hasA = duckA != null;
            bool hasB = duckB != null;

            if (hasA && hasB) return (duckA.transform.position.x + duckB.transform.position.x) * 0.5f;
            if (hasA) return duckA.transform.position.x;
            if (hasB) return duckB.transform.position.x;

            return lockedX;
        }

        /// <summary>
        /// Applies the orthographic size, including the dynamic zoom.
        ///
        /// Pixel Perfect Camera also writes camera.orthographicSize from its
        /// Reference Resolution. Two systems writing the same field every frame
        /// fight each other, so Pixel Perfect is disabled while dynamic zoom runs.
        ///
        /// The base size is derived rather than read: at Awake the camera still
        /// holds whatever size was serialized in the scene, which is stale and can
        /// be far too large. Reading the Pixel Perfect settings and computing
        /// (refY / 2) / PPU gives the size the view actually had.
        /// </summary>
        private void ApplyZoom()
        {
            if (_cam == null) _cam = GetComponent<Camera>();
            if (_cam == null) return;

            bool wantsDynamic = dynamicZoom && duckA != null && duckB != null;

            if (dynamicZoom && _baseSizeAtStart < 0f)
            {
                _baseSizeAtStart = ResolveBaseSize();
            }
            else if (!dynamicZoom)
            {
                _baseSizeAtStart = -1f;
            }

            if (!wantsDynamic)
            {
                if (dynamicZoom && _cam.orthographicSize <= 0.01f && _baseSizeAtStart > 0f)
                {
                    _cam.orthographicSize = _baseSizeAtStart;
                    return;
                }

                if (!dynamicZoom && _pixelPerfect != null)
                {
                    // Pixel Perfect owns the size again.
                    if (_pixelPerfect is Behaviour ppbOn) ppbOn.enabled = true;
                    return;
                }

                if (!dynamicZoom)
                {
                    _cam.orthographicSize = Mathf.Max(1, orthoSize);
                }
                else
                {
                    _cam.orthographicSize = _baseSizeAtStart;
                }
                return;
            }

            // Take ownership of the size and stand Pixel Perfect down.
            SuppressPixelPerfect();

            float gap = GetDuckHeightGap();
            float t = Mathf.InverseLerp(zoomStartGap, zoomFullGap, gap);
            float desiredMultiplier = Mathf.Lerp(1f, zoomMaxMultiplier, t);

            _currentZoomMultiplier = Mathf.SmoothDamp(
                _currentZoomMultiplier, desiredMultiplier, ref _zoomVelocity, zoomSmoothTime);

            _cam.orthographicSize = _baseSizeAtStart * _currentZoomMultiplier;
        }

        /// <summary>
        /// Determines the unscaled view size.
        ///
        /// Preference order:
        ///  1. Pixel Perfect Camera's Reference Resolution, which is what the game
        ///     is actually designed around,
        ///  2. the integer Ortho Size field,
        ///  3. whatever the camera currently holds, as a last resort.
        /// </summary>
        private float ResolveBaseSize()
        {
            if (_pixelPerfect != null)
            {
                // Runtime code cannot use SerializedObject (editor-only), so the
                // public Reference Resolution and PPU are read by reflection.
                int ppu = ReadIntField(_pixelPerfect, "pixelsPerUnit", "m_AssetsPPU", 32);
                int refY = ReadIntField(_pixelPerfect, "referenceResolution", "m_RefResolutionY", 0);
                int refX = ReadIntField(_pixelPerfect, "referenceResolution", "m_RefResolutionX", 0);

                if (refY <= 0)
                {
                    // referenceResolution is a Vector2Int on some versions.
                    Vector2Int res = ReadVector2IntField(_pixelPerfect, "referenceResolution");
                    if (res.y > 0) { refX = res.x; refY = res.y; }
                    else
                    {
                        refX = ReadIntField(_pixelPerfect, "m_RefResolutionX", "m_RefResolutionX", 0);
                        refY = ReadIntField(_pixelPerfect, "m_RefResolutionY", "m_RefResolutionY", 0);
                    }
                }

                if (refY > 0)
                {
                    float byHeight = (refY * 0.5f) / Mathf.Max(1f, ppu);

                    if (refX > 0)
                    {
                        float screenAspect = _cam.aspect > 0.01f ? _cam.aspect : 16f / 9f;
                        float refAspect = (float)refX / refY;

                        // A window narrower than the reference has to grow in height
                        // so the full reference width still fits.
                        if (screenAspect < refAspect)
                        {
                            float byWidth = (refX * 0.5f) / Mathf.Max(1f, ppu) / screenAspect;
                            return Mathf.Max(byHeight, byWidth);
                        }
                    }

                    return byHeight;
                }
            }

            if (_cam.orthographicSize > 0.01f) return _cam.orthographicSize;

            return Mathf.Max(1, orthoSize);
        }

        /// <summary>
        /// Reads an integer field or property from a component by name.
        /// Reflection is used because Pixel Perfect Camera lives in another
        /// assembly and its fields differ between package versions, so each
        /// setting is looked up under a couple of possible names.
        /// </summary>
        private static int ReadIntField(Component target, string name, string fallbackName, int defaultValue)
        {
            if (target == null) return defaultValue;

            var type = target.GetType();

            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic;

            var field = type.GetField(name, flags) ?? type.GetField(fallbackName, flags);
            if (field != null)
            {
                object value = field.GetValue(target);
                if (value is int i) return i;
                if (value is float f) return Mathf.RoundToInt(f);
            }

            var prop = type.GetProperty(name, flags);
            if (prop != null)
            {
                object value = prop.GetValue(target);
                if (value is int i) return i;
                if (value is Vector2Int v) return v.y;
            }

            return defaultValue;
        }

        private static Vector2Int ReadVector2IntField(Component target, string name)
        {
            if (target == null) return Vector2Int.zero;

            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public |
                System.Reflection.BindingFlags.NonPublic;

            var type = target.GetType();

            var field = type.GetField(name, flags);
            if (field != null && field.GetValue(target) is Vector2Int fv) return fv;

            var prop = type.GetProperty(name, flags);
            if (prop != null && prop.GetValue(target) is Vector2Int pv) return pv;

            return Vector2Int.zero;
        }

        private void SuppressPixelPerfect()
        {
            if (_pixelPerfect == null) return;

            if (_pixelPerfect is Behaviour ppb && ppb.enabled)
            {
                ppb.enabled = false;

                if (!_warnedPixelPerfect)
                {
                    _warnedPixelPerfect = true;
                    Debug.Log(
                        "[DuckKing] Pixel Perfect Camera disabled for dynamic zoom.\n" +
                        "Smooth dynamic zooming and strict pixel snapping both write " +
                        "camera.orthographicSize, so only one can run. Turn off Dynamic " +
                        "Zoom on CameraFollowLower to hand control back to Pixel Perfect.",
                        this);
                }
            }
        }

        /// <summary>Vertical distance between the two ducks.</summary>
        public float GetDuckHeightGap()
        {
            if (duckA == null || duckB == null) return 0f;
            return Mathf.Abs(duckA.Height - duckB.Height);
        }

        public bool HasBothTargets => duckA != null && duckB != null;

        public bool DynamicZoom
        {
            get => dynamicZoom;
            set => dynamicZoom = value;
        }

        private void ApplyPosition(float y, float x)
        {
            Vector3 p = transform.position;
            p.y = y;
            p.x = x;

            if (pixelSnap)
            {
                // Snap to 1/32 world units, matching the project's 32 PPU.
                const float ppu = 32f;
                p.x = Mathf.Round(p.x * ppu) / ppu;
                p.y = Mathf.Round(p.y * ppu) / ppu;
            }

            transform.position = p;
        }

        /// <summary>
        /// Clamps a desired camera Y against the world bounds.
        ///
        /// In view-edge mode the half-height is subtracted, so the clamp describes
        /// where the top/bottom of the SCREEN may reach rather than where the
        /// camera centre may reach. That is what makes the edge feel solid:
        /// the camera stops the instant the world edge becomes visible.
        /// </summary>
        private float ClampY(float desiredY)
        {
            if (!useWorldBounds)
            {
                return Mathf.Clamp(desiredY, minHeight, maxHeight);
            }

            if (!clampByViewEdge)
            {
                return Mathf.Clamp(desiredY, worldMinY, worldMaxY);
            }

            float half = VisibleHalfHeight;
            float lo = worldMinY + half;
            float hi = worldMaxY - half;

            // A world shorter than the view has no valid range; centre it instead.
            if (lo > hi)
            {
                return (worldMinY + worldMaxY) * 0.5f;
            }

            return Mathf.Clamp(desiredY, lo, hi);
        }

        private float ClampX(float desiredX)
        {
            if (!useWorldBounds) return desiredX;

            // The shaft is only a few units wide; the summit area is much wider than
            // the view. Swapping the limits here is what lets the camera pan at the
            // top without ever showing the void beyond the shaft walls on the way up.
            float minX = UsesSummitBounds ? summitMinX : worldMinX;
            float maxX = UsesSummitBounds ? summitMaxX : worldMaxX;

            if (!clampByViewEdge)
            {
                return Mathf.Clamp(desiredX, minX, maxX);
            }

            float half = VisibleHalfWidth;
            float lo = minX + half;
            float hi = maxX - half;

            if (lo > hi)
            {
                return (minX + maxX) * 0.5f;
            }

            return Mathf.Clamp(desiredX, lo, hi);
        }

        /// <summary>Half the visible height in world units.</summary>
        public float VisibleHalfHeight
        {
            get
            {
                if (_cam == null) _cam = GetComponent<Camera>();
                float size = _cam != null ? _cam.orthographicSize : orthoSize;
                return Mathf.Max(0f, size);
            }
        }

        /// <summary>Half the visible width in world units, derived from the aspect ratio.</summary>
        public float VisibleHalfWidth
        {
            get
            {
                if (_cam == null) _cam = GetComponent<Camera>();
                float aspect = _cam != null ? _cam.aspect : 16f / 9f;
                return VisibleHalfHeight * Mathf.Max(0.01f, aspect);
            }
        }

        /// <summary>
        /// Fits the world bounds to a rectangle, used by the editor tool that reads
        /// the painted tilemap and configures the camera automatically.
        /// </summary>
        public void SetWorldBounds(float minX, float maxX, float minY, float maxY)
        {
            worldMinX = minX;
            worldMaxX = maxX;
            worldMinY = minY;
            worldMaxY = maxY;
            useWorldBounds = true;

            // The legacy centre limits must not fight the new bounds.
            minHeight = -10000f;
            maxHeight = 10000f;
        }

        public bool UseWorldBounds
        {
            get => useWorldBounds;
            set => useWorldBounds = value;
        }

        protected void OnValidate()
        {
            if (worldMaxY < worldMinY) worldMaxY = worldMinY;
            if (worldMaxX < worldMinX) worldMaxX = worldMinX;
        }

        /// <summary>
        /// Returns the Y of whichever duck is lower.
        /// Degrades gracefully if one or both references are missing.
        /// </summary>
        private bool TryGetLowestY(out float lowest)
        {
            bool hasA = duckA != null;
            bool hasB = duckB != null;

            if (hasA && hasB)
            {
                lowest = Mathf.Min(duckA.Height, duckB.Height);
                return true;
            }

            if (hasA)
            {
                lowest = duckA.Height;
                return true;
            }

            if (hasB)
            {
                lowest = duckB.Height;
                return true;
            }

            lowest = 0f;
            return false;
        }

        /// <summary>Rebind targets at runtime.</summary>
        public void SetTargets(DuckController a, DuckController b)
        {
            duckA = a;
            duckB = b;
        }

        // ---- Summit panning ----------------------------------------------------

        /// <summary>
        /// True when the camera is allowed to move sideways: either the plain
        /// horizontal follow, or the summit switch once a duck is high enough.
        /// </summary>
        public bool HorizontalFollowActive =>
            followHorizontally || (followHorizontallyAboveSummit && _summitReached);

        /// <summary>
        /// Latches the summit switch the first time a duck climbs past the threshold.
        ///
        /// It latches rather than tracking "is anyone high right now" because the
        /// camera follows the LOWER duck: when the leader climbs away, the lower duck
        /// stays behind, and a live check would flip the pan off and back on again.
        /// </summary>
        private void UpdateSummitState(float _)
        {
            if (!followHorizontallyAboveSummit || _summitReached) return;

            bool highA = duckA != null && duckA.Height >= summitHeight;
            bool highB = duckB != null && duckB.Height >= summitHeight;

            if (highA || highB) _summitReached = true;
        }

        /// <summary>Raw horizontal target, before world limits are applied.</summary>
        private float FollowXRaw()
        {
            return HorizontalFollowActive ? GetMidX() : lockedX;
        }

        /// <summary>
        /// True while the camera is above the summit and should use the summit's own
        /// horizontal limits instead of the narrow shaft walls.
        /// </summary>
        private bool UsesSummitBounds => _summitReached && useSummitBounds;
    }
}