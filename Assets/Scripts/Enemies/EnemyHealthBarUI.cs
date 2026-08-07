using Combat;
using UnityEngine;
using UnityEngine.UI;

namespace Enemies
{
    /// World-space bar that floats above an enemy and always faces the camera. Built from
    /// generated UI rects at editor-setup time (same no-external-art approach as HealthHudUI/
    /// CrosshairUI), not parented under the enemy model so the enemy's own rotation/scale can't
    /// distort it - this script drives its position/rotation off the anchor every frame instead.
    public class EnemyHealthBarUI : MonoBehaviour
    {
        [SerializeField] private Transform anchor;
        [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2f, 0f);
        // Fill is resized via RectTransform.anchorMax.x rather than Image.Type.Filled - the
        // sprite-less Filled mesh path was unreliable (bar stayed full-width regardless of
        // health), whereas resizing a plain anchored rect always works.
        [SerializeField] private RectTransform fillRect;
        [SerializeField] private Image fillImage;
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private Color fullColor = new Color(0.15f, 0.9f, 0.25f);
        [SerializeField] private Color lowColor = new Color(1f, 0.2f, 0.15f);
        [SerializeField] private Health health;

        private Camera _mainCamera;

        public void Initialize(Transform followAnchor, Vector3 offset, RectTransform fill, Image fillImg, CanvasGroup group)
        {
            anchor = followAnchor;
            worldOffset = offset;
            fillRect = fill;
            fillImage = fillImg;
            canvasGroup = group;
        }

        // Only stores the reference here - EnemySceneSetup calls this at edit time while
        // building the scene, and a delegate subscribed there wouldn't survive the domain
        // reload on entering Play mode. The actual event subscription happens in OnEnable
        // (runs at real runtime), same pattern as EnemyBase/PlayerDeathHandler's Health.Died.
        public void Bind(Health target)
        {
            health = target;
        }

        private void OnEnable()
        {
            if (health == null) return;
            health.HealthChanged += UpdateFill;
            UpdateFill(health.CurrentHealth, health.MaxHealth);
        }

        private void OnDisable()
        {
            if (health != null) health.HealthChanged -= UpdateFill;
        }

        private void LateUpdate()
        {
            if (anchor == null)
            {
                Destroy(gameObject);
                return;
            }

            transform.position = anchor.position + worldOffset;

            if (_mainCamera == null) _mainCamera = Camera.main;
            if (_mainCamera != null)
            {
                transform.rotation = Quaternion.LookRotation(transform.position - _mainCamera.transform.position);
            }
        }

        // Tied directly to the health value (fires before Health.ApplyDamage's own Died event,
        // same call) rather than a separate Died subscription - guarantees the bar vanishes the
        // instant health hits 0 instead of waiting on the death animation/dissolve to finish.
        private void UpdateFill(float current, float max)
        {
            float fraction = max > 0f ? Mathf.Clamp01(current / max) : 0f;

            if (fillRect != null)
            {
                fillRect.anchorMax = new Vector2(fraction, 1f);
            }

            if (fillImage != null)
            {
                fillImage.color = Color.Lerp(lowColor, fullColor, fraction);
            }

            if (canvasGroup != null)
            {
                canvasGroup.alpha = fraction > 0f ? 1f : 0f;
            }
        }
    }
}
