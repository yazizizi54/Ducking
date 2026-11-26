using System.Collections;
using System.Text;
using DuckKing.Gameplay;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace DuckKing.UI
{
    /// <summary>
    /// End-of-game movie: the screen fades to black, white credits text scrolls up
    /// from below the frame to above it, then the game returns to the title screen.
    ///
    /// It mirrors the story intro the player already saw when pressing START, so the
    /// start and the ending of the climb feel like the same game. Everything is
    /// built at runtime from a single text field, which means there is nothing in
    /// the scene that can be left un-wired.
    /// </summary>
    [DisallowMultipleComponent]
    public class EndingScroll : MonoBehaviour
    {
        [Header("滚屏文字")]
        [Tooltip("每一行文字。支持 {time} 占位符，播放时替换成通关用时")]
        [TextArea(3, 10)]
        [SerializeField]
        private string[] lines =
        {
            "恭喜登顶",
            "",
            "两只鸭，一条路",
            "谁也没有松手",
            "",
            "通关用时  {time}",
            "",
            "感谢游玩"
        };

        [Header("自动追加")]
        [Tooltip("在滚屏文字最下面自动加一行用时，关掉的话请自己在文字里写 {time}")]
        [SerializeField] private bool appendFinalTime = false;

        [Header("登顶图片")]
        [Tooltip("触碰宝箱后先渐黑、再淡入这张图。留空则跳过图片，直接进滚屏")]
        [SerializeField] private Sprite summitImage;
        [Tooltip("图片显示高度占屏幕高度的比例，宽度按原图比例自动换算")]
        [Range(0.1f, 1f)]
        [SerializeField] private float summitImageHeightRatio = 0.75f;
        [Tooltip("图片最大宽度占屏幕宽度的比例，防止超宽图顶出屏幕")]
        [Range(0.1f, 1f)]
        [SerializeField] private float summitImageWidthRatio = 0.85f;
        [Tooltip("黑屏之后，图片淡入的时长")]
        [SerializeField] private float summitImageFadeIn = 0.8f;
        [Tooltip("图片完全显示的停留时长")]
        [SerializeField] private float summitImageHold = 2.5f;
        [Tooltip("图片淡出到全黑 / 或直接切黑的最大时长；下一段渐黑就是从这里接上")]
        [SerializeField] private float summitImageFadeOut = 0.6f;
        [Tooltip("图片底部的一句小字，留空则不显示。支持 {time}")]
        [SerializeField] private string summitCaption = "";
        [Tooltip("图片是否整体铺满全屏（会裁掉两边），适合先行用一张占位大图看排版")]
        [SerializeField] private bool summitImageStretch = false;

        [Header("时间 (秒)")]
        [Tooltip("触发后先停顿多久再开始变黑，留给玩家看见宝箱")]
        [SerializeField] private float startDelay = 0.6f;
        [Tooltip("黑屏淡入时长")]
        [SerializeField] private float fadeToBlack = 1.0f;
        [Tooltip("滚屏总时长。文字越长这个值要越大，否则会滚得飞快")]
        [SerializeField] private float scrollDuration = 12f;
        [Tooltip("滚完之后黑屏再停留多久")]
        [SerializeField] private float holdAfterScroll = 1.5f;

        [Header("外观")]
        [Tooltip("中文字体。留空会用系统里的微软雅黑；两者都找不到时中文会显示成方块")]
        [SerializeField] private Font font;
        [Tooltip("字号")]
        [SerializeField] private int fontSize = 54;
        [Tooltip("文字颜色（黑屏白字）")]
        [SerializeField] private Color textColor = Color.white;
        [Tooltip("行距倍数")]
        [SerializeField] private float lineSpacing = 1.35f;
        [Tooltip("文字超出屏幕两侧时自动折行")]
        [SerializeField] private bool wrapText = true;

        [Header("行为")]
        [Tooltip("结束后回主界面的场景名。留空则只黑屏不走")]
        [SerializeField] private string menuSceneName = "TitleScene";
        [Tooltip("结束后按空格 / Esc / 回车直接回主界面")]
        [SerializeField] private bool allowSkip = true;

        private Canvas _canvas;
        private Image _black;
        private Image _summit;
        private Text _summitCaption;
        private Image _summitBlackout;
        private Text _text;
        private float _fallbackClock;
        private float _clockAtStart;
        private bool _started;
        private bool _skipRequested;
        private bool _finished;

        private void Awake()
        {
            BuildUI();
        }

        private void Start()
        {
            var gm = SceneGameManager.Instance;
            if (gm != null) gm.OnFinished += HandleFinished;
        }

        private void OnDestroy()
        {
            var gm = SceneGameManager.Instance;
            if (gm != null) gm.OnFinished -= HandleFinished;
        }

        private void Update()
        {
            // The HUD clock is the fallback, so the time line still reads correctly
            // even when the scene never had a SceneGameManager.
            _fallbackClock += Time.unscaledDeltaTime;

            if (!allowSkip || !_started || _finished) return;

            if (UnityEngine.Input.GetKeyDown(KeyCode.Space) ||
                UnityEngine.Input.GetKeyDown(KeyCode.Escape) ||
                UnityEngine.Input.GetKeyDown(KeyCode.Return))
            {
                _skipRequested = true;
            }
        }

        private void HandleFinished(SceneGameManager.Outcome outcome)
        {
            if (_started) return;
            _started = true;
            _clockAtStart = _fallbackClock;
            StartCoroutine(PlayRoutine());
        }

        /// <summary>
        /// Plays the ending by hand. Wired to a Button so the movie can be triggered
        /// without a finish trigger, which is how the layout gets tested in the editor.
        /// </summary>
        public void Play()
        {
            if (_started) return;
            _started = true;
            _clockAtStart = _fallbackClock;
            StartCoroutine(PlayRoutine());
        }

        private IEnumerator PlayRoutine()
        {
            // Gameplay is over: freeze the world before covering the screen so a duck
            // cannot drift behind the credits.
            DisableDuckInput();

            yield return Wait(startDelay);

            // Black first, so the cut into the summit picture is never visible. The
            // player is looking at an empty screen for one beat, which is the same
            // rhythm the story intro opens with.
            yield return FadeBlack(0f, 1f, fadeToBlack, 1f);

            // Interlude: the summit portrait. Skipping jumps straight to the credits.
            if (summitImage != null && !_skipRequested)
            {
                yield return PlaySummitImage();
            }

            _skipRequested = false;

            // Hard cut, not a fade. The picture has to leave before the credits
            // arrive, and any gradient between them would spend its whole length
            // revealing the level behind a half-transparent overlay - which reads as
            // the game flickering back, not as a transition. Cut to solid black, then
            // scroll on black.
            CutToBlack();
            HideSummitImage();

            BuildText();

            yield return ScrollRoutine();

            yield return Wait(holdAfterScroll);

            ReturnToMenu();
        }

        /// <summary>
        /// Fades the summit picture in over black, holds it, then removes it.
        /// The removal is a hard cut rather than a fade: the black plane stays fully
        /// opaque underneath, so the exit never shows the level, and the credits take
        /// over without a gap. The caption is handled together with the picture,
        /// because two independent alphas on the same background read as flicker.
        /// </summary>
        private IEnumerator PlaySummitImage()
        {
            if (_summit == null) yield break;

            LayoutSummitImage();

            _summit.gameObject.SetActive(true);
            if (_summitCaption != null) _summitCaption.gameObject.SetActive(true);

            yield return FadeGraphic(_summit, 0f, 1f, summitImageFadeIn);

            yield return Wait(summitImageHold);

            // The exit is a black plane fading in *on top of* the picture, never the
            // picture's own alpha going down. Lowering the picture's alpha lets the
            // level show through the moment it drops below one, which the player reads
            // as the game cutting back in. Painting black over it keeps the frame fully
            // covered for the whole exit and lands on exactly the black the credits
            // scroll on.
            if (_summitBlackout != null)
            {
                _summitBlackout.gameObject.SetActive(true);

                var fill = _summitBlackout.color;
                fill.a = 0f;
                _summitBlackout.color = fill;

                yield return FadeGraphic(_summitBlackout, 0f, 1f, summitImageFadeOut);

                _summitBlackout.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// Removes the summit picture without animating it. Called after the blackout
        /// plane has already reached full opacity on the frame before.
        /// </summary>
        private void HideSummitImage()
        {
            if (_summit != null)
            {
                var color = _summit.color;
                color.a = 0f;
                _summit.color = color;
                _summit.gameObject.SetActive(false);
            }

            if (_summitCaption == null) return;

            var captionColor = _summitCaption.color;
            captionColor.a = 0f;
            _summitCaption.color = captionColor;
            _summitCaption.gameObject.SetActive(false);
        }

        /// <summary>
        /// Sizes the picture so it fits the screen whichever way its aspect ratio goes.
        /// A wide panorama is limited by width, a tall portrait by height, and neither
        /// ever spills past the edges.
        /// </summary>
        private void LayoutSummitImage()
        {
            if (_summit == null || summitImage == null) return;

            var rect = _summit.rectTransform;
            float viewWidth = _canvas != null && _canvas.transform is RectTransform canvasRect
                ? canvasRect.rect.width
                : Screen.width;
            float viewHeight = _canvas != null && _canvas.transform is RectTransform canvasRect2
                ? canvasRect2.rect.height
                : Screen.height;
            if (viewWidth <= 1f) viewWidth = Screen.width;
            if (viewHeight <= 1f) viewHeight = Screen.height;

            if (summitImageStretch)
            {
                rect.anchorMin = Vector2.zero;
                rect.anchorMax = Vector2.one;
                rect.offsetMin = Vector2.zero;
                rect.offsetMax = Vector2.zero;
                return;
            }

            // Keep the anchors centred and set an explicit size: anchors spanning the
            // canvas would make the ratio in sizeDelta meaningless.
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);

            var spriteRect = summitImage.rect;
            if (spriteRect.width <= 0f || spriteRect.height <= 0f) return;

            float aspect = spriteRect.width / spriteRect.height;
            float maxWidth = viewWidth * summitImageWidthRatio;
            float maxHeight = viewHeight * summitImageHeightRatio;

            float height = maxHeight;
            float width = height * aspect;

            if (width > maxWidth)
            {
                width = maxWidth;
                height = width / aspect;
            }

            rect.sizeDelta = new Vector2(width, height);

            // The caption sits just under the picture so it reads as a label on it.
            if (_summitCaption != null)
            {
                var capRect = _summitCaption.rectTransform;
                capRect.sizeDelta = new Vector2(Mathf.Min(maxWidth, viewWidth * 0.9f), 120f);
                capRect.anchoredPosition = new Vector2(0f, -height * 0.5f - 60f);
            }
        }

        /// <summary>Fades one graphic's alpha, leaving the colour untouched.</summary>
        private IEnumerator FadeGraphic(Graphic graphic, float from, float to, float duration)
        {
            if (graphic == null) yield break;

            var color = graphic.color;
            color.a = from;
            graphic.color = color;

            if (duration <= 0f)
            {
                color.a = to;
                graphic.color = color;
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                color.a = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
                graphic.color = color;

                if (_skipRequested) break;

                yield return null;
            }

            color.a = to;
            graphic.color = color;
        }

        private IEnumerator ScrollRoutine()
        {
            if (_text == null) yield break;

            var rect = _text.rectTransform;

            // Canvas has no rect of its own; the tracked RectTransform is the one that
            // matches the screen for a ScreenSpaceOverlay canvas.
            float viewHeight = _canvas != null && _canvas.transform is RectTransform canvasRect
                ? canvasRect.rect.height
                : Screen.height;
            if (viewHeight <= 1f) viewHeight = Screen.height;

            // Text height comes from the layout pass, which only runs after a frame. The
            // text is still transparent at this point, so the extra frames cost nothing
            // visually and buy a correct measurement.
            yield return null;
            Canvas.ForceUpdateCanvases();
            if (_text.preferredHeight <= 1f)
            {
                yield return null;
                Canvas.ForceUpdateCanvases();
            }

            float textHeight = Mathf.Max(_text.preferredHeight, viewHeight);

            rect.sizeDelta = new Vector2(rect.sizeDelta.x, textHeight);

            // Start fully below the bottom edge and finish fully above the top edge.
            float yStart = -viewHeight * 0.5f - textHeight * 0.5f;
            float yEnd = viewHeight * 0.5f + textHeight * 0.5f;

            SetScrollPosition(rect, yStart);

            // Reveal only after the position is committed, and on the next frame, so
            // the first visible frame of the credits is already the one below the
            // screen rather than whatever position the layout pass left behind.
            yield return null;
            _text.color = WithAlpha(_text.color, textColor.a);

            float t = 0f;
            float duration = Mathf.Max(0.1f, scrollDuration);

            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / duration);
                SetScrollPosition(rect, Mathf.Lerp(yStart, yEnd, p));

                if (_skipRequested) break;

                yield return null;
            }

            SetScrollPosition(rect, yEnd);
        }

        private static void SetScrollPosition(RectTransform rect, float y)
        {
            rect.anchoredPosition = new Vector2(0f, y);
        }

        /// <summary>Fills the Text with the configured lines, substituting {time}.</summary>
        private void BuildText()
        {
            if (_text == null) return;

            string timeText = ResolveTimeText();

            var sb = new StringBuilder();
            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0) sb.Append('\n');
                sb.Append(lines[i]);
            }

            if (appendFinalTime)
            {
                if (sb.Length > 0) sb.Append('\n');
                sb.Append("通关用时  ").Append(timeText);
            }

            // Null-safe, so a half-filled array cannot print "Null" inside the credits.
            string body = sb.ToString();
            _text.text = body.Contains("{time}") ? body.Replace("{time}", timeText) : body;

            // The text object exists since Awake, but the credits must not be visible
            // until ScrollRoutine has moved them below the bottom edge. Filling in the
            // string here would otherwise paint them across the centre of the screen
            // for one frame.
            _text.color = WithAlpha(_text.color, 0f);
        }

        /// <summary>Returns the colour with a new alpha, leaving RGB alone.</summary>
        private static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        private string ResolveTimeText()
        {
            var gm = SceneGameManager.Instance;
            if (gm != null) return gm.ElapsedDisplay;

            return FormatTime(_clockAtStart);
        }

        private static string FormatTime(float t)
        {
            t = Mathf.Max(0f, t);
            int minutes = (int)(t / 60f);
            int seconds = (int)(t % 60f);
            int hundredths = (int)((t * 100f) % 100f);
            return $"{minutes:00}:{seconds:00}.{hundredths:00}";
        }

        private void DisableDuckInput()
        {
            var gm = SceneGameManager.Instance;
            if (gm == null) return;

            var ducks = gm.Ducks;
            for (int i = 0; i < ducks.Count; i++)
            {
                if (ducks[i] != null) ducks[i].InputEnabled = false;
            }
        }

        private IEnumerator FadeBlack(float from, float to, float duration, float delay = 0f)
        {
            if (_black == null) yield break;

            var color = _black.color;

            if (delay <= 0f)
            {
                color.a = from;
                _black.color = color;
            }

            _black.gameObject.SetActive(true);

            if (delay > 0f && from > 0f)
            {
                // Hold the fully black screen for a beat first: fading a transparent
                // overlay up would show the level flashing by underneath.
                color.a = 1f;
                _black.color = color;
                yield return Wait(delay);
            }

            color.a = from;
            _black.color = color;

            if (duration <= 0f)
            {
                color.a = to;
                _black.color = color;
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                color.a = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
                _black.color = color;
                yield return null;
            }

            color.a = to;
            _black.color = color;
        }

        /// <summary>
        /// Swaps the screen to pure black instantly. Both the cut into the puzzle
        /// picture and the cut from picture to scroll use it: a 12-second gradient
        /// between the two reads as mud, an instant cut reads as an edit.
        /// </summary>
        private void CutToBlack()
        {
            if (_black == null) return;

            var color = _black.color;
            color.a = 1f;
            _black.color = color;
            _black.gameObject.SetActive(true);
        }

        /// <summary>Unscaled wait: the ending must run while timeScale is still zero.</summary>
        private IEnumerator Wait(float seconds)
        {
            if (seconds <= 0f) yield break;

            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        private void ReturnToMenu()
        {
            if (_finished) return;
            _finished = true;

            // A frozen game would carry timeScale 0 into the menu and lock it up.
            Time.timeScale = 1f;

            if (string.IsNullOrEmpty(menuSceneName)) return;

            SceneManager.LoadScene(menuSceneName);
        }

        /// <summary>
        /// Builds the black canvas, the black image and the scrolling text.
        ///
        /// The canvas sorts above every other canvas in the scene, so the credits cover
        /// the HUD, the settings panel and the old result screen without any ordering
        /// work in the Inspector.
        /// </summary>
        private void BuildUI()
        {
            if (_canvas != null) return;

            var canvasGo = new GameObject("EndingCanvas", typeof(RectTransform), typeof(Canvas));
            canvasGo.transform.SetParent(transform, false);

            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 300;

            var blackGo = new GameObject("Black", typeof(RectTransform), typeof(Image));
            blackGo.transform.SetParent(canvasGo.transform, false);

            var blackRect = blackGo.GetComponent<RectTransform>();
            blackRect.anchorMin = Vector2.zero;
            blackRect.anchorMax = Vector2.one;
            blackRect.offsetMin = Vector2.zero;
            blackRect.offsetMax = Vector2.zero;

            _black = blackGo.GetComponent<Image>();
            _black.color = new Color(0f, 0f, 0f, 0f);

            // Nothing here may eat clicks: the ending is never interactive, and a
            // full screen raycast target is exactly what froze the menu backdrop once.
            _black.raycastTarget = false;

            var textGo = new GameObject("ScrollText", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(canvasGo.transform, false);

            var rect = textGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(Mathf.Max(100f, Screen.width * 0.8f), 100f);
            rect.anchoredPosition = Vector2.zero;

            _text = textGo.GetComponent<Text>();
            _text.text = string.Empty;
            _text.font = ResolveFont();
            _text.fontSize = fontSize;
            _text.alignment = TextAnchor.UpperCenter;
            _text.lineSpacing = lineSpacing;
            _text.horizontalOverflow = wrapText ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            _text.verticalOverflow = VerticalWrapMode.Overflow;
            _text.raycastTarget = false;

            // Invisible until ScrollRoutine has placed it below the bottom edge. The
            // text object is built during Awake at screen centre, and the layout pass
            // that measures it only happens after the frame is drawn - so without this
            // the credits flash across the middle of the screen for one frame before
            // they jump down and start scrolling. The alpha is raised by the scroll
            // routine on the frame after placement.
            _text.color = WithAlpha(_text.color, 0f);

            // The picture sits above the black plane and below the credits. Built here
            // with no sprite so the slot exists in the Inspector before any art is
            // dropped in; LayoutSummitImage sizes it on arrival.
            var summitGo = new GameObject("SummitImage", typeof(RectTransform), typeof(Image));
            summitGo.transform.SetParent(canvasGo.transform, false);

            var summitRect = summitGo.GetComponent<RectTransform>();
            summitRect.anchorMin = new Vector2(0.5f, 0.5f);
            summitRect.anchorMax = new Vector2(0.5f, 0.5f);
            summitRect.pivot = new Vector2(0.5f, 0.5f);
            summitRect.anchoredPosition = Vector2.zero;

            _summit = summitGo.GetComponent<Image>();
            _summit.sprite = summitImage;
            // preserveAspect keeps a wrong-ratio slot from squashing the art while the
            // picture is faded in, before LayoutSummitImage has a size to go on.
            _summit.preserveAspect = !summitImageStretch;
            _summit.color = new Color(1f, 1f, 1f, 0f);
            _summit.raycastTarget = false;
            summitGo.SetActive(false);

            // Sits directly above the picture. Fading this in is how the picture
            // leaves, so the frame never opens a hole onto the level underneath.
            var blackoutGo = new GameObject("SummitBlackout", typeof(RectTransform), typeof(Image));
            blackoutGo.transform.SetParent(canvasGo.transform, false);

            var blackoutRect = blackoutGo.GetComponent<RectTransform>();
            blackoutRect.anchorMin = Vector2.zero;
            blackoutRect.anchorMax = Vector2.one;
            blackoutRect.offsetMin = Vector2.zero;
            blackoutRect.offsetMax = Vector2.zero;

            _summitBlackout = blackoutGo.GetComponent<Image>();
            _summitBlackout.color = new Color(0f, 0f, 0f, 0f);
            _summitBlackout.raycastTarget = false;
            blackoutGo.SetActive(false);

            var captionGo = new GameObject("SummitCaption", typeof(RectTransform), typeof(Text));
            captionGo.transform.SetParent(canvasGo.transform, false);

            var captionRect = captionGo.GetComponent<RectTransform>();
            captionRect.anchorMin = new Vector2(0.5f, 0.5f);
            captionRect.anchorMax = new Vector2(0.5f, 0.5f);
            captionRect.pivot = new Vector2(0.5f, 0.5f);
            captionRect.anchoredPosition = Vector2.zero;

            _summitCaption = captionGo.GetComponent<Text>();
            _summitCaption.text = summitCaption;
            _summitCaption.font = _text.font;
            _summitCaption.fontSize = Mathf.Max(18, fontSize / 2);
            _summitCaption.color = new Color(textColor.r, textColor.g, textColor.b, 0f);
            _summitCaption.alignment = TextAnchor.MiddleCenter;
            _summitCaption.raycastTarget = false;
            captionGo.SetActive(false);

            // Created last so it renders above the blackout plane: the caption is a
            // label on the picture, so it has to be covered by the exit, not survive it.
            captionGo.transform.SetAsLastSibling();

            blackGo.SetActive(false);
        }

        /// <summary>
        /// Returns the Inspector font, then a system CJK font, then a built-in one.
        ///
        /// Unity's built-in fonts have no Chinese glyphs, so the system lookup matters:
        /// Dynamic fonts load glyphs on demand, which is what makes 微软雅黑 work without
        /// pre-baking a font atlas.
        /// </summary>
        private Font ResolveFont()
        {
            if (font != null) return font;

            string[] systemFonts = { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "SimSun" };

            try
            {
                var installed = Font.GetOSInstalledFontNames();
                for (int i = 0; i < systemFonts.Length; i++)
                {
                    for (int j = 0; j < installed.Length; j++)
                    {
                        if (!string.Equals(installed[j], systemFonts[i])) continue;

                        var osFont = Font.CreateDynamicFontFromOSFont(systemFonts[i], fontSize);
                        if (osFont != null) return osFont;
                    }
                }
            }
            catch
            {
                // Font lookup is best effort; the built-ins below still render Latin.
            }

            try
            {
                Font builtin = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (builtin != null) return builtin;
            }
            catch
            {
                // Older editors expose a different built-in name; fall through.
            }

            return Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
    }
}