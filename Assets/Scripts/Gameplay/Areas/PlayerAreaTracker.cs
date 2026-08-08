using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gameplay.Areas
{
    /// <summary>
    /// Tracks the area occupied by the one shared astronaut body and publishes transitions
    /// for future gameplay, presentation, and audio consumers.
    /// </summary>
    [DefaultExecutionOrder(200)]
    [DisallowMultipleComponent]
    public sealed class PlayerAreaTracker : MonoBehaviour
    {
        [SerializeField] private Transform trackedBody;
        [SerializeField] private GameplayArea[] areas = Array.Empty<GameplayArea>();
        [SerializeField] private bool discoverAreasWhenEmpty = true;

        public GameplayArea CurrentArea { get; private set; }
        public IReadOnlyList<GameplayArea> Areas => areas;
        public bool DiscoverAreasWhenEmpty => discoverAreasWhenEmpty;
        public Transform TrackedBody => trackedBody;

        public event Action<GameplayArea> AreaEntered;
        public event Action<GameplayArea> AreaExited;
        public event Action<GameplayArea, GameplayArea> AreaChanged;

        private void Awake()
        {
            if (trackedBody == null)
            {
                trackedBody = transform;
            }

            if ((areas == null || areas.Length == 0) && discoverAreasWhenEmpty)
            {
                RefreshAreas();
            }
        }

        private void Start()
        {
            EvaluateCurrentArea();
        }

        private void LateUpdate()
        {
            EvaluateCurrentArea();
        }

        public void Configure(
            Transform body,
            IReadOnlyList<GameplayArea> configuredAreas,
            bool discoverWhenEmpty = true)
        {
            trackedBody = body != null ? body : transform;
            areas = configuredAreas == null
                ? Array.Empty<GameplayArea>()
                : CopyUniqueAreas(configuredAreas);
            discoverAreasWhenEmpty = discoverWhenEmpty;
        }

        public void RefreshAreas()
        {
            GameplayArea[] discovered = FindObjectsByType<GameplayArea>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            areas = CopyUniqueAreas(discovered);
        }

        public GameplayArea EvaluateCurrentArea()
        {
            Transform body = trackedBody != null ? trackedBody : transform;
            Vector3 position = body.position;
            GameplayArea candidate = FindBestExactArea(position);

            if (candidate == null &&
                CurrentArea != null &&
                CurrentArea.isActiveAndEnabled &&
                CurrentArea.ContainsWithExitPadding(position))
            {
                candidate = CurrentArea;
            }

            SetCurrentArea(candidate);
            return CurrentArea;
        }

        private GameplayArea FindBestExactArea(Vector3 position)
        {
            GameplayArea best = null;
            if (areas == null)
            {
                return null;
            }

            for (int index = 0; index < areas.Length; index++)
            {
                GameplayArea area = areas[index];
                if (area == null || !area.isActiveAndEnabled || !area.IsValid)
                {
                    continue;
                }

                if (!area.Contains(position) || !ShouldPrefer(area, best))
                {
                    continue;
                }

                best = area;
            }

            return best;
        }

        private void SetCurrentArea(GameplayArea next)
        {
            if (CurrentArea == next)
            {
                return;
            }

            GameplayArea previous = CurrentArea;
            if (previous != null)
            {
                AreaExited?.Invoke(previous);
            }

            CurrentArea = next;
            if (next != null)
            {
                AreaEntered?.Invoke(next);
            }

            AreaChanged?.Invoke(previous, next);
        }

        private static bool ShouldPrefer(GameplayArea candidate, GameplayArea currentBest)
        {
            if (currentBest == null || candidate.Priority > currentBest.Priority)
            {
                return true;
            }

            return candidate.Priority == currentBest.Priority &&
                   candidate.AreaId < currentBest.AreaId;
        }

        private static GameplayArea[] CopyUniqueAreas(IReadOnlyList<GameplayArea> source)
        {
            var unique = new List<GameplayArea>(source.Count);
            for (int index = 0; index < source.Count; index++)
            {
                GameplayArea area = source[index];
                if (area != null && !unique.Contains(area))
                {
                    unique.Add(area);
                }
            }

            return unique.ToArray();
        }
    }
}
