using UnityEngine;
using UnityEngine.UI;

namespace Player.UI
{
    public class EmoteWheelUI : MonoBehaviour
    {
        [SerializeField] private RectTransform root;
        [SerializeField] private Sprite ringSprite;
        [SerializeField] private float wheelSize = 340f;
        [SerializeField] private float labelRadius = 130f;
        [SerializeField] private Image[] slices;
        [SerializeField] private float deadZoneMagnitude = 0.15f;
        [SerializeField] private Color normalColor = new Color(0.08f, 0.08f, 0.11f, 0.85f);
        [SerializeField] private Color highlightColor = new Color(0.15f, 0.5f, 1f, 0.95f);

        private string[] _currentLabels;

        public int HoveredIndex { get; private set; } = -1;

        public void SetRingSprite(Sprite sprite)
        {
            ringSprite = sprite;
        }

        private void Awake()
        {
            if (root != null)
            {
                root.gameObject.SetActive(false);
            }
        }

        /// Rebuilds the wheel's slices/labels for the given label set, evenly dividing 360
        /// degrees among them - e.g. the base player uses {"Wave","Yes","No"} (3 wedges), the
        /// Mech ultimate uses {"Wave","Yes","No","Dance"} (4 wedges). Only rebuilds when the
        /// label set actually changed, so repeatedly opening the wheel in the same mode is cheap.
        /// Called both at edit time (PlayerSceneSetup, to build the default 3-slice layout) and
        /// at runtime (PlayerEmoteController, switching label sets on Ultimate activate/end).
        public void Configure(string[] labels)
        {
            if (root == null || labels == null || labels.Length == 0) return;
            if (_currentLabels != null && SameLabels(_currentLabels, labels)) return;

            ClearExistingSlices();

            var newSlices = new Image[labels.Length];
            float sliceDegrees = 360f / labels.Length;

            for (int i = 0; i < labels.Length; i++)
            {
                var sliceRect = CreateChildRect($"Slice_{labels[i]}", new Vector2(wheelSize, wheelSize), Vector2.zero);
                sliceRect.localRotation = Quaternion.Euler(0f, 0f, -(i * sliceDegrees));

                var slice = sliceRect.gameObject.AddComponent<Image>();
                slice.sprite = ringSprite;
                slice.type = Image.Type.Filled;
                slice.fillMethod = Image.FillMethod.Radial360;
                slice.fillOrigin = (int)Image.Origin360.Top;
                slice.fillClockwise = true;
                slice.fillAmount = 1f / labels.Length;
                slice.color = normalColor;
                slice.raycastTarget = false;
                newSlices[i] = slice;

                float midAngle = (i + 0.5f) * sliceDegrees * Mathf.Deg2Rad;
                var labelPos = new Vector2(Mathf.Sin(midAngle) * labelRadius, Mathf.Cos(midAngle) * labelRadius);
                var labelRect = CreateChildRect($"Label_{labels[i]}", new Vector2(100f, 30f), labelPos);

                var text = labelRect.gameObject.AddComponent<Text>();
                text.text = labels[i];
                text.alignment = TextAnchor.MiddleCenter;
                text.color = Color.white;
                text.fontSize = 16;
                text.fontStyle = FontStyle.Bold;
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.raycastTarget = false;
            }

            slices = newSlices;
            _currentLabels = (string[])labels.Clone();
        }

        private void ClearExistingSlices()
        {
            if (slices == null) return;
            foreach (var slice in slices)
            {
                if (slice == null) continue;
                // Slice and its sibling label share the same parent index range - destroy the
                // slice's own GameObject; the matching Label_* is a separate direct child of root
                // found and removed below since slices[] only tracks the Image, not the label.
                if (Application.isPlaying) Destroy(slice.gameObject);
                else DestroyImmediate(slice.gameObject);
            }

            if (root == null) return;
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                if (child != null && child.name.StartsWith("Label_"))
                {
                    if (Application.isPlaying) Destroy(child.gameObject);
                    else DestroyImmediate(child.gameObject);
                }
            }
        }

        private RectTransform CreateChildRect(string name, Vector2 size, Vector2 anchoredPosition)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(root, false);

            var rect = go.GetComponent<RectTransform>();
            var center = new Vector2(0.5f, 0.5f);
            rect.anchorMin = center;
            rect.anchorMax = center;
            rect.pivot = center;
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPosition;
            return rect;
        }

        private static bool SameLabels(string[] a, string[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i]) return false;
            }
            return true;
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
        /// how Configure lays the wedges out.
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
