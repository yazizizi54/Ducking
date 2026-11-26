using DuckKing.CameraSystem;
using UnityEngine;

namespace DuckKing.Background
{
    /// <summary>
    /// Vertical parallax for a climbing game.
    ///
    /// The camera rises as the ducks climb. Each background layer follows that
    /// rise at its own rate, so distant layers appear to hang almost still while
    /// near layers sweep past. This sells depth without anyone having to scroll
    /// the background by hand.
    ///
    /// Parallax Factor is the whole switch:
    ///   0.0  layer is pinned to the camera (infinitely far)
    ///   0.3  slow drift, good for distant mountains or sky
    ///   0.6  clear motion, good for cloud bands or mid trees
    ///   1.0  moves exactly with the world (no parallax at all)
    ///
    /// The layer's authored position in the scene is its position at the starting
    /// camera height, so no extra setup is needed beyond placing it.
    /// </summary>
    [ExecuteAlways]
    public class VerticalParallaxLayer : MonoBehaviour
    {
        [Header("Parallax")]
        [Tooltip("0 = pinned to camera (far), 1 = full world motion (near)")]
        [Range(0f, 1f)]
        [SerializeField] private float parallaxFactor = 0.3f;

        [Header("Target")]
        [Tooltip("Camera to follow. Defaults to the main camera.")]
        [SerializeField] private Transform targetCamera;

        [Header("Horizontal")]
        [Tooltip("Also apply horizontal parallax relative to the camera X")]
        [SerializeField] private bool followHorizontally = false;

        [Range(0f, 1f)]
        [SerializeField] private float horizontalFactor = 0.3f;

        [Header("Horizontal After Summit")]
        [Tooltip("Mirror the camera's summit pan: the layer only starts following the camera " +
                 "sideways once CameraFollowLower allows that pan.\n" +
                 "Without this the background stays put while the camera moves and the lake " +
                 "appears to slide off the edge of the world.")]
        [SerializeField] private bool followHorizontallyAfterSummit = false;

        [Tooltip("Optional camera whose summit state is mirrored. Left empty it is found " +
                 "automatically, so the two settings never drift apart.")]
        [SerializeField] private CameraFollowLower cameraFollow;

        private Vector3 _startPosition;
        private float _startCameraY;
        private float _startCameraX;
        private float _startX;
        private bool _summitPanCached;

        private void OnEnable()
        {
            CacheOrigin();
        }

        /// <summary>
        /// Records where the layer and camera were when the scene loaded.
        /// Every frame is then measured against these, which keeps the layer
        /// anchored exactly where it was authored.
        /// </summary>
        private void CacheOrigin()
        {
            _startPosition = transform.position;
            _startX = _startPosition.x;
            _summitPanCached = false;

            var cam = ResolveCamera();
            if (cam != null)
            {
                _startCameraY = cam.transform.position.y;
                _startCameraX = cam.transform.position.x;
            }
            else
            {
                _startCameraY = _startPosition.y;
                _startCameraX = _startPosition.x;
            }
        }

        private Transform ResolveCamera()
        {
            if (targetCamera != null) return targetCamera;

            var cam = Camera.main;
            return cam != null ? cam.transform : null;
        }

        private void LateUpdate()
        {
            var cam = ResolveCamera();
            if (cam == null) return;

            float cameraDeltaY = cam.position.y - _startCameraY;

            float newY = _startPosition.y + cameraDeltaY * (1f - parallaxFactor);

            float newX = transform.position.x;
            if (followHorizontally)
            {
                float cameraDeltaX = cam.position.x - _startCameraX;
                newX = _startPosition.x + cameraDeltaX * (1f - horizontalFactor);
            }
            else if (followHorizontallyAfterSummit && SummitPanActive())
            {
                // Measured from the moment the pan opened, not from scene load, so the
                // layer does not snap sideways on the switch frame.
                float cameraDeltaX = cam.position.x - _startCameraX;
                newX = _startX + cameraDeltaX * (1f - horizontalFactor);
            }
            else
            {
                newX = _startPosition.x;
            }

            transform.position = new Vector3(newX, newY, _startPosition.z);
        }

        /// <summary>
        /// True when the camera is allowed to pan sideways right now.
        ///
        /// The horizontal origin is re-cached on the first frame the pan turns on.
        /// Without that, every unit the camera travelled while it was still locked
        /// would be applied as a jump on the switch frame.
        /// </summary>
        private bool SummitPanActive()
        {
            if (cameraFollow == null) cameraFollow = FindCameraFollow();
            if (cameraFollow == null) return false;

            bool active = cameraFollow.HorizontalFollowActive;

            if (active && !_summitPanCached)
            {
                _summitPanCached = true;
                _startCameraX = cameraFollow.transform.position.x;
                _startX = transform.position.x;
            }
            else if (!active)
            {
                _summitPanCached = false;
            }

            return active;
        }

        private CameraFollowLower FindCameraFollow()
        {
            var cam = ResolveCamera();
            if (cam != null)
            {
                var found = cam.GetComponent<CameraFollowLower>();
                if (found != null) return found;
            }

            return FindObjectOfType<CameraFollowLower>();
        }

        /// <summary>Re-anchors this layer to the camera's current position.</summary>
        public void Reanchor()
        {
            CacheOrigin();
        }

        /// <summary>Points this layer at a camera, used by ParallaxController.</summary>
        public void SetTargetCamera(Transform camera)
        {
            if (camera == null || targetCamera == camera) return;

            targetCamera = camera;
            CacheOrigin();
        }

        public float ParallaxFactor
        {
            get => parallaxFactor;
            set => parallaxFactor = Mathf.Clamp01(value);
        }
    }
}