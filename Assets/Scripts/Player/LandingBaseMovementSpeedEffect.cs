using Gameplay.Areas;
using UnityEngine;

namespace Player
{
    /// <summary>
    /// Applies the Landing Base movement-speed benefit without coupling locomotion to
    /// perimeter geometry or area discovery.
    /// </summary>
    [DefaultExecutionOrder(100)]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerAreaTracker))]
    public sealed class LandingBaseMovementSpeedEffect : MonoBehaviour
    {
        private const float DefaultSpeedMultiplier = 2f;

        [SerializeField] private PlayerAreaTracker areaTracker;
        [SerializeField] private PlayerController playerController;
        [SerializeField, Min(0f)] private float speedMultiplier = DefaultSpeedMultiplier;

        private bool _subscribed;

        public PlayerAreaTracker AreaTracker => areaTracker;
        public PlayerController PlayerController => playerController;
        public float SpeedMultiplier => speedMultiplier;
        public bool IsApplied { get; private set; }

        private void Awake()
        {
            ResolveDependencies();
        }

        private void OnEnable()
        {
            ResolveDependencies();
            Subscribe();
            ApplyForArea(areaTracker != null ? areaTracker.CurrentArea : null);
        }

        private void OnDisable()
        {
            Unsubscribe();
            RemoveModifier();
        }

        private void OnValidate()
        {
            speedMultiplier = Mathf.Max(0f, speedMultiplier);
            if (Application.isPlaying && IsApplied && playerController != null)
            {
                playerController.SetMovementSpeedModifier(this, speedMultiplier);
            }
        }

        public void Configure(
            PlayerAreaTracker tracker,
            PlayerController controller,
            float landingBaseSpeedMultiplier = DefaultSpeedMultiplier)
        {
            Unsubscribe();
            RemoveModifier();

            areaTracker = tracker;
            playerController = controller;
            speedMultiplier = Mathf.Max(0f, landingBaseSpeedMultiplier);
            ResolveDependencies();

            if (isActiveAndEnabled)
            {
                Subscribe();
                ApplyForArea(areaTracker != null ? areaTracker.CurrentArea : null);
            }
        }

        private void ResolveDependencies()
        {
            if (areaTracker == null)
            {
                areaTracker = GetComponent<PlayerAreaTracker>();
            }

            if (playerController == null)
            {
                playerController = GetComponentInChildren<PlayerController>(true);
            }
        }

        private void Subscribe()
        {
            if (!_subscribed && areaTracker != null)
            {
                areaTracker.AreaChanged += OnAreaChanged;
                _subscribed = true;
            }
        }

        private void Unsubscribe()
        {
            if (_subscribed && areaTracker != null)
            {
                areaTracker.AreaChanged -= OnAreaChanged;
            }

            _subscribed = false;
        }

        private void OnAreaChanged(GameplayArea previous, GameplayArea current)
        {
            ApplyForArea(current);
        }

        private void ApplyForArea(GameplayArea area)
        {
            bool shouldApply = area != null && area.AreaId == GameplayAreaId.LandingBase;
            if (shouldApply && playerController != null)
            {
                playerController.SetMovementSpeedModifier(this, speedMultiplier);
                IsApplied = true;
                return;
            }

            RemoveModifier();
        }

        private void RemoveModifier()
        {
            if (playerController != null)
            {
                playerController.RemoveMovementSpeedModifier(this);
            }

            IsApplied = false;
        }
    }
}
