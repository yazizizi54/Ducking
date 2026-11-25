using UnityEngine;

namespace DuckKing.UI
{
    /// <summary>
    /// The smallest possible panel switch: one object toggles another on and off.
    ///
    /// Wire PanelOpenToggle.Toggle to a Button's OnClick and that button becomes a
    /// show/hide switch. Nothing else is required.
    /// </summary>
    public class PanelOpenToggle : MonoBehaviour
    {
        [Tooltip("The panel object to show and hide")]
        [SerializeField] private GameObject panel;

        [Tooltip("Start hidden")]
        [SerializeField] private bool hideOnStart = true;

        private void Start()
        {
            if (hideOnStart && panel != null) panel.SetActive(false);
        }

        /// <summary>Flips the panel between visible and hidden.</summary>
        public void Toggle()
        {
            if (panel == null) return;
            panel.SetActive(!panel.activeSelf);
        }

        public void Show() 
        {
            if (panel != null) panel.SetActive(true);
        }

        public void Hide()
        {
            if (panel != null) panel.SetActive(false);
        }
    }
}