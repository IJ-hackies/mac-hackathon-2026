using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Player.UI
{
    /// Visual-only hover/selection response for any Selectable: eases its RectTransform up to a
    /// slightly larger scale while hovered (mouse) or selected (keyboard/gamepad nav), and back
    /// down on exit/deselect. Attached automatically by UiSfxWirer alongside MenuButtonSfx.
    [DisallowMultipleComponent]
    public sealed class ButtonHoverEffect : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        [SerializeField] private float hoverScale = 1.06f;
        [SerializeField] private float easeSpeed = 12f;

        private RectTransform _rect;
        private Selectable _selectable;
        private Vector3 _baseScale;
        private Vector3 _targetScale;
        private bool _pointerOver;
        private bool _selected;

        private void Awake()
        {
            _rect = transform as RectTransform;
            _selectable = GetComponent<Selectable>();
            _baseScale = _rect != null ? _rect.localScale : Vector3.one;
            _targetScale = _baseScale;
        }

        private void OnDisable()
        {
            _pointerOver = false;
            _selected = false;
            if (_rect != null) _rect.localScale = _baseScale;
            _targetScale = _baseScale;
        }

        private void Update()
        {
            if (_rect == null) return;
            _rect.localScale = Vector3.Lerp(_rect.localScale, _targetScale, 1f - Mathf.Exp(-easeSpeed * Time.unscaledDeltaTime));
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            _pointerOver = true;
            RefreshTarget();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _pointerOver = false;
            RefreshTarget();
        }

        public void OnSelect(BaseEventData eventData)
        {
            _selected = true;
            RefreshTarget();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            _selected = false;
            RefreshTarget();
        }

        private void RefreshTarget()
        {
            bool interactable = _selectable == null || _selectable.interactable;
            bool hovered = interactable && (_pointerOver || _selected);
            _targetScale = hovered ? _baseScale * hoverScale : _baseScale;
        }
    }
}
