using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Player.UI.Progression
{
    /// <summary>
    /// Preserves ScrollRect drag/inertia behavior while turning wheel steps into bounded velocity.
    /// ScrollRect applies that velocity with unscaled time, so station scrolling remains smooth
    /// while the station menu pauses gameplay.
    /// </summary>
    [AddComponentMenu("UI/Smooth Station Scroll Rect")]
    public sealed class SmoothStationScrollRect : ScrollRect
    {
        [SerializeField, Min(0f)] private float wheelVelocityPerTick = 520f;
        [SerializeField, Min(0f)] private float maxWheelVelocity = 1600f;

        public override void OnScroll(PointerEventData eventData)
        {
            if (eventData == null || !IsActive()) return;

            Vector2 delta = eventData.scrollDelta;
            delta.y *= -1f;
            if (vertical && !horizontal)
            {
                if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y)) delta.y = delta.x;
                delta.x = 0f;
            }
            else if (horizontal && !vertical)
            {
                if (Mathf.Abs(delta.y) > Mathf.Abs(delta.x)) delta.x = delta.y;
                delta.y = 0f;
            }

            // Input System wheel values differ by platform. Preserve trackpad fractions while
            // normalizing large Windows wheel ticks to one predictable impulse.
            delta.x = Mathf.Clamp(delta.x, -1f, 1f);
            delta.y = Mathf.Clamp(delta.y, -1f, 1f);
            if (delta.sqrMagnitude <= Mathf.Epsilon) return;

            Vector2 nextVelocity = velocity;
            if (horizontal)
                nextVelocity.x = Mathf.Clamp(nextVelocity.x + delta.x * wheelVelocityPerTick,
                    -maxWheelVelocity, maxWheelVelocity);
            if (vertical)
                nextVelocity.y = Mathf.Clamp(nextVelocity.y + delta.y * wheelVelocityPerTick,
                    -maxWheelVelocity, maxWheelVelocity);
            velocity = nextVelocity;
            eventData.Use();
        }
    }
}
