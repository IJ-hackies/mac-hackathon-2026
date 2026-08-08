using Audio;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Player.UI
{
    /// Plays a click cue on press, for any Selectable (Button, Toggle, Slider). Attached
    /// automatically by UiSfxWirer rather than hand-placed on every button, so new menu buttons
    /// pick up sound for free. Hover no longer plays a sound - see ButtonHoverEffect for the
    /// visual-only hover response.
    [DisallowMultipleComponent]
    public sealed class MenuButtonSfx : MonoBehaviour, IPointerClickHandler, ISubmitHandler
    {
        [SerializeField] private SfxId clickSfx = SfxId.UiClick;

        private Selectable _selectable;

        private void Awake()
        {
            _selectable = GetComponent<Selectable>();
        }

        public void OnPointerClick(PointerEventData eventData) => PlayClick();

        public void OnSubmit(BaseEventData eventData) => PlayClick();

        private void PlayClick()
        {
            if (!IsInteractable()) return;
            AudioManager.Instance.PlaySfx(clickSfx);
        }

        private bool IsInteractable() => _selectable == null || _selectable.interactable;
    }
}
