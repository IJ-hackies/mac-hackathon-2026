using UnityEngine;

namespace Gameplay.Interaction
{
    public enum StationKind
    {
        Supply,
        SkillTree,
        SpecialShop
    }

    /// <summary>Attach to a trigger volume around a base structure. It owns no UI or economy.</summary>
    [DisallowMultipleComponent]
    public sealed class InteractableStation : MonoBehaviour
    {
        [SerializeField] private StationKind kind;
        [SerializeField] private string displayName = "STATION";
        [SerializeField] private StationInteractionController interactionController;
        [SerializeField, Min(1f)] private float interactionRadius = 8f;

        public StationKind Kind => kind;
        public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? kind.ToString() : displayName;
        public Vector3 InteractionPoint => transform.position;
        public float InteractionRadius => Mathf.Max(1f, interactionRadius);

        public bool IsInRange(Vector3 worldPoint) =>
            (worldPoint - InteractionPoint).sqrMagnitude <= InteractionRadius * InteractionRadius;

        private void OnTriggerEnter(Collider other)
        {
            if (!IsPlayer(other)) return;
            ResolveController()?.EnterRange(this);
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsPlayer(other)) return;
            ResolveController()?.ExitRange(this);
        }

        private StationInteractionController ResolveController()
        {
            if (interactionController == null)
            {
                interactionController = FindFirstObjectByType<StationInteractionController>();
            }
            return interactionController;
        }

        private static bool IsPlayer(Collider other)
        {
            return other != null && other.GetComponentInParent<global::Player.PlayerController>() != null;
        }
    }
}
