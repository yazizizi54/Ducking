using UnityEngine;

namespace DuckKing.Background
{
    /// <summary>
    /// Optional coordinator for the parallax layers.
    ///
    /// Layers work independently. This component just saves setup work:
    ///  - points every layer at one camera,
    ///  - gives layers sorting orders derived from their depth so far layers
    ///    never draw over near ones,
    ///  - offers a re-anchor after a respawn, which is easy to forget and leaves
    ///    layers drifting away from the level.
    ///
    /// Put it on any object in the scene; layers are found automatically.
    /// </summary>
    public class ParallaxController : MonoBehaviour
    {
        [Header("Camera")]
        [Tooltip("Camera all layers should follow. Defaults to the main camera.")]
        [SerializeField] private Transform targetCamera;

        [Header("Behaviour")]
        [Tooltip("Assign sorting orders from depth so far layers draw behind near ones")]
        [SerializeField] private bool autoSortingOrder = true;

        [Tooltip("Gap between consecutive layers' sorting orders")]
        [SerializeField] private int sortingOrderStep = 10;

        [Tooltip("Sorting order given to the furthest layer")]
        [SerializeField] private int baseSortingOrder = -1000;

        private void Start()
        {
            Apply();
        }

        [ContextMenu("Apply To Layers")]
        public void Apply()
        {
            var cam = targetCamera != null
                ? targetCamera
                : (Camera.main != null ? Camera.main.transform : null);

            var layers = FindObjectsOfType<VerticalParallaxLayer>();
            if (layers.Length == 0) return;

            // Furthest first: small parallax factor means far away.
            System.Array.Sort(layers, (a, b) => a.ParallaxFactor.CompareTo(b.ParallaxFactor));

            for (int i = 0; i < layers.Length; i++)
            {
                var layer = layers[i];

                if (cam != null)
                {
                    layer.SetTargetCamera(cam);
                }

                if (autoSortingOrder)
                {
                    var renderer = layer.GetComponent<SpriteRenderer>();
                    if (renderer != null)
                    {
                        renderer.sortingOrder = baseSortingOrder + i * sortingOrderStep;
                    }
                }
            }
        }

        /// <summary>Re-anchors every layer to the camera's current position.</summary>
        [ContextMenu("Reanchor All Layers")]
        public void ReanchorAll()
        {
            foreach (var layer in FindObjectsOfType<VerticalParallaxLayer>())
            {
                if (layer != null) layer.Reanchor();
            }
        }
    }
}