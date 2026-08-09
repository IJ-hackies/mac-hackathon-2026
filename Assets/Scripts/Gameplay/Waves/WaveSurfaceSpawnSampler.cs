using System;
using System.Collections.Generic;
using UnityEngine;
using Gameplay.Areas;

namespace Gameplay.Waves
{
    /// <summary>
    /// Conservative, radial-space bounds for a complete spawned enemy hierarchy.  Bounds are derived
    /// from every collider and CharacterController, including inactive boss stages, before spawning.
    /// </summary>
    public struct WaveSpawnFootprint
    {
        private const float MinimumRadius = .25f;

        public Vector3 LocalCenter { get; private set; }
        public Vector3 LocalExtents { get; private set; }
        public float TangentRadius { get; private set; }
        public float InwardExtent { get; private set; }
        public float OutwardExtent { get; private set; }
        public float EnclosingRadius { get; private set; }
        public int ShapeCount { get; private set; }

        public static WaveSpawnFootprint FromPrefab(GameObject prefab, float rootScaleMultiplier)
        {
            return prefab == null
                ? Default(rootScaleMultiplier)
                : FromRoot(prefab.transform, rootScaleMultiplier, false);
        }

        public static WaveSpawnFootprint FromSpawnedRoot(Transform root)
        {
            return root == null ? Default(1f) : FromRoot(root, 1f, true);
        }

        public Vector3 WorldCenter(Vector3 rootPosition, Quaternion rootRotation)
        {
            return rootPosition + rootRotation * LocalCenter;
        }

        public static Vector3 AverageRadialDirection(IReadOnlyList<Vector3> polePositions, Vector3 planetCenter)
        {
            if (polePositions == null || polePositions.Count == 0) return Vector3.zero;
            Vector3 sum = Vector3.zero;
            for (int index = 0; index < polePositions.Count; index++)
            {
                Vector3 radial = polePositions[index] - planetCenter;
                if (radial.sqrMagnitude > .0001f) sum += radial.normalized;
            }
            return sum.sqrMagnitude > .0001f ? sum.normalized : Vector3.zero;
        }

        private static WaveSpawnFootprint FromRoot(Transform root, float scaleMultiplier, bool useLossyRootScale)
        {
            Vector3 rootScale = useLossyRootScale ? Abs(root.lossyScale) : Abs(root.localScale) * Mathf.Max(.0001f, scaleMultiplier);
            Bounds combined = default;
            int shapeCount = 0;
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int index = 0; index < colliders.Length; index++)
            {
                if (!TryGetLocalBounds(colliders[index], out Bounds bounds)) continue;
                EncapsulateRootLocalBounds(root, bounds, ref combined, ref shapeCount);
            }
            CharacterController[] controllers = root.GetComponentsInChildren<CharacterController>(true);
            for (int index = 0; index < controllers.Length; index++)
            {
                CharacterController controller = controllers[index];
                float radius = Mathf.Max(0f, controller.radius);
                float halfHeight = Mathf.Max(radius, controller.height * .5f);
                Bounds bounds = new Bounds(controller.center, new Vector3(radius * 2f, halfHeight * 2f, radius * 2f));
                EncapsulateRootLocalBounds(controller.transform, bounds, ref combined, ref shapeCount, root);
            }

            if (shapeCount == 0) return Default(scaleMultiplier);
            Vector3 scaledCenter = Vector3.Scale(combined.center, rootScale);
            Vector3 scaledExtents = Vector3.Scale(combined.extents, rootScale);
            float tangentRadius = Mathf.Sqrt(
                Mathf.Pow(Mathf.Abs(scaledCenter.x) + scaledExtents.x, 2f) +
                Mathf.Pow(Mathf.Abs(scaledCenter.z) + scaledExtents.z, 2f));
            float enclosingRadius = scaledExtents.magnitude;
            return new WaveSpawnFootprint
            {
                LocalCenter = scaledCenter,
                LocalExtents = scaledExtents,
                TangentRadius = Mathf.Max(MinimumRadius, tangentRadius),
                InwardExtent = Mathf.Max(0f, -(scaledCenter.y - scaledExtents.y)),
                OutwardExtent = Mathf.Max(0f, scaledCenter.y + scaledExtents.y),
                EnclosingRadius = Mathf.Max(MinimumRadius, enclosingRadius),
                ShapeCount = shapeCount
            };
        }

        private static void EncapsulateRootLocalBounds(Transform root, Bounds bounds, ref Bounds combined, ref int shapeCount)
        {
            EncapsulateRootLocalBounds(root, bounds, ref combined, ref shapeCount, root);
        }

        private static void EncapsulateRootLocalBounds(Transform shapeTransform, Bounds bounds, ref Bounds combined, ref int shapeCount, Transform root)
        {
            Vector3 extents = bounds.extents;
            bool hasBounds = shapeCount > 0;
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
            {
                Vector3 corner = shapeTransform.TransformPoint(bounds.center + Vector3.Scale(extents, new Vector3(x, y, z)));
                Vector3 rootLocal = root.InverseTransformPoint(corner);
                if (!hasBounds) { combined = new Bounds(rootLocal, Vector3.zero); hasBounds = true; }
                else combined.Encapsulate(rootLocal);
            }
            shapeCount++;
        }

        private static bool TryGetLocalBounds(Collider collider, out Bounds bounds)
        {
            // Trigger-only hit volumes (notably the mech's leg damage trigger) should not
            // reserve terrain clearance. They can be substantially larger than the body.
            if (collider == null || collider.isTrigger)
            {
                bounds = default;
                return false;
            }

            switch (collider)
            {
                case BoxCollider box:
                    bounds = new Bounds(box.center, box.size);
                    return true;
                case SphereCollider sphere:
                    bounds = new Bounds(sphere.center, Vector3.one * sphere.radius * 2f);
                    return true;
                case CapsuleCollider capsule:
                    float radius = capsule.radius;
                    float halfHeight = Mathf.Max(radius, capsule.height * .5f);
                    Vector3 size = Vector3.one * radius * 2f;
                    size[capsule.direction] = halfHeight * 2f;
                    bounds = new Bounds(capsule.center, size);
                    return true;
                case MeshCollider mesh when mesh.sharedMesh != null:
                    bounds = mesh.sharedMesh.bounds;
                    return true;
                default:
                    bounds = default;
                    return false;
            }
        }

        private static WaveSpawnFootprint Default(float scale)
        {
            float radius = Mathf.Max(MinimumRadius, Mathf.Abs(scale) * MinimumRadius);
            return new WaveSpawnFootprint
            {
                LocalCenter = Vector3.up * radius,
                LocalExtents = Vector3.one * radius,
                TangentRadius = radius,
                InwardExtent = 0f,
                OutwardExtent = radius * 2f,
                EnclosingRadius = radius,
                ShapeCount = 0
            };
        }

        private static Vector3 Abs(Vector3 value) => new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    }

    /// <summary>Planet-relative sampler. It keeps prospective enemy bounds clear of terrain props and live enemies.</summary>
    [Serializable]
    public sealed class WaveSurfaceSpawnSampler
    {
        private const float SpawnGap = .2f;
        private const int OverlapBufferSize = 64;
        private const int SurfaceHitBufferSize = 64;

        [SerializeField] private Transform planetCenter;
        [SerializeField] private LayerMask surfaceMask = ~0;
        [SerializeField, Min(24f)] private float minimumDistance = 24f;
        [SerializeField, Min(24f)] private float maximumDistance = 45f;
        [SerializeField, Min(1)] private int attempts = 24;
        [SerializeField, Min(0f)] private float areaClearance = 5f;
        [SerializeField, Min(0f)] private float rayStartOffset = 30f;

        [NonSerialized] private Collider[] overlapBuffer;
        [NonSerialized] private RaycastHit[] surfaceHitBuffer;
        [NonSerialized] private float nextArenaFailureLogAt;
        [NonSerialized] private int lastSurfaceRayHitCount;
        [NonSerialized] private int lastSurfaceNormalRejectCount;
        [NonSerialized] private int lastSurfaceRadiusRejectCount;
        [NonSerialized] private int lastSurfaceHierarchyRejectCount;
        [NonSerialized] private bool lastSurfaceRaySaturated;
        [NonSerialized] private string lastSurfaceFirstHitSummary;

        public void ConfigurePlanet(Transform center) { planetCenter = center; }

        public bool TrySample(Transform player, Camera camera, IReadOnlyList<GameplayArea> areas,
            IReadOnlyList<WaveEnemyHandle> enemies, WaveSpawnFootprint footprint, out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = Quaternion.identity;
            if (player == null || planetCenter == null) return false;
            Vector3 center = planetCenter.position;
            float surfaceRadius = Vector3.Distance(player.position, center);
            Vector3 playerNormal = (player.position - center).normalized;
            if (playerNormal.sqrMagnitude < .001f) return false;
            BuildTangentBasis(playerNormal, out Vector3 tangent, out Vector3 bitangent);
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                float distance = UnityEngine.Random.Range(minimumDistance, maximumDistance);
                float angle = UnityEngine.Random.value * Mathf.PI * 2f;
                Vector3 direction = (playerNormal + (tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle)) * (distance / Mathf.Max(1f, surfaceRadius))).normalized;
                Vector3 rayStart = center + direction * (surfaceRadius + rayStartOffset);
                if (!TryFindPlanetSurface(rayStart, -direction, rayStartOffset * 2f + 100f, center,
                        surfaceRadius, 4f, out RaycastHit hit)) continue;
                Quaternion candidateRotation = RadialRotation(hit.normal, player.position - hit.point);
                Vector3 candidatePosition = RootPositionAboveSurface(hit.point, hit.normal, footprint);
                if (IsCameraVisible(hit.point, camera) || !IsClearOfAreas(hit.point, areas, footprint) ||
                    !IsSeparated(candidatePosition, hit.normal, footprint, enemies) ||
                    !IsFootprintClear(candidatePosition, candidateRotation, footprint, hit.collider)) continue;
                position = candidatePosition;
                rotation = candidateRotation;
                return true;
            }
            return false;
        }

        public bool TrySampleInside(GameplayArea area, IReadOnlyList<WaveEnemyHandle> enemies, WaveSpawnFootprint footprint,
            out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = Quaternion.identity;
            if (area == null || area.PlanetCenter == null || area.PerimeterPoles == null || area.PerimeterPoles.childCount < 3) return false;
            Vector3 center = area.PlanetCenter.position;
            Vector3 normal = AreaRadialDirection(area.PerimeterPoles, center);
            if (normal.sqrMagnitude < .001f) return false;
            float expectedRadius = AveragePoleRadius(area.PerimeterPoles, center);
            BuildTangentBasis(normal, out Vector3 tangent, out Vector3 bitangent);
            float radius = Mathf.Max(0f, InteriorRadius(area.PerimeterPoles, center + normal * expectedRadius, tangent, bitangent) - footprint.TangentRadius - SpawnGap);
            int noSurfaceCount = 0;
            int outsideAreaCount = 0;
            int separationCount = 0;
            int blockedFootprintCount = 0;
            int totalRawSurfaceHits = 0;
            int totalNormalRejects = 0;
            int totalRadiusRejects = 0;
            int totalHierarchyRejects = 0;
            int saturatedSurfaceRays = 0;
            string firstRawSurfaceHit = null;
            Collider firstBlockingCollider = null;
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                Vector2 disk = UnityEngine.Random.insideUnitCircle * radius;
                Vector3 direction = (normal + (tangent * disk.x + bitangent * disk.y) / Mathf.Max(1f, expectedRadius)).normalized;
                Vector3 rayStart = center + direction * (expectedRadius + rayStartOffset);
                if (!TryFindArenaSurface(rayStart, -direction, rayStartOffset * 2f + 100f, center,
                        area.PlanetCenter, out RaycastHit hit))
                {
                    noSurfaceCount++;
                    totalRawSurfaceHits += lastSurfaceRayHitCount;
                    totalNormalRejects += lastSurfaceNormalRejectCount;
                    totalRadiusRejects += lastSurfaceRadiusRejectCount;
                    totalHierarchyRejects += lastSurfaceHierarchyRejectCount;
                    if (lastSurfaceRaySaturated) saturatedSurfaceRays++;
                    if (firstRawSurfaceHit == null && !string.IsNullOrEmpty(lastSurfaceFirstHitSummary))
                        firstRawSurfaceHit = lastSurfaceFirstHitSummary;
                    continue;
                }
                Quaternion candidateRotation = RadialRotation(hit.normal, tangent);
                Vector3 candidatePosition = RootPositionAboveSurface(hit.point, hit.normal, footprint);
                if (!area.Contains(hit.point))
                {
                    outsideAreaCount++;
                    continue;
                }
                if (!IsSeparated(candidatePosition, hit.normal, footprint, enemies))
                {
                    separationCount++;
                    continue;
                }
                if (!TryGetFootprintClear(candidatePosition, candidateRotation, footprint, hit.collider,
                        out Collider blockingCollider))
                {
                    blockedFootprintCount++;
                    if (firstBlockingCollider == null) firstBlockingCollider = blockingCollider;
                    continue;
                }
                position = candidatePosition;
                rotation = candidateRotation;
                return true;
            }
            if (TrySurfaceSafeAreaFallback(area, enemies, footprint, out position, out rotation)) return true;

            if (Time.realtimeSinceStartup >= nextArenaFailureLogAt)
            {
                nextArenaFailureLogAt = Time.realtimeSinceStartup + 5f;
                string blocker = firstBlockingCollider != null
                    ? $"{firstBlockingCollider.name} ({firstBlockingCollider.GetType().Name})"
                    : "none observed";
                Debug.LogWarning(
                    $"Arena spawn sampler rejected all {attempts} candidates in '{area.name}': " +
                    $"surface={noSurfaceCount}, outside={outsideAreaCount}, separation={separationCount}, " +
                    $"footprint={blockedFootprintCount}, first blocker={blocker}, " +
                    $"raw hits={totalRawSurfaceHits}, normal rejects={totalNormalRejects}, " +
                    $"radius rejects={totalRadiusRejects}, hierarchy rejects={totalHierarchyRejects}, " +
                    $"saturated rays={saturatedSurfaceRays}, " +
                    $"first raw hit={firstRawSurfaceHit ?? "none"}, " +
                    $"pole radius={expectedRadius:F2}, sample radius={radius:F2}.",
                    area);
            }
            return false;
        }

        private bool TrySurfaceSafeAreaFallback(GameplayArea area, IReadOnlyList<WaveEnemyHandle> enemies, WaveSpawnFootprint footprint,
            out Vector3 position, out Quaternion rotation)
        {
            position = default;
            rotation = Quaternion.identity;
            Transform poles = area.PerimeterPoles;
            Vector3 center = area.PlanetCenter.position;
            Vector3 direction = AreaRadialDirection(poles, center);
            float expectedRadius = AveragePoleRadius(poles, center);
            if (direction.sqrMagnitude < .001f) return false;
            Vector3 rayStart = center + direction * (expectedRadius + rayStartOffset);
            if (!TryFindArenaSurface(rayStart, -direction, rayStartOffset * 2f + 100f, center,
                    area.PlanetCenter, out RaycastHit hit)) return false;
            BuildTangentBasis(hit.normal, out Vector3 tangent, out _);
            Quaternion candidateRotation = RadialRotation(hit.normal, tangent);
            Vector3 candidatePosition = RootPositionAboveSurface(hit.point, hit.normal, footprint);
            if (!area.Contains(hit.point) || !IsSeparated(candidatePosition, hit.normal, footprint, enemies) ||
                !IsFootprintClear(candidatePosition, candidateRotation, footprint, hit.collider)) return false;
            position = candidatePosition;
            rotation = candidateRotation;
            return true;
        }

        private bool TryFindPlanetSurface(Vector3 origin, Vector3 direction, float distance,
            Vector3 center, float expectedRadius, float radiusTolerance, out RaycastHit surfaceHit)
        {
            if (surfaceHitBuffer == null) surfaceHitBuffer = new RaycastHit[SurfaceHitBufferSize];
            int hitCount = Physics.RaycastNonAlloc(
                origin, direction, surfaceHitBuffer, distance, surfaceMask, QueryTriggerInteraction.Ignore);
            lastSurfaceRayHitCount = hitCount;
            lastSurfaceNormalRejectCount = 0;
            lastSurfaceRadiusRejectCount = 0;
            lastSurfaceHierarchyRejectCount = 0;
            lastSurfaceRaySaturated = hitCount >= surfaceHitBuffer.Length;
            lastSurfaceFirstHitSummary = null;
            if (hitCount > 0)
            {
                RaycastHit firstHit = surfaceHitBuffer[0];
                Vector3 firstRadial = firstHit.point - center;
                float firstDot = firstRadial.sqrMagnitude > .001f
                    ? Vector3.Dot(firstHit.normal.normalized, firstRadial.normalized)
                    : 0f;
                lastSurfaceFirstHitSummary =
                    $"{(firstHit.collider != null ? firstHit.collider.name : "null")} " +
                    $"r={firstRadial.magnitude:F2} dot={firstDot:F2}";
            }
            // Saturation means the selected subset is not trustworthy; let the caller retry a
            // different radial candidate instead of risking a prop hit or an inside-shell point.
            if (hitCount >= surfaceHitBuffer.Length)
            {
                surfaceHit = default;
                return false;
            }
            float bestRadiusError = float.PositiveInfinity;
            surfaceHit = default;
            bool found = false;
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = surfaceHitBuffer[index];
                Vector3 radial = hit.point - center;
                if (radial.sqrMagnitude < .001f || Vector3.Dot(hit.normal.normalized, radial.normalized) < .55f)
                {
                    lastSurfaceNormalRejectCount++;
                    continue;
                }
                float radiusError = Mathf.Abs(radial.magnitude - expectedRadius);
                if (radiusError > radiusTolerance)
                {
                    lastSurfaceRadiusRejectCount++;
                    continue;
                }
                if (radiusError >= bestRadiusError) continue;
                bestRadiusError = radiusError;
                surfaceHit = hit;
                found = true;
            }
            return found;
        }

        private bool TryFindArenaSurface(Vector3 origin, Vector3 direction, float distance,
            Vector3 center, Transform surfaceRoot, out RaycastHit surfaceHit)
        {
            if (surfaceHitBuffer == null) surfaceHitBuffer = new RaycastHit[SurfaceHitBufferSize];
            int hitCount = Physics.RaycastNonAlloc(
                origin, direction, surfaceHitBuffer, distance, surfaceMask, QueryTriggerInteraction.Ignore);
            lastSurfaceRayHitCount = hitCount;
            lastSurfaceNormalRejectCount = 0;
            lastSurfaceRadiusRejectCount = 0;
            lastSurfaceHierarchyRejectCount = 0;
            lastSurfaceRaySaturated = hitCount >= surfaceHitBuffer.Length;
            lastSurfaceFirstHitSummary = null;
            if (hitCount > 0)
            {
                RaycastHit firstHit = surfaceHitBuffer[0];
                Vector3 firstRadial = firstHit.point - center;
                float firstDot = firstRadial.sqrMagnitude > .001f
                    ? Vector3.Dot(firstHit.normal.normalized, firstRadial.normalized)
                    : 0f;
                lastSurfaceFirstHitSummary =
                    $"{(firstHit.collider != null ? firstHit.collider.name : "null")} " +
                    $"r={firstRadial.magnitude:F2} dot={firstDot:F2}";
            }
            if (hitCount >= surfaceHitBuffer.Length)
            {
                surfaceHit = default;
                return false;
            }

            // Perimeter poles define the arena boundary, not its floor height. Arena 1's poles
            // sit roughly forty world units above its crater floor, so comparing ground hits to
            // their average radius rejects every valid spawn. The configured planet hierarchy is
            // a stronger surface identity: it accepts the actual ground while excluding rocks,
            // arena walls, ships, and other props hit by the same inward ray.
            float outermostRadius = float.NegativeInfinity;
            surfaceHit = default;
            bool found = false;
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = surfaceHitBuffer[index];
                if (hit.collider == null || surfaceRoot == null ||
                    (hit.collider.transform != surfaceRoot && !hit.collider.transform.IsChildOf(surfaceRoot)))
                {
                    lastSurfaceHierarchyRejectCount++;
                    continue;
                }

                Vector3 radial = hit.point - center;
                if (radial.sqrMagnitude < .001f || Vector3.Dot(hit.normal.normalized, radial.normalized) < .55f)
                {
                    lastSurfaceNormalRejectCount++;
                    continue;
                }

                float radius = radial.magnitude;
                if (radius <= outermostRadius) continue;
                outermostRadius = radius;
                surfaceHit = hit;
                found = true;
            }
            return found;
        }

        private bool IsClearOfAreas(Vector3 point, IReadOnlyList<GameplayArea> areas, WaveSpawnFootprint footprint)
        {
            if (areas == null) return true;
            float clearance = areaClearance + footprint.TangentRadius;
            for (int index = 0; index < areas.Count; index++)
            {
                GameplayArea area = areas[index];
                if (area == null) continue;
                if (area.Contains(point) || DistanceToPerimeter(point, area) < clearance) return false;
            }
            return true;
        }

        private bool IsSeparated(Vector3 point, Vector3 normal, WaveSpawnFootprint footprint, IReadOnlyList<WaveEnemyHandle> enemies)
        {
            if (enemies == null) return true;
            for (int index = 0; index < enemies.Count; index++)
            {
                WaveEnemyHandle enemy = enemies[index];
                if (enemy == null) continue;
                WaveSpawnFootprint other = enemy.SpawnFootprint;
                Vector3 offset = Vector3.ProjectOnPlane(point - enemy.transform.position, normal);
                if (offset.magnitude < footprint.TangentRadius + other.TangentRadius + SpawnGap) return false;
            }
            return true;
        }

        private bool IsFootprintClear(Vector3 rootPosition, Quaternion rootRotation, WaveSpawnFootprint footprint, Collider groundCollider)
        {
            return TryGetFootprintClear(rootPosition, rootRotation, footprint, groundCollider, out _);
        }

        private bool TryGetFootprintClear(Vector3 rootPosition, Quaternion rootRotation, WaveSpawnFootprint footprint,
            Collider groundCollider, out Collider blockingCollider)
        {
            blockingCollider = null;
            if (overlapBuffer == null) overlapBuffer = new Collider[OverlapBufferSize];
            // The footprint is already a conservative root-local box containing all physical
            // colliders, including inactive boss stages. Using that oriented box avoids the
            // enormous empty spherical clearance generated by tall/offset boss hierarchies.
            Vector3 halfExtents = footprint.LocalExtents + Vector3.one * SpawnGap;
            int count = Physics.OverlapBoxNonAlloc(footprint.WorldCenter(rootPosition, rootRotation), halfExtents,
                overlapBuffer, rootRotation, ~0, QueryTriggerInteraction.Ignore);
            if (count >= overlapBuffer.Length) return false;
            for (int index = 0; index < count; index++)
            {
                Collider collider = overlapBuffer[index];
                if (collider != null && collider != groundCollider)
                {
                    blockingCollider = collider;
                    return false;
                }
            }
            return true;
        }

        private static float DistanceToPerimeter(Vector3 point, GameplayArea area)
        {
            Transform poles = area.PerimeterPoles;
            if (poles == null || poles.childCount < 2) return float.MaxValue;
            float closest = float.MaxValue;
            for (int index = 0; index < poles.childCount; index++)
            {
                Vector3 a = poles.GetChild(index).position;
                Vector3 b = poles.GetChild((index + 1) % poles.childCount).position;
                Vector3 segment = b - a;
                float t = segment.sqrMagnitude < .0001f ? 0f : Mathf.Clamp01(Vector3.Dot(point - a, segment) / segment.sqrMagnitude);
                closest = Mathf.Min(closest, Vector3.Distance(point, a + segment * t));
            }
            return closest;
        }

        private static bool IsCameraVisible(Vector3 point, Camera camera)
        {
            if (camera == null) return false;
            Vector3 viewport = camera.WorldToViewportPoint(point);
            return viewport.z > 0f && viewport.x >= 0f && viewport.x <= 1f && viewport.y >= 0f && viewport.y <= 1f;
        }

        /// <summary>
        /// Finds a grounded pickup site along a supplied globe direction. Unlike enemy sampling,
        /// this deliberately permits a caller-selected arena but still rejects every other
        /// protected area, physical obstacles, and already-reserved pickup sites.
        /// </summary>
        public bool TrySamplePickup(
            Transform surfaceRoot,
            IReadOnlyList<GameplayArea> protectedAreas,
            GameplayArea allowedArea,
            IReadOnlyList<Vector3> reservedPositions,
            Vector3 radialDirection,
            float expectedRadius,
            float clearanceRadius,
            out Vector3 position,
            out Quaternion rotation)
        {
            position = default;
            rotation = Quaternion.identity;
            if (planetCenter == null || surfaceRoot == null || radialDirection.sqrMagnitude < .001f) return false;

            Vector3 center = planetCenter.position;
            radialDirection.Normalize();
            Vector3 rayStart = center + radialDirection * (Mathf.Max(1f, expectedRadius) + rayStartOffset);
            if (!TryFindArenaSurface(rayStart, -radialDirection, rayStartOffset * 2f + 100f, center,
                    surfaceRoot, out RaycastHit hit)) return false;

            if (!IsClearOfPickupAreas(hit.point, protectedAreas, allowedArea, clearanceRadius) ||
                !IsSeparatedFromPickups(hit.point, hit.normal, reservedPositions, clearanceRadius) ||
                !IsPickupSiteClear(hit.point, hit.normal, clearanceRadius, hit.collider)) return false;

            position = hit.point;
            rotation = RadialRotation(hit.normal, radialDirection);
            return true;
        }

        /// <summary>Grounds a pickup at the radial center of an arena without randomizing it around the ring.</summary>
        public bool TrySamplePickupAtAreaCenter(
            GameplayArea area,
            Transform surfaceRoot,
            IReadOnlyList<GameplayArea> protectedAreas,
            IReadOnlyList<Vector3> reservedPositions,
            float clearanceRadius,
            out Vector3 position,
            out Quaternion rotation)
        {
            position = default;
            rotation = Quaternion.identity;
            if (area == null || area.PlanetCenter == null || area.PerimeterPoles == null) return false;
            Vector3 center = area.PlanetCenter.position;
            Vector3 direction = AreaRadialDirection(area.PerimeterPoles, center);
            if (direction.sqrMagnitude < .001f) return false;
            if (!TrySamplePickup(surfaceRoot, protectedAreas, area, reservedPositions, direction,
                    AveragePoleRadius(area.PerimeterPoles, center), clearanceRadius, out position, out rotation))
                return false;
            if (area.Contains(position)) return true;
            position = default;
            rotation = Quaternion.identity;
            return false;
        }

        private bool IsClearOfPickupAreas(Vector3 point, IReadOnlyList<GameplayArea> areas,
            GameplayArea allowedArea, float clearance)
        {
            if (areas == null) return true;
            for (int index = 0; index < areas.Count; index++)
            {
                GameplayArea area = areas[index];
                if (area == null || area == allowedArea) continue;
                if (area.Contains(point) || DistanceToPerimeter(point, area) < areaClearance + clearance) return false;
            }
            return true;
        }

        private static bool IsSeparatedFromPickups(Vector3 point, Vector3 normal,
            IReadOnlyList<Vector3> reservedPositions, float clearance)
        {
            if (reservedPositions == null) return true;
            for (int index = 0; index < reservedPositions.Count; index++)
            {
                Vector3 offset = Vector3.ProjectOnPlane(point - reservedPositions[index], normal);
                if (offset.magnitude < clearance * 2f) return false;
            }
            return true;
        }

        private bool IsPickupSiteClear(Vector3 surfacePoint, Vector3 surfaceNormal, float clearance,
            Collider groundCollider)
        {
            if (overlapBuffer == null) overlapBuffer = new Collider[OverlapBufferSize];
            Vector3 probeCenter = surfacePoint + surfaceNormal.normalized * clearance;
            int count = Physics.OverlapSphereNonAlloc(probeCenter, clearance, overlapBuffer, ~0,
                QueryTriggerInteraction.Ignore);
            if (count >= overlapBuffer.Length) return false;
            for (int index = 0; index < count; index++)
            {
                Collider collider = overlapBuffer[index];
                if (collider != null && collider != groundCollider) return false;
            }
            return true;
        }

        private static Quaternion RadialRotation(Vector3 normal, Vector3 forwardHint)
        {
            Vector3 forward = Vector3.ProjectOnPlane(forwardHint, normal);
            if (forward.sqrMagnitude < .0001f) BuildTangentBasis(normal, out forward, out _);
            return Quaternion.LookRotation(forward.normalized, normal);
        }

        private static Vector3 RootPositionAboveSurface(Vector3 surfacePosition, Vector3 surfaceNormal,
            WaveSpawnFootprint footprint)
        {
            // The sampled point is where the terrain is safe to stand, whereas some enemy roots
            // sit below their physical controller/collider. Lifting by that authored inward
            // extent keeps the clearance query from rejecting the planet as an obstacle and
            // prevents the runtime controller from beginning inside the surface.
            return surfacePosition + surfaceNormal.normalized * (footprint.InwardExtent + SpawnGap);
        }

        private static void BuildTangentBasis(Vector3 normal, out Vector3 tangent, out Vector3 bitangent)
        {
            tangent = Vector3.Cross(Mathf.Abs(normal.y) > .9f ? Vector3.forward : Vector3.up, normal).normalized;
            bitangent = Vector3.Cross(normal, tangent).normalized;
        }

        private static Vector3 AreaRadialDirection(Transform poles, Vector3 center)
        {
            if (poles == null || poles.childCount == 0) return Vector3.zero;
            Vector3 sum = Vector3.zero;
            for (int index = 0; index < poles.childCount; index++)
            {
                Vector3 radial = poles.GetChild(index).position - center;
                if (radial.sqrMagnitude > .0001f) sum += radial.normalized;
            }
            return sum.sqrMagnitude > .0001f ? sum.normalized : Vector3.zero;
        }

        private static float AveragePoleRadius(Transform poles, Vector3 center)
        {
            float sum = 0f;
            for (int index = 0; index < poles.childCount; index++) sum += Vector3.Distance(poles.GetChild(index).position, center);
            return sum / poles.childCount;
        }

        private static float InteriorRadius(Transform poles, Vector3 center, Vector3 tangent, Vector3 bitangent)
        {
            float radius = 0f;
            for (int index = 0; index < poles.childCount; index++)
            {
                Vector3 offset = poles.GetChild(index).position - center;
                radius = Mathf.Max(radius, new Vector2(Vector3.Dot(offset, tangent), Vector3.Dot(offset, bitangent)).magnitude);
            }
            return Mathf.Max(1f, radius * .7f);
        }
    }

    /// <summary>
    /// Owns only wave-created pickups. Regular supplies are distributed across the globe once
    /// per regular wave; an arena Thunder pickup is a separate, fight-scoped allocation.
    /// </summary>
    [Serializable]
    public sealed class WavePickupSpawner
    {
        private const int PlacementAttemptsPerPickup = 48;
        private const float PickupClearanceRadius = .9f;
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private readonly List<Vector3> _reservedPositions = new List<Vector3>();

        public int ActiveCount
        {
            get
            {
                PruneDestroyed();
                return _spawned.Count;
            }
        }

        public void SpawnRegularPickups(
            WaveSurfaceSpawnSampler sampler,
            Transform planetSurface,
            Transform player,
            IReadOnlyList<GameplayArea> protectedAreas,
            GameObject healthPrefab,
            bool spawnHealth,
            GameObject ammoPrefab,
            bool spawnAmmo,
            Transform parent)
        {
            if (sampler == null || planetSurface == null || player == null) return;
            float expectedRadius = Vector3.Distance(player.position, planetSurface.position);
            if (spawnHealth) SpawnSet(sampler, planetSurface, protectedAreas, healthPrefab,
                WaveRules.MedKitPickupsPerRegularWave, expectedRadius, parent, .19f);
            if (spawnAmmo) SpawnSet(sampler, planetSurface, protectedAreas, ammoPrefab,
                WaveRules.AmmoKitPickupsPerRegularWave, expectedRadius, parent, .71f);
        }

        public void SpawnArenaUltimate(
            WaveSurfaceSpawnSampler sampler,
            Transform planetSurface,
            IReadOnlyList<GameplayArea> protectedAreas,
            GameplayArea targetArena,
            GameObject thunderPrefab,
            Transform parent)
        {
            if (sampler == null || planetSurface == null || targetArena == null || thunderPrefab == null) return;
            PruneDestroyed();
            if (!sampler.TrySamplePickupAtAreaCenter(targetArena, planetSurface, protectedAreas,
                    _reservedPositions, PickupClearanceRadius, out Vector3 position, out Quaternion rotation))
            {
                Debug.LogWarning($"Could not find a safe center pickup site in '{targetArena.name}'.", targetArena);
                return;
            }
            Spawn(thunderPrefab, position, rotation, parent);
        }

        public void Cleanup()
        {
            for (int index = 0; index < _spawned.Count; index++)
            {
                GameObject pickup = _spawned[index];
                if (pickup != null) UnityEngine.Object.Destroy(pickup);
            }
            _spawned.Clear();
            _reservedPositions.Clear();
        }

        /// <summary>Fibonacci sphere positions keep a finite wave budget evenly globe-wide.</summary>
        public static Vector3 EvenlyDistributedDirection(int index, int count, float phase)
        {
            count = Mathf.Max(1, count);
            float fraction = (Mathf.Clamp(index, 0, count - 1) + .5f) / count;
            float y = 1f - 2f * fraction;
            float radial = Mathf.Sqrt(Mathf.Max(0f, 1f - y * y));
            float angle = index * 2.39996323f + phase;
            return new Vector3(Mathf.Cos(angle) * radial, y, Mathf.Sin(angle) * radial);
        }

        private void SpawnSet(WaveSurfaceSpawnSampler sampler, Transform planetSurface,
            IReadOnlyList<GameplayArea> protectedAreas, GameObject prefab, int count, float expectedRadius,
            Transform parent, float phaseOffset)
        {
            if (prefab == null) return;
            PruneDestroyed();
            float phase = phaseOffset * Mathf.PI * 2f + UnityEngine.Random.value * .16f;
            int placed = 0;
            for (int index = 0; index < count; index++)
            {
                Vector3 baseDirection = EvenlyDistributedDirection(index, count, phase);
                bool found = false;
                for (int attempt = 0; attempt < PlacementAttemptsPerPickup; attempt++)
                {
                    Vector3 direction = JitterDirection(baseDirection, index, attempt);
                    if (!sampler.TrySamplePickup(planetSurface, protectedAreas, null, _reservedPositions,
                            direction, expectedRadius, PickupClearanceRadius, out Vector3 position,
                            out Quaternion rotation)) continue;
                    Spawn(prefab, position, rotation, parent);
                    placed++;
                    found = true;
                    break;
                }
                if (!found)
                    Debug.LogWarning($"Wave pickup placement skipped slot {index + 1}/{count}; no safe terrain site was found.");
            }
            if (placed != count)
                Debug.LogWarning($"Wave pickup allocation produced {placed}/{count} '{prefab.name}' pickups.");
        }

        private void Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent)
        {
            GameObject pickup = UnityEngine.Object.Instantiate(prefab, position, rotation, parent);
            _spawned.Add(pickup);
            _reservedPositions.Add(position);
        }

        private static Vector3 JitterDirection(Vector3 baseDirection, int index, int attempt)
        {
            // The first attempt is a small per-wave offset. Retries expand outward only when
            // terrain or a protected zone rejects that ideal evenly-spaced site.
            Vector3 reference = Mathf.Abs(baseDirection.y) > .9f ? Vector3.forward : Vector3.up;
            Vector3 tangent = Vector3.Cross(reference, baseDirection).normalized;
            Vector3 bitangent = Vector3.Cross(baseDirection, tangent).normalized;
            float hash = Mathf.Repeat(Mathf.Sin((index + 1) * 12.9898f + (attempt + 1) * 78.233f) * 43758.5453f, 1f);
            float angle = hash * Mathf.PI * 2f;
            float magnitude = attempt == 0 ? .045f : Mathf.Min(.28f, .045f + attempt * .008f);
            return (baseDirection + (tangent * Mathf.Cos(angle) + bitangent * Mathf.Sin(angle)) * magnitude).normalized;
        }

        private void PruneDestroyed()
        {
            for (int index = _spawned.Count - 1; index >= 0; index--)
            {
                if (_spawned[index] != null) continue;
                _spawned.RemoveAt(index);
                _reservedPositions.RemoveAt(index);
            }
        }
    }
}
