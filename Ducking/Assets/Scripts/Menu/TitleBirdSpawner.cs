using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DuckKing.Menu
{
    /// <summary>
    /// Spawns small flocks of birds drifting across the title screen.
    ///
    /// Each flock is a few birds on a diagonal, staggered so they enter one after
    /// another like a formation rather than a single blob. Birds fly past the edge,
    /// then destroy themselves, so nothing accumulates over time.
    ///
    /// Birds are UI Images under the Canvas, which keeps them drawn between the
    /// background and the menu text without touching the 3D camera.
    /// </summary>
    public class TitleBirdSpawner : MonoBehaviour
    {
        [Header("Frames")]
        [Tooltip("Two or more frames of the bird flapping. Order sets the cycle.")]
        [SerializeField] private Sprite[] birdFrames;
        [Tooltip("Seconds per animation frame")]
        [SerializeField] private float flapFrameTime = 0.18f;

        [Header("Flocking")]
        [Tooltip("Birds per flock")]
        [SerializeField] private int birdsPerFlock = 4;
        [Tooltip("Seconds between flocks at minimum")]
        [SerializeField] private float minInterval = 6f;
        [Tooltip("Seconds between flocks at maximum")]
        [SerializeField] private float maxInterval = 12f;
        [Tooltip("Delay between each bird inside a flock")]
        [SerializeField] private float birdStagger = 0.35f;

        [Header("Flight")]
        [Tooltip("Horizontal speed in UI units per second")]
        [SerializeField] private float speed = 220f;
        [Tooltip("Extra units off-screen before spawning / despawning")]
        [SerializeField] private float offscreenMargin = 120f;

        [Header("Placement")]
        [Tooltip("Bird height as a fraction of screen height, top of range (0 = bottom, 1 = top)")]
        [SerializeField] private float heightMax = 0.86f;
        [Tooltip("Bird height as a fraction of screen height, bottom of range")]
        [SerializeField] private float heightMin = 0.58f;
        [Tooltip("Vertical spread between birds inside a flock")]
        [SerializeField] private float formationSpread = 26f;
        [Tooltip("Vertical drift per second, negative flies downward")]
        [SerializeField] private float verticalDrift = -6f;

        [Header("Size")]
        [Tooltip("Bird width and height in UI units")]
        [SerializeField] private float birdSize = 42f;
        [Tooltip("Tint applied to the bird sprite")]
        [SerializeField] private Color birdColor = Color.white;

        [Header("Behaviour")]
        [Tooltip("Start flying as soon as the scene loads")]
        [SerializeField] private bool playOnStart = true;
        [Tooltip("Allow some flocks to fly right to left")]
        [Range(0f, 1f)]
        [SerializeField] private float reverseChance = 0.3f;

        private RectTransform _root;
        private float _timer;
        private float _nextSpawn;
        private readonly List<GameObject> _birds = new List<GameObject>();

        private void Awake()
        {
            // A container keeps the hierarchy tidy and survives the birds being
            // destroyed, so spawning always has a stable parent.
            var container = new GameObject("Birds", typeof(RectTransform));
            container.transform.SetParent(transform, false);

            _root = container.GetComponent<RectTransform>();
            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.offsetMin = Vector2.zero;
            _root.offsetMax = Vector2.zero;

            ScheduleNext();
        }

        private void Start()
        {
            if (playOnStart) _timer = _nextSpawn;
        }

        private void Update()
        {
            // Unscaled time: the title screen should keep animating even if a
            // previous scene left timeScale at zero.
            _timer += Time.unscaledDeltaTime;

            if (_timer >= _nextSpawn)
            {
                _timer = 0f;
                ScheduleNext();
                StartCoroutine(SpawnFlockRoutine());
            }
        }

        private void ScheduleNext()
        {
            _nextSpawn = Random.Range(Mathf.Min(minInterval, maxInterval),
                                     Mathf.Max(minInterval, maxInterval));
        }

        private System.Collections.IEnumerator SpawnFlockRoutine()
        {
            if (!HasFrames()) yield break;

            int count = Mathf.Max(1, birdsPerFlock);
            bool reverse = Random.value < reverseChance;

            // All birds in one flock share a direction and a base height so they
            // read as a single formation.
            float baseHeight = RandomHeight();

            for (int i = 0; i < count; i++)
            {
                SpawnBird(baseHeight + i * formationSpread, reverse);
                yield return new WaitForSecondsRealtime(birdStagger);
            }
        }

        private void SpawnBird(float height, bool reverse)
        {
            var go = new GameObject("Bird", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_root, false);

            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(birdSize, birdSize);
            rect.anchoredPosition = new Vector2(StartX(reverse), height);

            var img = go.GetComponent<Image>();
            img.sprite = birdFrames[0];
            img.color = birdColor;
            img.raycastTarget = false;    // birds must never eat UI clicks
            img.preserveAspect = true;

            // Facing: the art points right, so mirror it when flying left.
            if (reverse) rect.localScale = new Vector3(-1f, 1f, 1f);

            var bird = go.AddComponent<TitleBird>();
            bird.Initialize(rect, img, birdFrames, flapFrameTime,
                reverse ? -speed : speed, verticalDrift, EndX(reverse), offscreenMargin);

            _birds.Add(go);
        }

        private float StartX(bool reverse)
        {
            float halfWidth = GetHalfWidth() + offscreenMargin;
            return reverse ? halfWidth : -halfWidth;
        }

        private float EndX(bool reverse)
        {
            float halfWidth = GetHalfWidth() + offscreenMargin;
            return reverse ? -halfWidth : halfWidth;
        }

        private float RandomHeight()
        {
            float min = Mathf.Min(heightMin, heightMax);
            float max = Mathf.Max(heightMin, heightMax);
            float t = Random.Range(min, max);

            // Anchored to the bottom of the canvas, so scale by the canvas height.
            return GetHeight() * t;
        }

        private float GetHeight()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null && canvas.GetComponent<RectTransform>() != null)
            {
                return canvas.GetComponent<RectTransform>().rect.height;
            }

            return Screen.height;
        }

        private float GetHalfWidth()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null && canvas.GetComponent<RectTransform>() != null)
            {
                return canvas.GetComponent<RectTransform>().rect.width * 0.5f;
            }

            return Screen.width * 0.5f;
        }

        private bool HasFrames()
        {
            return birdFrames != null && birdFrames.Length > 0 && birdFrames[0] != null;
        }

        /// <summary>Stops all birds and clears the container.</summary>
        public void StopAll()
        {
            foreach (var go in _birds)
            {
                if (go != null) Destroy(go);
            }

            _birds.Clear();
        }
    }
}