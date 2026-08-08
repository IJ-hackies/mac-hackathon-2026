using UnityEngine;
using UnityEngine.InputSystem;

namespace Gameplay.Interaction
{
    /// <summary>One-press keyboard interaction gate. Place it on the PlayerRig or HUD root.</summary>
    [DefaultExecutionOrder(900)]
    [DisallowMultipleComponent]
    public sealed class StationInteractionController : MonoBehaviour
    {
        [SerializeField] private StationMenuController stationMenu;
        [SerializeField] private InteractionPromptView prompt;

        private InteractableStation _nearby;
        private InteractableStation[] _stations;
        private bool _menuWasOpen;

        public InteractableStation NearbyStation => _nearby;

        private void Awake()
        {
            if (stationMenu == null) stationMenu = FindFirstObjectByType<StationMenuController>();
            if (prompt == null) prompt = FindFirstObjectByType<InteractionPromptView>();
            RefreshStations();
        }

        private void Update()
        {
            RefreshNearby();

            if (_menuWasOpen && stationMenu != null && !stationMenu.IsOpen && _nearby != null)
            {
                prompt?.Show(_nearby);
            }
            _menuWasOpen = stationMenu != null && stationMenu.IsOpen;

            if (Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame) return;
            TryInteract();
        }

        public bool TryInteract()
        {
            if (_nearby == null || stationMenu == null || stationMenu.IsOpen) return false;
            stationMenu.Open(_nearby);
            // The nearby trigger remains occupied while a console is open, but its prompt must
            // not overlap the station shell. It will be restored after the shell closes.
            if (stationMenu.IsOpen) prompt?.Hide();
            _menuWasOpen = stationMenu.IsOpen;
            return stationMenu.IsOpen;
        }

        public void EnterRange(InteractableStation station)
        {
            if (station == null) return;
            _nearby = station;
            if (stationMenu == null || !stationMenu.IsOpen) prompt?.Show(station);
        }

        public void ExitRange(InteractableStation station)
        {
            if (_nearby != station) return;
            _nearby = null;
            prompt?.Hide();
        }

        public void ClearNearby()
        {
            _nearby = null;
            prompt?.Hide();
        }

        public void RefreshStations()
        {
            _stations = FindObjectsByType<InteractableStation>(FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        }

        public void RefreshNearby()
        {
            if (_stations == null || _stations.Length == 0) RefreshStations();

            InteractableStation closest = null;
            float closestDistance = float.PositiveInfinity;
            if (_stations != null)
            {
                foreach (InteractableStation station in _stations)
                {
                    if (station == null || !station.isActiveAndEnabled || !station.IsInRange(transform.position))
                        continue;

                    float distance = (station.InteractionPoint - transform.position).sqrMagnitude;
                    if (distance >= closestDistance) continue;
                    closest = station;
                    closestDistance = distance;
                }
            }

            if (closest == _nearby) return;
            _nearby = closest;
            if (stationMenu != null && stationMenu.IsOpen) return;
            if (_nearby != null) prompt?.Show(_nearby);
            else prompt?.Hide();
        }
    }
}
