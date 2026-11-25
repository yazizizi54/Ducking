using UnityEditor;
using UnityEngine;

namespace DuckKing.EditorTools
{
    /// <summary>
    /// Turns a plain object into a level platform you can place by hand.
    ///
    /// Workflow once the level is laid out in the Scene view:
    ///   1. Drop objects into a parent called "Platforms".
    ///   2. Select them all, run "Make Selected Platforms".
    ///   3. Move, scale and duplicate them freely; the collider tracks the sprite.
    ///
    /// The collider uses size 1 with auto-sync, so scaling the Transform is enough
    /// to size the platform. This is the fastest way to greybox a level and it
    /// keeps working after real art is dropped in, because nothing depends on the
    /// sprite's pixel dimensions.
    /// </summary>
    public static class LevelKit
    {
        [MenuItem("DuckKing/Platforms/1. Make Selected Platforms", false, 20)]
        public static void MakeSelectedPlatforms()
        {
            var selected = Selection.gameObjects;
            if (selected.Length == 0)
            {
                EditorUtility.DisplayDialog("DuckKing",
                    "Select one or more objects first.", "OK");
                return;
            }

            int converted = 0;
            foreach (var go in selected)
            {
                if (go == null) continue;

                EnsureSpriteRenderer(go);

                var col = go.GetComponent<BoxCollider2D>();
                if (col == null)
                {
                    col = go.AddComponent<BoxCollider2D>();
                }

                // Size 1 means the collider follows the Transform scale, which is
                // how the platforms are sized in the Scene view.
                col.size = Vector2.one;
                col.offset = Vector2.zero;
                col.isTrigger = false;

                int platformLayer = LayerMask.NameToLayer("Ground");
                if (platformLayer >= 0)
                {
                    go.layer = platformLayer;
                }

                converted++;
            }

            Debug.Log($"[DuckKing] Converted {converted} object(s) to platforms.");
        }

        [MenuItem("DuckKing/Platforms/2. Create Platform Here", false, 21)]
        public static void CreatePlatform()
        {
            var go = new GameObject("Platform");
            go.transform.SetParent(FindOrCreateParent("Platforms").transform, false);

            // Place it under the Scene view pivot so it lands where the user is looking.
            var sceneView = SceneView.lastActiveSceneView;
            Vector3 spawnPos = sceneView != null ? sceneView.pivot : Vector3.zero;
            spawnPos.z = 0f;

            go.transform.position = new Vector3(
                Mathf.Round(spawnPos.x * 2f) / 2f,
                Mathf.Round(spawnPos.y * 2f) / 2f,
                0f);

            go.transform.localScale = new Vector3(4f, 0.5f, 1f);

            var sr = EnsureSpriteRenderer(go);
            sr.color = new Color(0.55f, 0.44f, 0.32f, 1f);

            var col = go.AddComponent<BoxCollider2D>();
            col.size = Vector2.one;

            Undo.RegisterCreatedObjectUndo(go, "Create Platform");
            Selection.activeGameObject = go;

            Debug.Log("[DuckKing] Platform created. Drag it in the Scene view, scale to resize.");
        }

        /// <summary>
        /// Duplicates the selection and nudges it up by one jumpable step.
        /// Fastest way to build a climbing path: select the last platform and
        /// run this repeatedly; the path alternates left and right as it climbs.
        /// </summary>
        [MenuItem("DuckKing/Platforms/4. Duplicate Up One Step", false, 22)]
        public static void DuplicateUpOneStep()
        {
            var selected = Selection.gameObjects;
            if (selected.Length == 0)
            {
                EditorUtility.DisplayDialog("DuckKing",
                    "Select a platform first.", "OK");
                return;
            }

            const float stepHeight = 2.4f;
            const float horizontalJitter = 2.5f;

            var created = new System.Collections.Generic.List<GameObject>();

            // Alternate left and right so the path zigzags instead of stacking.
            foreach (var go in selected)
            {
                var copy = UnityEngine.Object.Instantiate(go, go.transform.parent);
                copy.name = go.name + "_copy";

                int index = created.Count;
                float dx = (index % 2 == 0) ? horizontalJitter : -horizontalJitter;

                copy.transform.position = go.transform.position + new Vector3(dx, stepHeight, 0f);
                Undo.RegisterCreatedObjectUndo(copy, "Duplicate Platform Up");

                created.Add(copy);
            }

            Selection.objects = created.ToArray();
            Debug.Log($"[DuckKing] Duplicated {created.Count} platform(s) one step up.");
        }

        [MenuItem("DuckKing/Platforms/5. Create Coin Here", false, 23)]
        public static void CreateCoin()
        {
            var go = new GameObject("Coin");
            go.transform.SetParent(FindOrCreateParent("Coins").transform, false);

            var sceneView = SceneView.lastActiveSceneView;
            Vector3 spawnPos = sceneView != null ? sceneView.pivot : Vector3.zero;

            go.transform.position = new Vector3(
                Mathf.Round(spawnPos.x * 2f) / 2f,
                Mathf.Round(spawnPos.y * 2f) / 2f,
                0f);
            go.transform.localScale = new Vector3(0.45f, 0.45f, 1f);

            var sr = EnsureSpriteRenderer(go);
            sr.color = new Color(1f, 0.77f, 0.05f, 1f);
            sr.sortingOrder = 5;

            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.5f;

            go.AddComponent<DuckKing.Gameplay.CoinPickup>();

            Undo.RegisterCreatedObjectUndo(go, "Create Coin");
            Selection.activeGameObject = go;
        }

        [MenuItem("DuckKing/Platforms/6. List Coin Positions", false, 24)]
        public static void ListCoins()
        {
            var coins = UnityEngine.Object.FindObjectsOfType<DuckKing.Gameplay.CoinPickup>();
            if (coins.Length == 0)
            {
                EditorUtility.DisplayDialog("DuckKing", "No coins found in the scene.", "OK");
                return;
            }

            // Report where coins are so the designer can spot unreachable ones.
            var report = new System.Text.StringBuilder();
            report.AppendLine($"Found {coins.Length} coins:");

            foreach (var coin in coins)
            {
                var p = coin.transform.position;
                report.AppendLine($"  {coin.name}  x={p.x:F1}  y={p.y:F1}");
            }

            Debug.Log("[DuckKing] Coin positions:\n" + report);
        }

        [MenuItem("DuckKing/Platforms/7. Validate Level Reachability", false, 25)]
        public static void ValidateReachability()
        {
            var colliders = UnityEngine.Object.FindObjectsOfType<BoxCollider2D>();
            var platforms = new System.Collections.Generic.List<BoxCollider2D>();

            foreach (var c in colliders)
            {
                if (c.isTrigger) continue;
                if (c.GetComponent<DuckKing.Player.DuckController>() != null) continue;
                platforms.Add(c);
            }

            if (platforms.Count == 0)
            {
                EditorUtility.DisplayDialog("DuckKing", "No platforms found.", "OK");
                return;
            }

            // Sort by height, then check the climb between consecutive platforms.
            platforms.Sort((a, b) => a.bounds.center.y.CompareTo(b.bounds.center.y));

            const float maxJumpHeight = 3.2f;
            const float maxJumpWidth = 5.0f;
            const float tolerance = 0.85f;

            var problems = new System.Text.StringBuilder();
            int gapsTooHigh = 0;
            int gapsTooFar = 0;

            for (int i = 1; i < platforms.Count; i++)
            {
                var lower = platforms[i - 1].bounds;
                var upper = platforms[i].bounds;

                float dy = upper.max.y - lower.max.y;
                float horizontalDistance = Mathf.Abs(upper.center.x - lower.center.x)
                                           - (lower.extents.x + upper.extents.x);

                if (dy > maxJumpHeight * tolerance && dy > 0.01f)
                {
                    gapsTooHigh++;
                    problems.AppendLine(
                        $"  Too high: {platforms[i - 1].name} -> {platforms[i].name}  dy={dy:F2} (max {maxJumpHeight * tolerance:F2})");
                }

                if (horizontalDistance > maxJumpWidth * tolerance)
                {
                    gapsTooFar++;
                    problems.AppendLine(
                        $"  Too far: {platforms[i - 1].name} -> {platforms[i].name}  gap={horizontalDistance:F2} (max {maxJumpWidth * tolerance:F2})");
                }
            }

            if (gapsTooHigh == 0 && gapsTooFar == 0)
            {
                Debug.Log($"[DuckKing] Reachability OK across {platforms.Count} platforms.");
                EditorUtility.DisplayDialog("DuckKing",
                    $"All {platforms.Count} platforms are reachable.", "OK");
                return;
            }

            Debug.LogWarning("[DuckKing] Reachability problems:\n" + problems);
            EditorUtility.DisplayDialog("DuckKing",
                $"{gapsTooHigh} gap(s) too high, {gapsTooFar} gap(s) too far.\n\n" +
                "See Console for the exact pairs.", "OK");
        }

        // ---------------------------------------------------------------------

        private static SpriteRenderer EnsureSpriteRenderer(GameObject go)
        {
            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) sr = go.AddComponent<SpriteRenderer>();

            if (sr.sprite == null)
            {
                sr.sprite = PlaceholderArt.Sprite;
            }

            return sr;
        }

        private static GameObject FindOrCreateParent(string name)
        {
            var existing = GameObject.Find(name);
            if (existing != null) return existing;

            var go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
            return go;
        }
    }
}