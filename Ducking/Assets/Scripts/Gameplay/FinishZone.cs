using UnityEngine;

namespace DuckKing.Gameplay
{
    /// <summary>山顶终点触发器。任意一只鸭进入即触发结算。</summary>
    [RequireComponent(typeof(Collider2D))]
    public class FinishZone : MonoBehaviour
    {
        private void Awake()
        {
            var col = GetComponent<Collider2D>();
            col.isTrigger = true;
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.GetComponentInParent<Player.DuckController>() == null) return;

            if (SceneGameManager.Instance != null)
            {
                SceneGameManager.Instance.Finish();
            }
        }
    }
}