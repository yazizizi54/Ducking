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

        private Vector3 _startPosition;
        private float _startCameraY;
        private float _startCameraX;

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
            else
            {
                newX = _startPosition.x;
            }

            transform.position = new Vector3(newX, newY, _startPosition.z);
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