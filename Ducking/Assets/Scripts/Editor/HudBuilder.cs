using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace DuckKing.EditorTools
{
    /// <summary>
    /// Generates the in-game HUD (top-centre timer, top-right settings button) and
    /// the result screen Canvas inside the currently open gameplay scene.
    ///
    /// Run this from the scene you actually ship, so the Canvas lives with the
    /// level instead of being duplicated per scene later.
    /// </summary>
    public static class HudBuilder
    {
        [MenuItem("DuckKing/5. Build HUD + Result Screen", false, 5)]
        public static void BuildHud()
        {
            // Fully qualified: UnityEditor.SceneManagement and UnityEngine.SceneManagement
            // both expose a "SceneManager"-like symbol, which would be ambiguous.
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();

            if (string.IsNullOrEmpty(scene.path))
            {
                EditorUtility.DisplayDialog("DuckKing",
                    "Save the current scene first, then run this again.", "OK");
                return;
            }

            if (UnityEngine.Object.FindObjectOfType<DuckKing.UI.GameHud>() != null)
            {
                bool again = EditorUtility.DisplayDialog("DuckKing",
                    "This scene already has a GameHud. Build another one?", "Build anyway", "Cancel");
                if (!again) return;
            }

            BuildHudInternal();
            EditorSceneManager.MarkSceneDirty(scene);

            Debug.Log("[DuckKing] HUD + result screen built. Save the scene (Ctrl+S) to keep it.");
        }

        /// <summary>
        /// Creates the GameCanvas with the timer, settings button and result screen.
        /// Exposed so other scene builders can produce the same layout without
        /// duplicating this code. Does not save the scene.
        /// </summary>
        public static void BuildHudInternal()
        {
            EnsureEventSystem();

            var canvasGo = new GameObject("GameCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            var hud = canvasGo.AddComponent<DuckKing.UI.GameHud>();

            // --- Timer, top centre ---
            var timerGroup = new GameObject("TimerGroup", typeof(RectTransform));
            timerGroup.transform.SetParent(canvasGo.transform, false);
            AnchorTopCenter(timerGroup.GetComponent<RectTransform>(), new Vector2(420f, 96f), new Vector2(0f, -18f));

            var timerLabel = CreateText(timerGroup.transform, "TimerLabel", "TIME", 22,
                new Color(0.85f, 0.86f, 0.92f), TextAnchor.MiddleCenter);
            AnchorTopCenter(timerLabel.rectTransform, new Vector2(420f, 28f), Vector2.zero);

            var timerText = CreateText(timerGroup.transform, "TimerText", "00:00.00", 56,
                Color.white, TextAnchor.MiddleCenter);
            AnchorTopCenter(timerText.rectTransform, new Vector2(420f, 64f), new Vector2(0f, -26f));

            // --- Settings button, top right ---
            var settingsBtn = CreateButton(canvasGo.transform, "SettingsButton", "SETTINGS",
                Vector2.zero, new Vector2(150f, 52f));
            var settingsRect = settingsBtn.GetComponent<RectTransform>();
            settingsRect.anchorMin = new Vector2(1f, 1f);
            settingsRect.anchorMax = new Vector2(1f, 1f);
            settingsRect.pivot = new Vector2(1f, 1f);
            settingsRect.anchoredPosition = new Vector2(-24f, -24f);
            settingsRect.sizeDelta = new Vector2(150f, 52f);

            // --- Settings panel (hidden until the button is pressed) ---
            var backdrop = CreateImage(canvasGo.transform, "SettingsBackdrop", new Color(0f, 0f, 0f, 0.72f));
            Stretch(backdrop.rectTransform);
            // Must never intercept clicks; the panel sits on top of it.
            backdrop.raycastTarget = false;
            backdrop.gameObject.SetActive(false);

            var settingsPanel = new GameObject("SettingsPanel", typeof(RectTransform), typeof(Image));
            settingsPanel.transform.SetParent(canvasGo.transform, false);

            var panelImg = settingsPanel.GetComponent<Image>();
            panelImg.color = new Color(0.12f, 0.14f, 0.20f, 0.98f);

            AnchorCenter(settingsPanel.GetComponent<RectTransform>(), new Vector2(560f, 560f));

            var panelTitle = CreateText(settingsPanel.transform, "Title", "设置", 40,
                new Color(1f, 0.85f, 0.30f), TextAnchor.MiddleCenter);
            AnchorCenter(panelTitle.rectTransform, new Vector2(500f, 60f), new Vector2(0f, 230f));

            // --- Audio rows, matching the title screen ---
            var musicSlider = CreateSliderRow(settingsPanel.transform, "MusicRow", "音乐", 130f,
                out var musicValue);
            var sfxSlider = CreateSliderRow(settingsPanel.transform, "SfxRow", "音效", 50f,
                out var sfxValue);

            var audioSettings = settingsPanel.AddComponent<DuckKing.UI.AudioSettings>();
            var audioSo = new SerializedObject(audioSettings);
            audioSo.FindProperty("musicSlider").objectReferenceValue = musicSlider;
            audioSo.FindProperty("sfxSlider").objectReferenceValue = sfxSlider;
            audioSo.FindProperty("musicValueText").objectReferenceValue = musicValue;
            audioSo.FindProperty("sfxValueText").objectReferenceValue = sfxValue;
            audioSo.ApplyModifiedPropertiesWithoutUndo();

            // --- Buttons: resume and back to the title screen ---
            var resumeBtn = CreateButton(settingsPanel.transform, "ResumeButton", "继续游戏",
                new Vector2(0f, -100f));
            var menuBtnTop = CreateButton(settingsPanel.transform, "MenuButton", "返回主菜单",
                new Vector2(0f, -190f));

            settingsPanel.SetActive(false);

            // --- Result screen ---
            var resultRoot = new GameObject("ResultRoot", typeof(RectTransform));
            resultRoot.transform.SetParent(canvasGo.transform, false);
            Stretch(resultRoot.GetComponent<RectTransform>());

            var dimmer = CreateImage(resultRoot.transform, "Dimmer", new Color(0f, 0f, 0f, 0.72f));
            Stretch(dimmer.rectTransform);

            var panelBg = CreateImage(resultRoot.transform, "Panel", new Color(0.12f, 0.14f, 0.20f, 0.97f));
            AnchorCenter(panelBg.rectTransform, new Vector2(760f, 440f));

            var headline = CreateText(panelBg.transform, "Headline", "RESULT", 48,
                new Color(1f, 0.85f, 0.30f), TextAnchor.MiddleCenter);
            AnchorCenter(headline.rectTransform, new Vector2(700f, 90f), new Vector2(0f, 130f));

            var body = CreateText(panelBg.transform, "Body", "", 34,
                Color.white, TextAnchor.MiddleCenter);
            AnchorCenter(body.rectTransform, new Vector2(700f, 160f), new Vector2(0f, 20f));

            var restartBtn = CreateButton(panelBg.transform, "RestartButton", "PLAY AGAIN", new Vector2(-180f, -140f));
            var menuBtn = CreateButton(panelBg.transform, "MenuButton", "MENU", new Vector2(180f, -140f));

            var resultScreen = canvasGo.AddComponent<DuckKing.UI.ResultScreen>();
            var so = new SerializedObject(resultScreen);
            so.FindProperty("root").objectReferenceValue = resultRoot;
            so.FindProperty("dimmer").objectReferenceValue = dimmer;
            so.FindProperty("headlineText").objectReferenceValue = headline;
            so.FindProperty("bodyText").objectReferenceValue = body;
            so.FindProperty("restartButton").objectReferenceValue = restartBtn;
            so.FindProperty("menuButton").objectReferenceValue = menuBtn;
            so.ApplyModifiedPropertiesWithoutUndo();

            var hudSo = new SerializedObject(hud);
            hudSo.FindProperty("timerText").objectReferenceValue = timerText;
            hudSo.FindProperty("timerLabel").objectReferenceValue = timerLabel;
            hudSo.FindProperty("settingsButton").objectReferenceValue = settingsBtn;
            hudSo.FindProperty("settingsPanel").objectReferenceValue = settingsPanel;
            hudSo.FindProperty("settingsBackdrop").objectReferenceValue = backdrop;
            hudSo.ApplyModifiedPropertiesWithoutUndo();

            // Resume simply closes the panel again.
            var resume = resumeBtn.onClick;
            resume.AddListener(() => hud.SetSettingsVisible(false));

            // Quit to menu from inside the run.
            var quit = menuBtnTop.onClick;
            quit.AddListener(() => hud.ReturnToMenu());

            resultRoot.SetActive(true);
            Selection.activeGameObject = canvasGo;
        }

        private static void EnsureEventSystem()
        {
            if (UnityEngine.Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null) return;

            var go = new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));
            go.name = "EventSystem";
        }

        private static Image CreateImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            return img;
        }

        private static Text CreateText(Transform parent, string name, string content, int size,
            Color color, TextAnchor anchor)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);

            var text = go.GetComponent<Text>();
            text.text = content;
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.font = GetSafeFont();
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;

            return text;
        }

        /// <summary>
        /// Builds one "label - slider - percent" row and returns its slider.
        /// The row is a fixed-height strip anchored to the panel centre.
        /// </summary>
        private static Slider CreateSliderRow(Transform parent, string name, string label,
            float y, out Text valueText)
        {
            var row = new GameObject(name, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            AnchorCenter(row.GetComponent<RectTransform>(), new Vector2(480f, 56f), new Vector2(0f, y));

            var caption = CreateText(row.transform, "Label", label, 30, Color.white, TextAnchor.MiddleLeft);
            var captionRect = caption.rectTransform;
            captionRect.anchorMin = new Vector2(0f, 0f);
            captionRect.anchorMax = new Vector2(0f, 1f);
            captionRect.pivot = new Vector2(0f, 0.5f);
            captionRect.anchoredPosition = Vector2.zero;
            captionRect.sizeDelta = new Vector2(90f, 0f);

            var sliderGo = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            sliderGo.transform.SetParent(row.transform, false);

            var sliderRect = sliderGo.GetComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0f, 0.5f);
            sliderRect.anchorMax = new Vector2(0f, 0.5f);
            sliderRect.pivot = new Vector2(0f, 0.5f);
            sliderRect.anchoredPosition = new Vector2(110f, 0f);
            sliderRect.sizeDelta = new Vector2(290f, 24f);

            // A Slider with no fill/target graphic still drags, but shows nothing.
            var background = CreateImage(sliderGo.transform, "Background", new Color(0.30f, 0.33f, 0.40f, 1f));
            Stretch(background.rectTransform);

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderGo.transform, false);
            var fillAreaRect = fillArea.GetComponent<RectTransform>();
            fillAreaRect.anchorMin = new Vector2(0f, 0f);
            fillAreaRect.anchorMax = new Vector2(1f, 1f);
            fillAreaRect.offsetMin = Vector2.zero;
            fillAreaRect.offsetMax = Vector2.zero;

            var fill = CreateImage(fillArea.transform, "Fill", new Color(0.98f, 0.78f, 0.24f, 1f));
            var fillRect = fill.rectTransform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = new Vector2(1f, 1f);
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;

            var slider = sliderGo.GetComponent<Slider>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.targetGraphic = background;
            slider.fillRect = fillRect;
            slider.handleRect = null;

            valueText = CreateText(row.transform, "Value", "100%", 26,
                new Color(0.85f, 0.86f, 0.92f), TextAnchor.MiddleRight);
            var valueRect = valueText.rectTransform;
            valueRect.anchorMin = new Vector2(1f, 0f);
            valueRect.anchorMax = new Vector2(1f, 1f);
            valueRect.pivot = new Vector2(1f, 0.5f);
            valueRect.anchoredPosition = Vector2.zero;
            valueRect.sizeDelta = new Vector2(80f, 0f);

            return slider;
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 pos,
            Vector2? size = null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);

            var img = go.GetComponent<Image>();
            img.color = new Color(0.98f, 0.78f, 0.24f, 1f);

            AnchorCenter(go.GetComponent<RectTransform>(), size ?? new Vector2(300f, 68f), pos);

            var text = CreateText(go.transform, "Label", label, 30,
                new Color(0.15f, 0.12f, 0.06f), TextAnchor.MiddleCenter);
            Stretch(text.rectTransform);

            return go.GetComponent<Button>();
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void AnchorCenter(RectTransform rt, Vector2 size, Vector2 pos = default)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
        }

        /// <summary>Anchors to the top edge, horizontally centred, growing downward.</summary>
        private static void AnchorTopCenter(RectTransform rt, Vector2 size, Vector2 pos)
        {
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;
        }

        private static Font GetSafeFont()
        {
            Font font = null;

            try { font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
            catch { /* older editors */ }

            if (font == null)
            {
                try { font = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
                catch { /* fall through */ }
            }

            if (font == null)
            {
                Debug.LogWarning("[DuckKing] No builtin font found. Assign fonts manually.");
            }

            return font;
        }
    }
}