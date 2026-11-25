using UnityEditor;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace DuckKing.EditorTools
{
    /// <summary>
    /// Reads the painted tilemaps in the open scene and writes their combined
    /// bounds into the camera, so the Terraria-style edge lock matches the level
    /// exactly without anyone typing four numbers by hand.
    ///
    /// Bounds are taken from the union of every non-empty Tilemap plus any plain
    /// colliders tagged onto the Ground layer, then padded slightly so the camera
    /// never clips the outermost tile.
    /// </summary>
    public static class CameraBoundsTool
    {
        [MenuItem("DuckKing/Camera/1. Fit World Bounds To Level", false, 60)]
        public static void FitBoundsToLevel()
        {
            var follow = UnityEngine.Object.FindObjectOfType<DuckKing.CameraSystem.CameraFollowLower>();
            if (follow == null)
            {
                EditorUtility.DisplayDialog("DuckKing",
                    "No CameraFollowLower found. Is the main camera configured?", "OK");
                return;
            }

            if (!TryComputeBounds(out Bounds bounds, out string source))
            {
                EditorUtility.DisplayDialog("DuckKing",
                    "No painted tiles or colliders found.\n\n" +
                    "Paint some tiles on a Tilemap first, then run this again.", "OK");
                return;
            }

            const float padding = 0.5f;

            float minX = bounds.min.x - padding;
            float maxX = bounds.max.x + padding;
            float minY = bounds.min.y - padding;
            float maxY = bounds.max.y + padding;

            Undo.RecordObject(follow, "Fit Camera World Bounds");
            follow.SetWorldBounds(minX, maxX, minY, maxY);
            EditorUtility.SetDirty(follow);

            Debug.Log(
                $"[DuckKing] Camera world bounds set from {source}:\n" +
                $"  X {minX:F2} .. {maxX:F2}   (width {maxX - minX:F2})\n" +
                $"  Y {minY:F2} .. {maxY:F2}   (height {maxY - minY:F2})\n" +
                "The camera now stops when the screen edge reaches these values.");
        }

        [MenuItem("DuckKing/Camera/2. Report Level Bounds", false, 61)]
        public static void ReportBounds()
        {
            if (!TryComputeBounds(out Bounds bounds, out string source))
            {
                Debug.Log("[DuckKing] No level geometry found.");
                return;
            }

            Debug.Log(
                $"[DuckKing] Level bounds from {source}:\n" +
                $"  X {bounds.min.x:F2} .. {bounds.max.x:F2}\n" +
                $"  Y {bounds.min.y:F2} .. {bounds.max.y:F2}");
        }

        private static bool TryComputeBounds(out Bounds bounds, out string source)
        {
            bounds = new Bounds(Vector3.zero, Vector3.zero);
            source = null;
            bool has = false;
            int tiles = 0;
            int colliders = 0;

            foreach (var tilemap in UnityEngine.Object.FindObjectsOfType<Tilemap>())
            {
                tilemap.CompressBounds();
                if (tilemap.GetUsedTilesCount() == 0) continue;

                // cellBounds is in cell space; localBounds accounts for the Grid cell size.
                Bounds b = tilemap.localBounds;
                Vector3 center = tilemap.transform.TransformPoint(b.center);

                if (!has)
                {
                    bounds = new Bounds(center, b.size);
                    has = true;
                }
                else
                {
                    bounds.Encapsulate(new Bounds(center, b.size));
                }

                tiles += tilemap.GetUsedTilesCount();
            }

            foreach (var col in UnityEngine.Object.FindObjectsOfType<Collider2D>())
            {
                if (col.isTrigger) continue;
                if (col.GetComponent<DuckKing.Player.DuckController>() != null) continue;
                if (col.GetComponentInParent<DuckKing.Player.DuckController>() != null) continue;

                if (!has)
                {
                    bounds = col.bounds;
                    has = true;
                }
                else
                {
                    bounds.Encapsulate(col.bounds);
                }

                colliders++;
            }

            if (!has) return false;

            if (tiles > 0 && colliders > 0) source = $"{tiles} tiles + {colliders} colliders";
            else if (tiles > 0) source = $"{tiles} tiles";
            else source = $"{colliders} colliders";

            return true;
        }
    }
}