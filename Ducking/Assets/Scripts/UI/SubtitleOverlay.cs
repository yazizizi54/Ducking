using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DuckKing.UI
{
    /// <summary>
    /// One line of in-game subtitle, drawn as white text with a black outline.
    ///
    /// The overlay builds its own UI at runtime: a Canvas parented to this object, a
    /// Text with an Outline component. That is deliberate. A scene object with five
    /// hand-wired references is five chances to leave one empty, and the usual symptom
    /// is a subtitle that never appears with no error to explain why.
    ///
    /// Nothing here blocks input: every Graphic has raycastTarget off, and the canvas
    /// never takes focus.
    ///
    /// Attach this to any empty GameObject in the gameplay scene. Fill in the lines in
    /// the Inspector; the timing fields mirror the story intro so both feel the same.
    /// </summary>
    [DisallowMultipleComponent]
    public class SubtitleOverlay : MonoBehaviour
    {
        [Header("字幕内容")]
        [Tooltip("每条字幕占一行。留空则什么都不显示")]
        [TextArea(2, 6)]
        [SerializeField]
        private string[] lines =
        {
            "你受到了诅咒",
            "一只鸭无法独自前行",
            "跳跃键被调换了"
        };

        [Header("时间 (秒)")]
        [Tooltip("出现前的等待")]
        [SerializeField] private float delay = 0.6f;
        [Tooltip("每条字幕淡入时长")]
        [SerializeField] private float fadeIn = 0.5f;
        [Tooltip("每条字幕停留时长")]
        [SerializeField] private float holdTime = 2.0f;
        [Tooltip("每条字幕淡出时长")]
        [SerializeField] private float fadeOut = 0.4f;
        [Tooltip("两条字幕之间的间隔")]
        [SerializeField] private float gapBetweenLines = 0.3f;

        [Header("外观")]
        [Tooltip("中文字体资源。留空则用 Unity 内置字体，内置字体没有中文字形，中文会显示成方块")]
        [SerializeField] private Font font;
        [Tooltip("字号")]
        [SerializeField] private int fontSize = 52;
        [Tooltip("文字颜色（白体）")]
        [SerializeField] private Color textColor = Color.white;
        [Tooltip("描边颜色（黑边）")]
        [SerializeField] private Color outlineColor = Color.black;
        [Tooltip("描边厚度，像素")]
        [SerializeField] private Vector2 outlineDistance = new Vector2(3f, -3f);
        [Tooltip("文字距离屏幕底部的像素")]
        [SerializeField] private float bottomOffset = 180f;

        [Header("行为")]
        [Tooltip("只播一次；关掉会在每次 Start 重播")]
        [SerializeField] private bool playOnce = true;
        [Tooltip("按空格或 Esc 跳过当前这条")]
        [SerializeField] private bool allowSkip = false;

        private Canvas _canvas;
        private Text _text;
        private bool _playing;

        private static bool _playedOnce;

        private void Awake()
        {
            BuildUI();
        }

        private void Start()
        {
            if (playOnce && _playedOnce) return;

            _playedOnce = true;
            Play();
        }

        /// <summary>Builds the canvas, text and outline. Idempotent.</summary>
        private void BuildUI()
        {
            if (_text != null) return;

            var canvasGo = new GameObject("SubtitleCanvas", typeof(RectTransform), typeof(Canvas));
            canvasGo.transform.SetParent(transform, false);

            _canvas = canvasGo.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            // Above the HUD so a subtitle is never hidden behind the timer, but still
            // beneath the settings panel while it is open.
            _canvas.sortingOrder = 50;

            var textGo = new GameObject("SubtitleText", typeof(RectTransform), typeof(Text));
            textGo.transform.SetParent(canvasGo.transform, false);

            var rect = textGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, bottomOffset);
            rect.sizeDelta = new Vector2(0f, 120f);

            _text = textGo.GetComponent<Text>();
            _text.text = string.Empty;
            _text.fontSize = fontSize;
            _text.color = WithAlpha(textColor, 0f);
            _text.alignment = TextAnchor.MiddleCenter;
            _text.horizontalOverflow = HorizontalWrapMode.Wrap;
            _text.verticalOverflow = VerticalWrapMode.Overflow;
            _text.raycastTarget = false;
            _text.font = ResolveFont();

            // The outline is a duplicate of the text drawn behind it in black. Effect
            // distance is in UI units, so it stays proportional at 1920 wide.
            var outline = textGo.AddComponent<Outline>();
            outline.effectColor = outlineColor;
            outline.effectDistance = outlineDistance;
            outline.useGraphicAlpha = true;
        }

        /// <summary>
        /// Returns the font assigned in the Inspector, or a built-in fallback.
        ///
        /// The Inspector field is the real answer for Chinese: Unity's built-in fonts are
        /// Latin-only, so CJK text draws as empty boxes no matter which of them is picked.
        /// The fallback exists so English subtitles still work with nothing assigned.
        /// </summary>
        private Font ResolveFont()
        {
            if (font != null) return font;

            try
            {
                Font builtin = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                if (builtin != null) return builtin;
            }
            catch
            {
                // Older editors expose a different name; fall through.
            }

            return Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        /// <summary>Starts the subtitle sequence.</summary>
        public void Play()
        {
            if (_playing) return;
            _playing = true;
            StartCoroutine(PlayRoutine());
        }

        private IEnumerator PlayRoutine()
        {
            yield return Wait(delay);

            for (int i = 0; i < lines.Length; i++)
            {
                if (string.IsNullOrWhiteSpace(lines[i])) continue;

                _text.text = lines[i];

                yield return FadeText(0f, 1f, fadeIn);
                yield return Wait(holdTime);
                yield return FadeText(1f, 0f, fadeOut);

                if (i < lines.Length - 1) yield return Wait(gapBetweenLines);
            }

            _text.text = string.Empty;
            _playing = false;
        }

        private IEnumerator FadeText(float from, float to, float duration)
        {
            if (duration <= 0f)
            {
                SetAlpha(to);
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                SetAlpha(Mathf.Lerp(from, to, Mathf.Clamp01(t / duration)));

                if (allowSkip && SkipPressed())
                {
                    SetAlpha(to);
                    yield break;
                }

                yield return null;
            }

            SetAlpha(to);
        }

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

        private static bool SkipPressed()
        {
            return UnityEngine.Input.GetKeyDown(KeyCode.Space) ||
                   UnityEngine.Input.GetKeyDown(KeyCode.Escape);
        }

        private void SetAlpha(float a)
        {
            if (_text == null) return;
            _text.color = WithAlpha(textColor, a);
        }

        private static Color WithAlpha(Color c, float a)
        {
            c.a = a;
            return c;
        }
    }
}