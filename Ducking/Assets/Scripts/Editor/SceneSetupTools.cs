using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DuckKing.EditorTools
{
    /// <summary>
    /// Editor helpers that build the title screen and the dev scene in one click.
    /// Everything here is editor-only and is excluded from player builds.
    /// </summary>
    public static class SceneSetupTools
    {
        private const string ScenesFolder = "Assets/Scenes";
        private const string TitleScenePath = ScenesFolder + "/TitleScene.unity";
        private const string GameScenePath = ScenesFolder + "/SampleScene.unity";

        [MenuItem("DuckKing/1. Create Title Scene", false, 1)]
        public static void CreateTitleScene()
        {
            if (!AskOverwrite(TitleScenePath)) return;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera
            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.13f, 0.18f, 0.26f, 1f);
            cam.orthographic = true;
            cam.orthographicSize = 5f;
            camGo.transform.position = new Vector3(0f, 0f, -10f);

            // EventSystem
            var esGo = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
            esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            // Canvas
            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280f, 720f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // Title text
            CreateText(canvasGo.transform, "TitleText", "DUCK KING", 120,
                new Vector2(0f, 180f), new Vector2(1100f, 200f), new Color(1f, 0.86f, 0.35f));

            CreateText(canvasGo.transform, "SubtitleText", "Two ducks. One legend. Swapped jump keys.", 28,
                new Vector2(0f, 95f), new Vector2(1100f, 60f), new Color(0.85f, 0.88f, 0.95f));

            // Movement stays with each duck; jump keys are swapped as part of the story.
            CreateText(canvasGo.transform, "HintText",
                "Duck 1: A / D move,  [Up] jump     Duck 2: [Left]/[Right] move,  W jump\n" +
                "The curse swapped your jump keys. Alone, neither duck can climb.",
                22, new Vector2(0f, -170f), new Vector2(1200f, 90f), new Color(1f, 1f, 1f, 0.75f));

            // Buttons. Spread vertically so four entries still read as a clean stack.
            const float buttonStep = 78f;
            const float firstButtonY = 20f;

            Button startBtn = CreateButton(canvasGo.transform, "StartButton", "START",
                new Vector2(0f, firstButtonY));
            Button settingsBtn = CreateButton(canvasGo.transform, "SettingsButton", "SETTINGS",
                new Vector2(0f, firstButtonY - buttonStep));
            Button creditsBtn = CreateButton(canvasGo.transform, "CreditsButton", "CREDITS",
                new Vector2(0f, firstButtonY - buttonStep * 2f));
            Button quitBtn = CreateButton(canvasGo.transform, "QuitButton", "QUIT",
                new Vector2(0f, firstButtonY - buttonStep * 3f));

            // Shared dimmer behind an open panel. Sits under the panels, above the menu.
            var backdrop = CreateImage(canvasGo.transform, "ModalBackdrop",
                new Color(0f, 0f, 0f, 0.72f));
            StretchRect(backdrop.rectTransform);
            // The dimmer only darkens the screen. It must never intercept clicks,
            // otherwise it swallows the button presses on top of it.
            backdrop.raycastTarget = false;
            backdrop.gameObject.SetActive(false);

            var settingsPanel = BuildSettingsPanel(canvasGo.transform);
            var creditsPanel = BuildCreditsPanel(canvasGo.transform);

            // Controller
            var ctrlGo = new GameObject("MenuController");
            var ctrl = ctrlGo.AddComponent<DuckKing.Menu.MainMenuController>();
            var so = new SerializedObject(ctrl);
            so.FindProperty("gameSceneName").stringValue = "SampleScene";
            so.FindProperty("startButton").objectReferenceValue = startBtn;
            so.FindProperty("settingsButton").objectReferenceValue = settingsBtn;
            so.FindProperty("creditsButton").objectReferenceValue = creditsBtn;
            so.FindProperty("quitButton").objectReferenceValue = quitBtn;
            so.FindProperty("settingsPanel").objectReferenceValue = settingsPanel;
            so.FindProperty("creditsPanel").objectReferenceValue = creditsPanel;
            so.FindProperty("modalBackdrop").objectReferenceValue = backdrop.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();

            Directory.CreateDirectory(ScenesFolder);
            EditorSceneManager.SaveScene(scene, TitleScenePath);
            AddSceneToBuildSettings(TitleScenePath, true);

            Debug.Log($"[DuckKing] Title scene created at {TitleScenePath}");
            Selection.activeObject = ctrlGo;
        }

        [MenuItem("DuckKing/2. Add Current Scene to Build Settings", false, 2)]
        public static void AddCurrentToBuild()
        {
            // Fully qualified: UnityEditor.SceneManagement and UnityEngine.SceneManagement
            // both expose a "SceneManager"-like symbol, which would be ambiguous.
            string path = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            if (string.IsNullOrEmpty(path))
            {
                EditorUtility.DisplayDialog("DuckKing", "Save the scene first, then run this.", "OK");
                return;
            }

            AddSceneToBuildSettings(path, false);
            Debug.Log($"[DuckKing] Added to build settings: {path}");
        }

        [MenuItem("DuckKing/3. Apply Pixel Settings (320x180)", false, 3)]
        public static void ApplyPixelSettings()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;

            Debug.Log("[DuckKing] Window defaults set to 1280x720 (4x of 320x180).\n" +
                      "Add the Pixel Perfect Camera component to Main Camera and set " +
                      "Assets PPU = 32, Reference Resolution = 320 x 180.");
        }

        private static bool AskOverwrite(string path)
        {
            if (!File.Exists(path)) return true;

            return EditorUtility.DisplayDialog(
                "DuckKing",
                $"{path} already exists.\nOverwrite it?",
                "Overwrite", "Cancel");
        }

        // ---------------------------------------------------------------------
        // Settings panel

        private static DuckKing.Menu.SettingsPanel BuildSettingsPanel(Transform canvas)
        {
            var panelGo = CreatePanel(canvas, "SettingsPanel", new Vector2(620f, 480f));
            panelGo.SetActive(false);

            CreateText(panelGo.transform, "Title", "SETTINGS", 44,
                new Vector2(0f, 180f), new Vector2(560f, 60f), new Color(1f, 0.86f, 0.35f));

            Slider masterSlider = CreateSlider(panelGo.transform, "MasterSlider", "MASTER VOLUME", 120f);
            Slider musicSlider = CreateSlider(panelGo.transform, "MusicSlider", "MUSIC", 50f);
            Slider sfxSlider = CreateSlider(panelGo.transform, "SfxSlider", "SFX", -20f);

            Toggle fullscreenToggle = CreateToggle(panelGo.transform, "FullscreenToggle", "FULLSCREEN",
                new Vector2(0f, -95f));

            Button resetBtn = CreateButton(panelGo.transform, "ResetButton", "RESET", new Vector2(-150f, -190f), 220f);
            Button backBtn = CreateButton(panelGo.transform, "BackButton", "BACK", new Vector2(150f, -190f), 220f);

            var panel = panelGo.AddComponent<DuckKing.Menu.SettingsPanel>();
            var so = new SerializedObject(panel);
            so.FindProperty("panelRoot").objectReferenceValue = panelGo;
            so.FindProperty("masterSlider").objectReferenceValue = masterSlider;
            so.FindProperty("musicSlider").objectReferenceValue = musicSlider;
            so.FindProperty("sfxSlider").objectReferenceValue = sfxSlider;
            so.FindProperty("masterValueText").objectReferenceValue =
                FindValueLabel(panelGo.transform, "MasterSlider");
            so.FindProperty("musicValueText").objectReferenceValue =
                FindValueLabel(panelGo.transform, "MusicSlider");
            so.FindProperty("sfxValueText").objectReferenceValue =
                FindValueLabel(panelGo.transform, "SfxSlider");
            so.FindProperty("fullscreenToggle").objectReferenceValue = fullscreenToggle;
            so.FindProperty("backButton").objectReferenceValue = backBtn;
            so.FindProperty("resetButton").objectReferenceValue = resetBtn;
            so.ApplyModifiedPropertiesWithoutUndo();

            return panel;
        }

        // ---------------------------------------------------------------------
        // Credits panel

        private static DuckKing.Menu.CreditsPanel BuildCreditsPanel(Transform canvas)
        {
            var panelGo = CreatePanel(canvas, "CreditsPanel", new Vector2(760f, 560f));
            panelGo.SetActive(false);

            CreateText(panelGo.transform, "Title", "CREDITS", 44,
                new Vector2(0f, 235f), new Vector2(700f, 60f), new Color(1f, 0.86f, 0.35f));

            // A masked scroll view keeps long credit lists inside the panel.
            var viewport = CreateImage(panelGo.transform, "Viewport", new Color(0f, 0f, 0f, 0f));
            viewport.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            viewport.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            viewport.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            viewport.rectTransform.sizeDelta = new Vector2(700f, 380f);
            viewport.rectTransform.anchoredPosition = new Vector2(0f, 5f);
            viewport.gameObject.AddComponent<RectMask2D>();

            var contentGo = new GameObject("CreditsText", typeof(RectTransform), typeof(Text));
            contentGo.transform.SetParent(viewport.transform, false);

            var contentRect = contentGo.GetComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0f, 1f);
            contentRect.anchorMax = new Vector2(1f, 1f);
            contentRect.pivot = new Vector2(0.5f, 1f);
            contentRect.sizeDelta = new Vector2(0f, 900f);
            contentRect.anchoredPosition = Vector2.zero;

            var creditsText = contentGo.GetComponent<Text>();
            creditsText.fontSize = 24;
            creditsText.font = GetSafeFont();
            creditsText.alignment = TextAnchor.UpperCenter;
            creditsText.color = Color.white;
            creditsText.lineSpacing = 1.3f;

            Button backBtn = CreateButton(panelGo.transform, "BackButton", "BACK", new Vector2(0f, -235f), 220f);

            var panel = panelGo.AddComponent<DuckKing.Menu.CreditsPanel>();
            var so = new SerializedObject(panel);
            so.FindProperty("panelRoot").objectReferenceValue = panelGo;
            so.FindProperty("creditsText").objectReferenceValue = creditsText;
            so.FindProperty("backButton").objectReferenceValue = backBtn;
            so.ApplyModifiedPropertiesWithoutUndo();

            return panel;
        }

        // ---------------------------------------------------------------------
        // Shared UI helpers

        private static GameObject CreatePanel(Transform canvas, string name, Vector2 size)
        {
            var img = CreateImage(canvas, name, new Color(0.10f, 0.13f, 0.20f, 0.98f));
            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = Vector2.zero;

            return img.gameObject;
        }

        private static Slider CreateSlider(Transform parent, string name, string label, float y)
        {
            var row = new GameObject(name, typeof(RectTransform));
            row.transform.SetParent(parent, false);

            var rowRect = row.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0.5f, 0.5f);
            rowRect.anchorMax = new Vector2(0.5f, 0.5f);
            rowRect.pivot = new Vector2(0.5f, 0.5f);
            rowRect.sizeDelta = new Vector2(540f, 46f);
            rowRect.anchoredPosition = new Vector2(0f, y);

            CreateLabel(row.transform, "Label", label, 22,
                new Vector2(-180f, 0f), new Vector2(220f, 36f), Color.white).alignment = TextAnchor.MiddleLeft;

            // Track background
            var trackBg = CreateImage(row.transform, "Background", new Color(0.2f, 0.24f, 0.32f, 1f));
            trackBg.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            trackBg.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            trackBg.rectTransform.sizeDelta = new Vector2(240f, 12f);
            trackBg.rectTransform.anchoredPosition = new Vector2(30f, 0f);

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(row.transform, false);
            var fillAreaRect = fillArea.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0.5f, 0.5f);
            fillAreaRect.anchorMax = new Vector2(0.5f, 0.5f);
            fillAreaRect.sizeDelta = new Vector2(240f, 12f);
            fillAreaRect.anchoredPosition = new Vector2(30f, 0f);

            var fill = CreateImage(fillArea.transform, "Fill", new Color(0.98f, 0.78f, 0.24f, 1f));
            StretchRect(fill.rectTransform);

            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(row.transform, false);
            var handleAreaRect = handleArea.GetComponent<RectTransform>();
            handleAreaRect.anchorMin = new Vector2(0.5f, 0.5f);
            handleAreaRect.anchorMax = new Vector2(0.5f, 0.5f);
            handleAreaRect.sizeDelta = new Vector2(240f, 30f);
            handleAreaRect.anchoredPosition = new Vector2(30f, 0f);

            var handle = CreateImage(handleArea.transform, "Handle", new Color(1f, 1f, 1f, 1f));
            handle.rectTransform.sizeDelta = new Vector2(18f, 28f);

            var slider = row.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;

            CreateLabel(row.transform, "Value", "100%", 20,
                new Vector2(215f, 0f), new Vector2(90f, 32f), Color.white).alignment = TextAnchor.MiddleRight;

            return slider;
        }

        private static Text FindValueLabel(Transform panel, string sliderName)
        {
            var sliderRoot = panel.Find(sliderName);
            if (sliderRoot == null) return null;

            var valueTransform = sliderRoot.Find("Value");
            return valueTransform != null ? valueTransform.GetComponent<Text>() : null;
        }

        private static Toggle CreateToggle(Transform parent, string name, string label, Vector2 pos)
        {
            var row = new GameObject(name, typeof(RectTransform));
            row.transform.SetParent(parent, false);

            var rowRect = row.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0.5f, 0.5f);
            rowRect.anchorMax = new Vector2(0.5f, 0.5f);
            rowRect.sizeDelta = new Vector2(540f, 40f);
            rowRect.anchoredPosition = pos;

            CreateLabel(row.transform, "Label", label, 22,
                new Vector2(-180f, 0f), new Vector2(220f, 32f), Color.white).alignment = TextAnchor.MiddleLeft;

            var box = CreateImage(row.transform, "Box", new Color(0.2f, 0.24f, 0.32f, 1f));
            box.rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            box.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            box.rectTransform.sizeDelta = new Vector2(28f, 28f);

            var check = CreateImage(box.transform, "Check", new Color(0.98f, 0.78f, 0.24f, 1f));
            var checkRect = check.rectTransform;
            checkRect.anchorMin = new Vector2(0.5f, 0.5f);
            checkRect.anchorMax = new Vector2(0.5f, 0.5f);
            checkRect.sizeDelta = new Vector2(18f, 18f);

            var toggle = row.AddComponent<Toggle>();
            toggle.targetGraphic = box;
            toggle.graphic = check;
            toggle.isOn = true;

            return toggle;
        }

        private static Image CreateImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            return img;
        }

        private static void StretchRect(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static GameObject CreateText(Transform parent, string name, string content, int size,
            Vector2 anchoredPos, Vector2 rectSize, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = rectSize;

            var text = go.GetComponent<Text>();
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            // Legacy Text needs a font. Arial was stripped from newer editors, so try
            // the modern builtin name first and fall back to the old one.
            text.font = GetSafeFont();

            return go;
        }

        /// <summary>
        /// Resolves a usable builtin font without throwing.
        /// A missing font would leave every label invisible, which is a silent
        /// and confusing failure, so this logs loudly instead.
        /// </summary>
        /// <summary>
        /// Same as CreateText but returns the Text component, for callers that
        /// need to tweak alignment or wrapping right after creation.
        /// </summary>
        private static Text CreateLabel(Transform parent, string name, string content, int size,
            Vector2 anchoredPos, Vector2 rectSize, Color color)
        {
            var go = CreateText(parent, name, content, size, anchoredPos, rectSize, color);
            return go.GetComponent<Text>();
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
                Debug.LogWarning("[DuckKing] No builtin font found. UI text may be invisible. " +
                                 "Assign a font manually or import TextMeshPro.");
            }

            return font;
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 anchoredPos,
            float width = 280f)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var rt = go.GetComponent<RectTransform>();
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = new Vector2(width, 64f);

            var img = go.GetComponent<Image>();
            img.color = new Color(0.98f, 0.78f, 0.24f, 1f);

            var btn = go.GetComponent<Button>();

            var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.offsetMin = Vector2.zero;
            textRt.offsetMax = Vector2.zero;

            var text = textGo.GetComponent<Text>();
            text.text = label;
            text.fontSize = 32;
            text.font = GetSafeFont();
            text.alignment = TextAnchor.MiddleCenter;
            text.color = new Color(0.15f, 0.12f, 0.06f, 1f);

            return btn;
        }

        private static void AddSceneToBuildSettings(string path, bool makeFirst)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            bool exists = false;
            foreach (var s in scenes)
            {
                if (s.path == path) { exists = true; break; }
            }

            if (!exists)
            {
                var entry = new EditorBuildSettingsScene(path, true);
                if (makeFirst) scenes.Insert(0, entry);
                else scenes.Add(entry);
                EditorBuildSettings.scenes = scenes.ToArray();
            }
        }
    }
}