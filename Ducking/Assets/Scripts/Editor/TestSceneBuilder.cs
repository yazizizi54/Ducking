using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DuckKing.EditorTools
{
    /// <summary>
    /// Editor entry points for building gameplay scenes.
    ///
    /// The actual level contents live in LevelContentBuilder so the throwaway
    /// playground and the real gameplay scene stay identical.
    ///
    /// All gameplay feel values are plain [SerializeField] fields on the
    /// components, so tuning happens in the Inspector while in Play mode.
    /// </summary>
    public static class TestSceneBuilder
    {
        private const string PlaygroundScenePath = "Assets/Scenes/_Playground.unity";

        [MenuItem("DuckKing/4. Build Test Playground", false, 4)]
        public static void BuildPlayground()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            LevelContentBuilder.BuildAll();
            BuildHudAndResult();

            System.IO.Directory.CreateDirectory("Assets/Scenes");
            EditorSceneManager.SaveScene(scene, PlaygroundScenePath);

            Debug.Log($"[DuckKing] Playground built at {PlaygroundScenePath}. " +
                      "Press Play, select Duck1 or Duck2 to tune values in the Inspector.");
        }

        [MenuItem("DuckKing/6. Fill SampleScene With Level", false, 6)]
        public static void FillGameScene()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

            if (string.IsNullOrEmpty(scene.path))
            {
                EditorUtility.DisplayDialog("DuckKing",
                    "Save the scene first, then run this again.", "OK");
                return;
            }

            if (UnityEngine.Object.FindObjectOfType<DuckKing.Player.DuckController>() != null)
            {
                bool again = EditorUtility.DisplayDialog("DuckKing",
                    "This scene already contains ducks.\n\n" +
                    "Building again will duplicate the level content.\n" +
                    "Use \"8. Rebuild Current Scene Clean\" instead if you want a fresh build.",
                    "Build anyway", "Cancel");
                if (!again) return;
            }

            LevelContentBuilder.BuildAll();
            BuildHudAndResult();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[DuckKing] Level content written into {scene.path}. " +
                      "Press Play, then select Duck1 or Duck2 to tune values.");
        }

        /// <summary>
        /// Wipes every object this toolchain created and rebuilds the scene from
        /// scratch. Prevents the duplicate-Canvas and duplicate-duck mess that
        /// happens when a build menu is run more than once on the same scene.
        /// </summary>
        [MenuItem("DuckKing/8. Rebuild Current Scene Clean", false, 8)]
        public static void RebuildClean()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

            if (string.IsNullOrEmpty(scene.path))
            {
                EditorUtility.DisplayDialog("DuckKing", "Save the scene first.", "OK");
                return;
            }

            bool proceed = EditorUtility.DisplayDialog("DuckKing",
                "This deletes all generated objects (ducks, platforms, coins, HUD, " +
                "manager) and rebuilds them.\n\n" +
                "Any manual edits to those objects will be lost.\n\nContinue?",
                "Rebuild", "Cancel");
            if (!proceed) return;

            string[] generatedRoots =
            {
                "Duck1", "Duck2", "Platforms", "Coins", "GameManager",
                "GameCanvas", "EventSystem", "FinishZone", "DebugTuningPanel"
            };

            foreach (var name in generatedRoots)
            {
                // Walk scene roots rather than GameObject.Find: Find skips inactive
                // objects and the scene may contain duplicates from repeated runs.
                var roots = scene.GetRootGameObjects();
                foreach (var root in roots)
                {
                    if (root != null && root.name == name)
                    {
                        UnityEngine.Object.DestroyImmediate(root);
                    }
                }
            }

            LevelContentBuilder.BuildAll();
            BuildHudAndResult();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log($"[DuckKing] Scene rebuilt clean: {scene.path}. " +
                      "Press Play, then select Duck1 or Duck2 to tune values.");
        }

        private static void BuildHudAndResult()
        {
            // Reuses the same builder as the "5." menu entry so there is exactly
            // one place that defines the HUD layout.
            HudBuilder.BuildHudInternal();
        }
    }
}