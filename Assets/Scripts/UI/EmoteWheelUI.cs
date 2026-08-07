using UnityEngine;
using UnityEngine.UI;

namespace Player.UI
{
    public class EmoteWheelUI : MonoBehaviour
    {
        [SerializeField] private RectTransform root;
        [SerializeField] private Image[] slices;
        [SerializeField] private float deadZoneMagnitude = 0.15f;
        [SerializeField] private Color normalColor = new Color(0.08f, 0.08f, 0.11f, 0.85f);
        [SerializeField] private Color highlightColor = new Color(0.15f, 0.5f, 1f, 0.95f);

        public int HoveredIndex { get; private set; } = -1;

        private void Awake()
        {
            if (root != null)
            {
                root.gameObject.SetActive(false);
            }
        }

        public void Show()
        {
            HoveredIndex = -1;
            if (root != null)
            {
                root.gameObject.SetActive(true);
            }
            ApplyHighlight();
        }

        public void Hide()
        {
            if (root != null)
            {
                root.gameObject.SetActive(false);
            }
            HoveredIndex = -1;
        }

        /// <paramref name="direction"/> is a virtual-joystick offset accumulated from mouse
        /// deltas while the wheel is held open (the cursor itself never unlocks or moves),
        /// roughly in the 0..1 magnitude range. Angle 0 = up, increasing clockwise, matching
        /// how PlayerSceneSetup lays the wedges out.
        public void UpdateHover(Vector2 direction)
        {
            if (slices == null || slices.Length == 0 || direction.magnitude < deadZoneMagnitude)
            {
                HoveredIndex = -1;
            }
            else
            {
                float angle = Mathf.Atan2(direction.x, direction.y) * Mathf.Rad2Deg;
                if (angle < 0f) angle += 360f;
                float sliceSize = 360f / slices.Length;
                HoveredIndex = Mathf.FloorToInt(angle / sliceSize) % slices.Length;
            }

            ApplyHighlight();
        }

        private void ApplyHighlight()
        {
            if (slices == null) return;

            for (int i = 0; i < slices.Length; i++)
            {
                if (slices[i] != null)
                {
                    slices[i].color = i == HoveredIndex ? highlightColor : normalColor;
                }
            }
        }
    }
}
