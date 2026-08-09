using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Player.UI
{
    /// <summary>Lets the player drag a self-built runtime UI panel to reposition it, persisting
    /// the chosen anchoredPosition to PlayerPrefs (keyed by name) so it stays put across
    /// sessions. Add to the graphic (e.g. the panel's background Image) that should act as the
    /// drag handle; moves a separate target RectTransform (defaults to its own parent) so the
    /// whole panel - not just the handle graphic - follows the drag.</summary>
    [DisallowMultipleComponent]
    public sealed class DraggableUiPanel : MonoBehaviour, IBeginDragHandler, IDragHandler
    {
        [SerializeField] private RectTransform target;
        [SerializeField] private string persistenceKey;
        [SerializeField] private Canvas canvas;

        private Vector2 _dragStartAnchoredPosition;
        private Vector2 _dragStartPointerPosition;
        private bool _configured;

        /// For callers that build this component via AddComponent at runtime rather than wiring
        /// it in the Inspector - SerializedObject/AssetDatabase are Editor-only and unavailable
        /// in builds, so serialized fields created this way must be set through a public method
        /// instead.
        public void Configure(RectTransform dragTarget, string key, Canvas ownerCanvas)
        {
            target = dragTarget;
            persistenceKey = key;
            canvas = ownerCanvas;
            _configured = true;
            LoadPersistedPosition();
        }

        private void Awake()
        {
            if (target == null) target = transform.parent as RectTransform;
            if (canvas == null) canvas = GetComponentInParent<Canvas>();
            if (!_configured) LoadPersistedPosition();
        }

        private void LoadPersistedPosition()
        {
            if (string.IsNullOrEmpty(persistenceKey) || target == null) return;
            float x = PlayerPrefs.GetFloat(persistenceKey + ".x", target.anchoredPosition.x);
            float y = PlayerPrefs.GetFloat(persistenceKey + ".y", target.anchoredPosition.y);
            target.anchoredPosition = new Vector2(x, y);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (target == null) return;
            _dragStartAnchoredPosition = target.anchoredPosition;
            _dragStartPointerPosition = eventData.position;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (target == null) return;

            float scale = canvas != null && canvas.scaleFactor > 0f ? canvas.scaleFactor : 1f;
            Vector2 pointerDelta = (eventData.position - _dragStartPointerPosition) / scale;
            target.anchoredPosition = _dragStartAnchoredPosition + pointerDelta;

            if (!string.IsNullOrEmpty(persistenceKey))
            {
                PlayerPrefs.SetFloat(persistenceKey + ".x", target.anchoredPosition.x);
                PlayerPrefs.SetFloat(persistenceKey + ".y", target.anchoredPosition.y);
            }
        }

        private void OnDisable()
        {
            if (!string.IsNullOrEmpty(persistenceKey)) PlayerPrefs.Save();
        }
    }
}
