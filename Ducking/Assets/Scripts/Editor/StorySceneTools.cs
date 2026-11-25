using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace DuckKing.EditorTools
{
    /// <summary>
    /// Generates the story intro scene: a black screen with centred text that
    /// fades lines in and out, then loads the game.
    ///
    /// Built as its own scene so the intro can be re-shot for the trailer without
    /// touching the gameplay scene, and so the title screen only needs one string
    /// changed to point at it.
    /// </summary>
    public static class StorySceneTools
    {
        private const string ScenesFolder = "Assets/Scenes";
        private const string StoryScenePath = ScenesFolder + "/StoryScene.unity";
        private const string GameSceneName = "SampleScene";

        [MenuItem("DuckKing/9. Create Story Intro Scene", false, 9)]
        public static void CreateStoryScene()
        {
            if (File.Exists(StoryScenePath))
            {
                bool overwrite = EditorUtility.DisplayDialog("DuckKing",
                    $"{StoryScenePath} already exists.\nOverwrite it?", "Overwrite", "Cancel");
                if (!overwrite) return;
            }

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera: solid black, so the scene is black even before the overlay draws.
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            camGo.tag = "MainCamera";
            var cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.black;
            cam.orthographic = true;
            camGo.transform.position = new Vector3(0f, 0f, -10f);

            // Canvas
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // Black overlay. raycastTarget off so it never intercepts anything.
            var overlayGo = new GameObject("BlackOverlay", typeof(RectTransform), typeof(Image));
            overlayGo.transform.SetParent(canvasGo.transform, false);
            var overlayRect = overlayGo.GetComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;
            var overlay = overlayGo.GetComponent<Image>();
            overlay.color = Color.black;
            overlay.raycastTarget = false;

            // Story text, centred, generous width so lines wrap readably.
            var textGo = new GameObject("StoryText", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(canvasGo.transform, false);

            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0.5f, 0.5f);
            textRect.anchorMax = new Vector2(0.5f, 0.5f);
            textRect.pivot = new Vector2(0.5f, 0.5f);
            textRect.sizeDelta = new Vector2(1000f, 400f);
            textRect.anchoredPosition = Vector2.zero;

            var storyText = textGo.GetComponent<Text>();
            storyText.fontSize = 38;
            storyText.font = GetSafeFont();
            storyText.color = new Color(1f, 1f, 1f, 0f);
            storyText.alignment = TextAnchor.MiddleCenter;
            storyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            storyText.verticalOverflow = VerticalWrapMode.Overflow;
            storyText.lineSpacing = 1.35f;
            storyText.raycastTarget = false;

            // Controller
            var ctrlGo = new GameObject("StoryController");
            var intro = ctrlGo.AddComponent<DuckKing.Menu.StoryIntro>();

            var so = new SerializedObject(intro);
            so.FindProperty("nextSceneName").stringValue = GameSceneName;
            so.FindProperty("blackOverlay").objectReferenceValue = overlay;
            so.FindProperty("storyText").objectReferenceValue = storyText;
            so.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(ScenesFolder);
            EditorSceneManager.SaveScene(scene, StoryScenePath);
            AddSceneToBuildSettings(StoryScenePath);

            EditorUtility.DisplayDialog("DuckKing",
                "Story scene created at " + StoryScenePath + "\n\n" +
                "Next step:\n" +
                "Open TitleScene, select MenuController, and set\n" +
                "Intro Scene Name = StoryScene\n\n" +
                "Then press Play in TitleScene and click START.",
                "OK");

            Debug.Log($"[DuckKing] Story scene created: {StoryScenePath}. " +
                      "Edit the story lines on the StoryController component.");
        }

        /// <summary>
        /// Points the title screen at the story scene, so no manual wiring is needed.
        /// </summary>
        [MenuItem("DuckKing/10. Link Title Scene To Story", false, 10)]
        public static void LinkTitleToStory()
        {
            string titlePath = ScenesFolder + "/TitleScene.unity";
            if (!File.Exists(titlePath))
            {
                EditorUtility.DisplayDialog("DuckKing",
                    "TitleScene not found. Run \"1. Create Title Scene\" first.", "OK");
                return;
            }

            if (!File.Exists(StoryScenePath))
            {
                EditorUtility.DisplayDialog("DuckKing",
                    "StoryScene not found. Run \"9. Create Story Intro Scene\" first.", "OK");
                return;
            }

            var scene = EditorSceneManager.OpenScene(titlePath, OpenSceneMode.Single);

            var menu = UnityEngine.Object.FindObjectOfType<DuckKing.Menu.MainMenuController>();
            if (menu == null)
            {
                EditorUtility.DisplayDialog("DuckKing",
                    "No MainMenuController found in TitleScene.", "OK");
                return;
            }

            var so = new SerializedObject(menu);
            var prop = so.FindProperty("introSceneName");
            if (prop == null)
            {
                EditorUtility.DisplayDialog("DuckKing",
                    "MainMenuController has no introSceneName field. " +
                    "Make sure the scripts compiled.", "OK");
                return;
            }

            prop.stringValue = "StoryScene";
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("[DuckKing] TitleScene now plays StoryScene before the game.");
            EditorUtility.DisplayDialog("DuckKing",
                "Done. TitleScene -> START now plays the story first.", "OK");
        }

        // ------------------------------------------------------------------

        private static void AddSceneToBuildSettings(string path)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            foreach (var s in scenes)
            {
                if (s.path == path) return;
            }

            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        private static Font GetSafeFont()
        {
            Font font = null;

            try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
            catch { /* older editors may not have it */ }

            if (font == null)
            {
                try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
                catch { /* fall through */ }
            }

            if (font == null)
            {
                Debug.LogWarning("[DuckKing] No builtin font found. Story text may be invisible.");
            }

            return font;
        }
    }
}