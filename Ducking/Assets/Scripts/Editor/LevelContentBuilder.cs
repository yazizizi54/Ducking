using UnityEditor;
using UnityEngine;

namespace DuckKing.EditorTools
{
    /// <summary>
    /// Builds the actual level contents: ducks, camera, platforms, coins, manager.
    ///
    /// Shared by the throwaway playground and by the real gameplay scene, so a
    /// tuning session in one scene produces the same layout as the other. If the
    /// level shape changes, change it here once.
    ///
    /// Editor-only. Runtime gameplay never calls into this.
    /// </summary>
    public static class LevelContentBuilder
    {
        // --- Level shape -----------------------------------------------------
        // Vertical step is 2.4 units against a default jump height of 3.2, which
        // leaves comfortable headroom. Change both together if jump feel moves.
        private const float StepVertical = 2.4f;
        private const float StepWidth = 3.5f;
        private const float StepHeight = 0.5f;

        private static readonly Vector2[] StepPositions =
        {
            new Vector2(-4f, 2.0f), new Vector2(0f, 4.4f), new Vector2(4f, 6.8f),
            new Vector2(0f, 9.2f), new Vector2(-4f, 11.6f), new Vector2(-1f, 14.0f),
            new Vector2(3f, 16.4f), new Vector2(6f, 18.8f), new Vector2(2f, 21.2f),
            new Vector2(-2f, 23.6f), new Vector2(-6f, 26.0f), new Vector2(-3f, 28.4f)
        };

        private static readonly Vector3[] CoinPositions =
        {
            new Vector3(-3f, 2.6f, 0f), new Vector3(0f, 5.0f, 0f), new Vector3(4f, 7.4f, 0f),
            new Vector3(0f, 9.8f, 0f), new Vector3(-4f, 12.2f, 0f), new Vector3(-1f, 14.6f, 0f),
            new Vector3(8.5f, 2.0f, 0f), new Vector3(-8.5f, 2.0f, 0f)
        };

        /// <summary>Height of the finish trigger, derived so it sits above the last step.</summary>
        public static float FinishHeight
        {
            get
            {
                var last = StepPositions[StepPositions.Length - 1];
                return last.y + StepVertical;
            }
        }

        /// <summary>
        /// Builds every gameplay object into the currently open scene.
        /// Returns the finish height so callers can align the manager's fallback.
        /// </summary>
        public static float BuildAll()
        {
            var (duckA, duckB) = BuildDucks();
            BuildCamera(duckA, duckB);
            BuildPlatforms();
            BuildCoins();
            BuildManager(duckA, duckB);

            return FinishHeight;
        }

        // ---------------------------------------------------------------------

        public static (DuckKing.Player.DuckController, DuckKing.Player.DuckController) BuildDucks()
        {
            var a = CreateDuck("Duck1", new Vector3(-3f, 1f, 0f),
                new Color(1f, 0.85f, 0.24f), DuckKing.Input.KeyboardDuckInput.Scheme.PlayerOne);

            var b = CreateDuck("Duck2", new Vector3(3f, 1f, 0f),
                new Color(0.91f, 0.91f, 0.94f), DuckKing.Input.KeyboardDuckInput.Scheme.PlayerTwo);

            return (a, b);
        }

        private static DuckKing.Player.DuckController CreateDuck(string name, Vector3 pos, Color color,
            DuckKing.Input.KeyboardDuckInput.Scheme scheme)
        {
            var go = new GameObject(name);
            go.transform.position = pos;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderArt.Sprite;
            sr.color = color;
            sr.sortingOrder = 10;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;

            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(0.8f, 0.9f);

            var ctrl = go.AddComponent<DuckKing.Player.DuckController>();
            var so = new SerializedObject(ctrl);
            so.FindProperty("inputScheme").enumValueIndex = (int)scheme;
            so.ApplyModifiedPropertiesWithoutUndo();

            return ctrl;
        }

        public static void BuildCamera(DuckKing.Player.DuckController duckA, DuckKing.Player.DuckController duckB)
        {
            // Reuse an existing camera rather than stacking a second one.
            var cam = UnityEngine.Object.FindObjectOfType<Camera>();
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
                camGo.tag = "MainCamera";
                cam = camGo.GetComponent<Camera>();
            }

            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.42f, 0.62f, 0.80f, 1f);
            cam.orthographic = true;

            if (cam.transform.position.z > -1f)
            {
                cam.transform.position = new Vector3(0f, 4f, -10f);
            }

            var follow = cam.GetComponent<DuckKing.CameraSystem.CameraFollowLower>();
            if (follow == null) follow = cam.gameObject.AddComponent<DuckKing.CameraSystem.CameraFollowLower>();

            var so = new SerializedObject(follow);
            so.FindProperty("minHeight").floatValue = 0f;
            so.FindProperty("maxHeight").floatValue = FinishHeight + 20f;
            so.FindProperty("duckA").objectReferenceValue = duckA;
            so.FindProperty("duckB").objectReferenceValue = duckB;

            // The follow script owns the orthographic size at runtime, so set the
            // serialized integer here rather than writing cam.orthographicSize directly.
            var sizeProp = so.FindProperty("orthoSize");
            if (sizeProp != null) sizeProp.intValue = 5;

            var followHz = so.FindProperty("followHorizontally");
            if (followHz != null) followHz.boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void BuildPlatforms()
        {
            var root = new GameObject("Platforms");

            CreatePlatform(root.transform, "Ground", new Vector3(0f, -0.5f, 0f),
                new Vector2(24f, 1f), new Color(0.35f, 0.28f, 0.22f));

            for (int i = 0; i < StepPositions.Length; i++)
            {
                CreatePlatform(root.transform, $"Step_{i:00}",
                    new Vector3(StepPositions[i].x, StepPositions[i].y, 0f),
                    new Vector2(StepWidth, StepHeight), new Color(0.55f, 0.44f, 0.32f));
            }

            var last = StepPositions[StepPositions.Length - 1];
            var finish = new GameObject("FinishZone");
            finish.transform.position = new Vector3(last.x, FinishHeight, 0f);

            var fcol = finish.AddComponent<BoxCollider2D>();
            fcol.size = new Vector2(4f, 2f);
            fcol.isTrigger = true;

            finish.AddComponent<DuckKing.Gameplay.FinishZone>();
        }

        private static void CreatePlatform(Transform parent, string name, Vector3 pos, Vector2 size, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = new Vector3(size.x, size.y, 1f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = PlaceholderArt.Sprite;
            sr.color = color;

            var col = go.AddComponent<BoxCollider2D>();
            col.size = Vector2.one;
        }

        public static void BuildCoins()
        {
            var root = new GameObject("Coins");

            foreach (var p in CoinPositions)
            {
                var go = new GameObject("Coin");
                go.transform.SetParent(root.transform, false);
                go.transform.position = p;
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
        }

        public static void BuildManager(DuckKing.Player.DuckController a, DuckKing.Player.DuckController b)
        {
            var go = new GameObject("GameManager");
            var mgr = go.AddComponent<DuckKing.Gameplay.SceneGameManager>();

            var so = new SerializedObject(mgr);
            var list = so.FindProperty("ducks");
            list.arraySize = 2;
            list.GetArrayElementAtIndex(0).objectReferenceValue = a;
            list.GetArrayElementAtIndex(1).objectReferenceValue = b;
            so.FindProperty("finishHeight").floatValue = FinishHeight;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}