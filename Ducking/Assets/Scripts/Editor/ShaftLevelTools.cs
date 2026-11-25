using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DuckKing.EditorTools
{
    /// <summary>
    /// Converts the level into a single vertical shaft with solid walls on both
    /// sides, the way Jump King is laid out.
    ///
    /// In a shaft the camera never needs to pan sideways, so this also switches the
    /// follow camera to a locked X and points its world bounds at the shaft width.
    /// Everything it creates is parented under "ShaftWalls" so it can be deleted or
    /// reshaped by hand afterwards.
    /// </summary>
    public static class ShaftLevelTools
    {
        private const string WallsRootName = "ShaftWalls";

        [MenuItem("DuckKing/Level/1. Build Vertical Shaft Walls", false, 70)]
        public static void BuildShaft()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                EditorUtility.DisplayDialog("DuckKing", "Save the scene first.", "OK");
                return;
            }

            // Reuse the existing walls rather than stacking duplicates on a re-run.
            var existing = GameObject.Find(WallsRootName);
            if (existing != null)
            {
                bool again = EditorUtility.DisplayDialog("DuckKing",
                    "A ShaftWalls object already exists.\nRebuild it?", "Rebuild", "Cancel");
                if (!again) return;

                UnityEngine.Object.DestroyImmediate(existing);
            }

            float halfWidth = 5f;   // shaft is 10 units wide
            float bottom = -2f;
            float top = 60f;

            var root = new GameObject(WallsRootName);

            CreateWall(root.transform, "WallLeft", new Vector3(-halfWidth - 0.5f, (bottom + top) * 0.5f, 0f),
                new Vector2(1f, top - bottom), new Color(0.30f, 0.24f, 0.20f));

            CreateWall(root.transform, "WallRight", new Vector3(halfWidth + 0.5f, (bottom + top) * 0.5f, 0f),
                new Vector2(1f, top - bottom), new Color(0.30f, 0.24f, 0.20f));

            CreateWall(root.transform, "Floor", new Vector3(0f, bottom - 0.5f, 0f),
                new Vector2(halfWidth * 2f + 2f, 1f), new Color(0.30f, 0.24f, 0.20f));

            ConfigureCamera(halfWidth, bottom, top);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[DuckKing] Vertical shaft built: width {halfWidth * 2f} units, " +
                      $"from Y {bottom} to {top}.\n" +
                      "The camera is now fixed horizontally and clamped to the shaft.");
        }

        /// <summary>
        /// Locks the follow camera to the shaft centre and fits its bounds to the walls.
        /// </summary>
        private static void ConfigureCamera(float halfWidth, float bottom, float top)
        {
            var follow = UnityEngine.Object.FindObjectOfType<DuckKing.CameraSystem.CameraFollowLower>();
            if (follow == null)
            {
                Debug.LogWarning("[DuckKing] No CameraFollowLower found; camera not configured.");
                return;
            }

            var so = new SerializedObject(follow);

            // A shaft never pans sideways.
            var followProp = so.FindProperty("followHorizontally");
            if (followProp != null) followProp.boolValue = false;

            var lockedXProp = so.FindProperty("lockedX");
            if (lockedXProp != null) lockedXProp.floatValue = 0f;

            // Bounds describe where the screen edge may reach, so they sit at the walls.
            var useBounds = so.FindProperty("useWorldBounds");
            if (useBounds != null) useBounds.boolValue = true;

            var clampEdge = so.FindProperty("clampByViewEdge");
            if (clampEdge != null) clampEdge.boolValue = true;

            SetFloat(so, "worldMinX", -halfWidth);
            SetFloat(so, "worldMaxX", halfWidth);
            SetFloat(so, "worldMinY", bottom);
            SetFloat(so, "worldMaxY", top);

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(follow);

            Debug.Log("[DuckKing] CameraFollowLower set to horizontal lock with shaft bounds.");
        }

        private static void SetFloat(SerializedObject so, string name, float value)
        {
            var prop = so.FindProperty(name);
            if (prop != null) prop.floatValue = value;
        }

        private static void CreateWall(Transform parent, string name, Vector3 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderArt.Sprite;
            sr.color = color;
            sr.sortingOrder = 1;

            var col = go.AddComponent<BoxCollider2D>();
            col.size = Vector2.one;
        }
    }
}