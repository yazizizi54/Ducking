using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DuckKing.EditorTools
{
    /// <summary>
    /// Builds a sample shaft layout for a two-player climb.
    ///
    /// The platforms alternate left and right with occasional centre anchors.
    /// That alternation is the point: both ducks want the same next ledge, so the
    /// player who is slightly behind has to either wait or leap at the same time.
    /// A duplicate run clears the previous layout first.
    ///
    /// Layout rules:
    ///  - vertical gap stays inside the jump height with some headroom,
    ///  - horizontal offset stays inside the horizontal reach,
    ///  - widths vary so narrow ledges force a real choice and wide ones give rest.
    /// </summary>
    public static class SampleShaftLayout
    {
        private const string RootName = "ShaftPlatforms";

        // A step is x position and width; height is generated from the step index.
        private struct Step
        {
            public float X;
            public float Width;
            public string Tag;

            public Step(float x, float width, string tag = null)
            {
                X = x;
                Width = width;
                Tag = tag;
            }
        }

        [MenuItem("DuckKing/Level/2. Build Sample Shaft Layout", false, 71)]
        public static void BuildSampleLayout()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                EditorUtility.DisplayDialog("DuckKing", "Save the scene first.", "OK");
                return;
            }

            var existing = GameObject.Find(RootName);
            if (existing != null)
            {
                bool again = EditorUtility.DisplayDialog("DuckKing",
                    "A sample layout already exists.\nReplace it?", "Replace", "Cancel");
                if (!again) return;

                UnityEngine.Object.DestroyImmediate(existing);
            }

            var root = new GameObject(RootName);
            var steps = BuildSteps();

            float plateHeight = 0.4f;
            float spacing = 2.3f;
            float startY = 1.5f;

            var spawned = new List<GameObject>();

            for (int i = 0; i < steps.Count; i++)
            {
                float y = startY + i * spacing;

                var go = CreatePlatform(root.transform, $"Step_{i:00}_{steps[i].Tag}", steps[i].X, y,
                    steps[i].Width, plateHeight);

                spawned.Add(go);
            }

            float topY = startY + steps.Count * spacing;

            // Finish marker on a wide landing at the very top.
            var finish = CreatePlatform(root.transform, "FinishPlatform", 0f, topY, 6f, 0.5f);
            finish.GetComponent<SpriteRenderer>().color = new Color(0.95f, 0.80f, 0.30f);

            var finishZone = new GameObject("FinishZone");
            finishZone.transform.SetParent(root.transform, false);
            finishZone.transform.position = new Vector3(0f, topY + 1f, 0f);

            var zoneCol = finishZone.AddComponent<BoxCollider2D>();
            zoneCol.size = new Vector2(6f, 2f);
            zoneCol.isTrigger = true;
            finishZone.AddComponent<DuckKing.Gameplay.FinishZone>();

            PlaceCoins(root.transform, steps, startY, spacing);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[DuckKing] Sample shaft built: {steps.Count} steps from Y {startY:F1} " +
                      $"to {topY:F1}.\nRun \"Level > 1. Build Vertical Shaft Walls\" if the " +
                      "shaft walls are not there yet.");
        }

        /// <summary>
        /// The actual layout. Alternating sides with a varied rhythm so the two
        /// ducks keep competing for the same ledge instead of running in parallel.
        /// </summary>
        private static List<Step> BuildSteps()
        {
            return new List<Step>
            {
                new Step(-2.5f, 3.0f, "L"),
                new Step( 2.0f, 2.4f, "R"),
                new Step( 0.0f, 3.6f, "C"),
                new Step(-3.0f, 2.2f, "L"),   // narrow left ledge
                new Step( 1.2f, 3.2f, "R"),
                new Step( 3.2f, 2.0f, "R"),   // narrow right ledge
                new Step(-1.6f, 3.0f, "L"),
                new Step(-3.4f, 2.4f, "L"),
                new Step( 0.4f, 4.0f, "C"),   // wide rest ledge
                new Step( 2.8f, 2.2f, "R"),
                new Step(-0.8f, 2.6f, "L"),
                new Step(-3.0f, 2.0f, "L"),   // narrow left ledge
                new Step( 1.8f, 3.0f, "R"),
                new Step( 3.4f, 2.2f, "R"),
                new Step(-1.2f, 3.4f, "L"),
                new Step( 0.0f, 2.6f, "C"),
                new Step(-2.8f, 2.2f, "L"),
                new Step( 2.6f, 2.4f, "R"),
                new Step( 0.6f, 3.0f, "C"),
                new Step(-2.0f, 3.4f, "L"),
            };
        }

        /// <summary>
        /// Coins sit on a subset of ledges. Placing one per ledge would be noise;
        /// spacing them out makes each one a decision instead of a vacuum sweep.
        /// </summary>
        private static void PlaceCoins(Transform parent, List<Step> steps, float startY, float spacing)
        {
            var coinRoot = new GameObject("ShaftCoins");
            coinRoot.transform.SetParent(parent, false);

            // Every third ledge, offset slightly so the coin sits to one side.
            for (int i = 1; i < steps.Count; i += 3)
            {
                float y = startY + i * spacing + 0.8f;
                float x = steps[i].X + (steps[i].Tag == "R" ? -0.4f : 0.4f);
                CreateCoin(coinRoot.transform, x, y);
            }
        }

        private static void CreateCoin(Transform parent, float x, float y)
        {
            var go = new GameObject("Coin");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, y, 0f);
            go.transform.localScale = new Vector3(0.45f, 0.45f, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderArt.Sprite;
            sr.color = new Color(1f, 0.77f, 0.05f, 1f);
            sr.sortingOrder = 5;

            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.5f;

            go.AddComponent<DuckKing.Gameplay.CoinPickup>();
        }

        private static GameObject CreatePlatform(Transform parent, string name, float x, float y,
            float width, float height)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, y, 0f);
            go.transform.localScale = new Vector3(width, height, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderArt.Sprite;
            sr.color = new Color(0.55f, 0.44f, 0.32f);
            sr.sortingOrder = 2;

            var col = go.AddComponent<BoxCollider2D>();
            col.size = Vector2.one;

            return go;
        }
    }
}