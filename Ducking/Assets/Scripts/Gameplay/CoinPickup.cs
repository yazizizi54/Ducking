using System.Collections;
using DuckKing.Player;
using UnityEngine;

namespace DuckKing.Gameplay
{
    /// <summary>
    /// Coin pickup. Score is registered through SceneGameManager so each coin
    /// does not need to know about scoring rules.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class CoinPickup : MonoBehaviour
    {
        [Header("Value")]
        [SerializeField] private int value = 1;

        [Header("Feel")]
        [SerializeField] private float popDuration = 0.12f;
        [SerializeField] private float idleSpinSpeed = 90f;

        private bool _collected;
        private Vector3 _baseScale;

        public int Value => value;

        private void Awake()
        {
            var col = GetComponent<Collider2D>();
            col.isTrigger = true;
            _baseScale = transform.localScale;
        }

        private void Update()
        {
            if (_collected) return;
            transform.Rotate(0f, 0f, idleSpinSpeed * Time.deltaTime);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (_collected) return;

            // The collider may live on a child object, so search upwards.
            var duck = other.GetComponentInParent<DuckController>();
            if (duck == null) return;

            _collected = true;

            if (SceneGameManager.Instance != null)
            {
                int playerIndex = SceneGameManager.Instance.GetDuckIndex(duck);
                if (playerIndex >= 0)
                {
                    SceneGameManager.Instance.AddScore(playerIndex, value);
                }
            }

            StartCoroutine(PopAndDestroy());
        }

        private IEnumerator PopAndDestroy()
        {
            float t = 0f;
            while (t < popDuration)
            {
                t += Time.deltaTime;
                float k = t / popDuration;
                transform.localScale = _baseScale * Mathf.Lerp(1f, 1.6f, k);
                yield return null;
            }

            Destroy(gameObject);
        }
    }
}