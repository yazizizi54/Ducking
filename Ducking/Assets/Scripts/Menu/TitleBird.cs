using UnityEngine;
using UnityEngine.UI;

namespace DuckKing.Menu
{
    /// <summary>
    /// One bird flying across the title screen.
    ///
    /// Moves at a constant horizontal speed while cycling through its frames, and
    /// destroys itself once it passes the far edge. Movement is done on the
    /// RectTransform so it composes with the UI layout and needs no physics.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [RequireComponent(typeof(Image))]
    public class TitleBird : MonoBehaviour
    {
        private RectTransform _rect;
        private Image _image;

        private Sprite[] _frames;
        private float _frameTime;
        private float _speedX;
        private float _speedY;
        private float _endX;

        private int _index;
        private float _timer;
        private bool _running;

        /// <summary>
        /// Called by the spawner. Kept explicit instead of relying on Awake order so
        /// the bird is fully configured before its first Update.
        /// </summary>
        public void Initialize(RectTransform rect, Image image, Sprite[] frames,
            float frameTime, float speedX, float speedY, float endX, float margin)
        {
            _rect = rect;
            _image = image;
            _frames = frames;
            _frameTime = Mathf.Max(0.01f, frameTime);
            _speedX = speedX;
            _speedY = speedY;
            _endX = endX;

            if (_frames != null && _frames.Length > 0) _image.sprite = _frames[0];

            _running = true;
        }

        private void Update()
        {
            if (!_running || _rect == null) return;

            Vector2 pos = _rect.anchoredPosition;
            pos.x += _speedX * Time.unscaledDeltaTime;
            pos.y += _speedY * Time.unscaledDeltaTime;
            _rect.anchoredPosition = pos;

            // Frame cycling. Only advances when there is more than one frame, so a
            // single-sprite bird just stays still.
            if (_frames != null && _frames.Length > 1)
            {
                _timer += Time.unscaledDeltaTime;
                if (_timer >= _frameTime)
                {
                    _timer -= _frameTime;
                    _index = (_index + 1) % _frames.Length;
                    if (_image != null) _image.sprite = _frames[_index];
                }
            }

            // Despawn once past the edge it was heading toward.
            bool passedRight = _speedX > 0f && pos.x > _endX;
            bool passedLeft = _speedX < 0f && pos.x < _endX;

            if (passedRight || passedLeft)
            {
                _running = false;
                Destroy(gameObject);
            }
        }
    }
}