using UnityEngine;
using UnityEngine.UI;

namespace DuckKing.Menu
{
    /// <summary>
    /// Credits panel shown over the title screen.
    ///
    /// Content lives in a serialized string so the team can edit the text in the
    /// Inspector without touching code, and defaults are provided so the panel is
    /// never blank if nobody fills it in.
    ///
    /// Supports an optional scrolling animation for longer credit lists.
    /// </summary>
    public class CreditsPanel : MonoBehaviour
    {
        [Header("Root")]
        [Tooltip("Panel object toggled on and off")]
        [SerializeField] private GameObject panelRoot;

        [Header("Content")]
        [SerializeField] private Text creditsText;

        [TextArea(10, 30)]
        [SerializeField]
        private string creditsBody =
            "DUCK KING\n" +
            "\n" +
            "A two-player climbing game\n" +
            "\n" +
            "--- TEAM ---\n" +
            "Programmer    Your Name\n" +
            "Design        Your Name\n" +
            "Art           Your Name\n" +
            "Audio         Your Name\n" +
            "\n" +
            "--- MADE WITH ---\n" +
            "Unity 2022.3\n" +
            "2D Pixel Perfect\n" +
            "\n" +
            "Made in 7 days for the Game Jam\n" +
            "\n" +
            "Thank you for playing!";

        [Header("Buttons")]
        [SerializeField] private Button backButton;

        [Header("Scrolling (optional)")]
        [Tooltip("Slowly scroll the credits upward")]
        [SerializeField] private bool autoScroll = false;

        [Tooltip("Scroll speed in UI units per second")]
        [SerializeField] private float scrollSpeed = 30f;

        [Tooltip("Resets to the bottom when the text scrolls fully past the top")]
        [SerializeField] private bool loopScroll = true;

        private RectTransform _textRect;
        private float _startY;
        private float _viewportHeight;
        private bool _initialized;

        private void Awake()
        {
            // The panel object is disabled in the saved scene, so Awake may only run
            // once Open() activates it. Initialise defensively and retry in Open().
            Initialize();
        }

        /// <summary>
        /// Wires the back button and caches the text rect. Safe to call repeatedly.
        /// </summary>
        private void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            if (creditsText != null)
            {
                creditsText.text = creditsBody;
                _textRect = creditsText.rectTransform;
            }

            if (backButton != null) backButton.onClick.AddListener(Close);
        }

        private void Start()
        {
            // Start runs the first time the object becomes active, i.e. when opened.
            Initialize();
        }

        /// <summary>
        /// Raised whenever the panel hides itself, including via its own Back button.
        /// The menu controller uses it to take the shared dimmer down with it.
        /// </summary>
        public event System.Action Closed;

        public void Open()
        {
            Initialize();

            if (panelRoot != null) panelRoot.SetActive(true);

            if (creditsText != null) creditsText.text = creditsBody;

            // Remember the resting position so scrolling can restart from it.
            if (_textRect != null)
            {
                _startY = _textRect.anchoredPosition.y;
                _viewportHeight = _textRect.parent is RectTransform parentRect
                    ? parentRect.rect.height
                    : 0f;
            }
        }

        public void Close()
        {
            if (panelRoot != null) panelRoot.SetActive(false);

            if (_textRect != null)
            {
                Vector2 p = _textRect.anchoredPosition;
                p.y = _startY;
                _textRect.anchoredPosition = p;
            }

            Closed?.Invoke();
        }

        /// <summary>
        /// Hides the panel without touching the scroll position. Used during startup.
        /// </summary>
        public void HideWithoutSaving()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        public bool IsOpen => panelRoot != null && panelRoot.activeSelf;

        private void Update()
        {
            if (!autoScroll || _textRect == null || !IsOpen) return;

            Vector2 p = _textRect.anchoredPosition;
            p.y += scrollSpeed * Time.unscaledDeltaTime;
            _textRect.anchoredPosition = p;

            if (!loopScroll || _viewportHeight <= 0f) return;

            // Once the text has scrolled its full height past the viewport, wrap.
            float textHeight = _textRect.rect.height;
            if (p.y > _viewportHeight * 0.5f + textHeight)
            {
                p.y = _startY - textHeight;
                _textRect.anchoredPosition = p;
            }
        }

        /// <summary>Lets the menu controller push its own text in at runtime.</summary>
        public void SetCreditsText(string body)
        {
            creditsBody = body;
            if (creditsText != null) creditsText.text = body;
        }
    }
}