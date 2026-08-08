using UnityEngine;
using UnityEngine.UI;

namespace Gameplay.Interaction
{
    [DisallowMultipleComponent]
    public sealed class InteractionPromptView : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Text promptText;
        [SerializeField] private string format = "E - INTERACT  {0}";

        public bool IsVisible => root != null && root.activeSelf;

        public void Configure(GameObject targetRoot, Text targetText)
        {
            root = targetRoot;
            promptText = targetText;
        }

        private void Awake() => Hide();

        public void Show(InteractableStation station)
        {
            if (root != null) root.SetActive(true);
            if (promptText != null)
            {
                promptText.text = string.Format(format, station != null ? station.DisplayName : string.Empty);
            }
        }

        public void Hide()
        {
            if (root != null) root.SetActive(false);
        }
    }
}
