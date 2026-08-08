using UnityEngine;

namespace Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
    public sealed class RadialCapsuleMotor : MonoBehaviour
    {
        [Header("Collision")]
        [SerializeField] private LayerMask collisionMask = ~0;
        [SerializeField, Min(0f)] private float contactOffset = 0.02f;
        [SerializeField, Range(1, 8)] private int slideIterations = 4;
        [SerializeField, Range(1, 8)] private int depenetrationIterations = 3;
        [SerializeField, Range(0f, 1f)] private float minimumBottomDot = 0.25f;
        [SerializeField, Min(0f)] private float stepHeight = 0.3f;
        [SerializeField, Range(0f, 89f)] private float maxStepGroundAngle = 45f;

        private const float DistanceEpsilon = 0.0001f;
        private const int HitBufferSize = 16;
        private const int OverlapBufferSize = 16;

        private readonly RaycastHit[] _hitBuffer = new RaycastHit[HitBufferSize];
        private readonly Collider[] _overlapBuffer = new Collider[OverlapBufferSize];
        private Rigidbody _body;
        private CapsuleCollider _capsule;

        public bool HadBottomContact { get; private set; }
        public bool HadSideContact { get; private set; }
        public bool HadTopContact { get; private set; }

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _capsule = GetComponent<CapsuleCollider>();
            ConfigurePhysicsBody();
        }

        public void Move(
            Vector3 displacement,
            Quaternion targetRotation,
            Vector3 localUp,
            bool allowStep)
        {
            HadBottomContact = false;
            HadSideContact = false;
            HadTopContact = false;

            Vector3 position = _body.position;
            Quaternion rotation = targetRotation.normalized;
            position = ResolvePenetration(position, rotation);

            Vector3 remaining = displacement;
            int iterations = Mathf.Clamp(slideIterations, 1, 8);
            for (int iteration = 0; iteration < iterations; iteration++)
            {
                float distance = remaining.magnitude;
                if (distance <= DistanceEpsilon)
                {
                    break;
                }

                Vector3 direction = remaining / distance;
                GetWorldCapsule(position, rotation, out Vector3 point1, out Vector3 point2,
                    out float radius);
                if (!TryGetNearestCastHit(
                        point1,
                        point2,
                        radius,
                        direction,
                        distance + Mathf.Max(0f, contactOffset),
                        out RaycastHit hit))
                {
                    position += remaining;
                    remaining = Vector3.zero;
                    break;
                }

                float travel = Mathf.Max(0f, hit.distance - Mathf.Max(0f, contactOffset));
                position += direction * Mathf.Min(travel, distance);
                ClassifyContact(hit.normal, localUp);

                float consumed = Mathf.Min(travel, distance);
                Vector3 unconsumed = remaining - direction * consumed;
                if (allowStep && IsSideContact(hit.normal, localUp) &&
                    TryStep(position, rotation, unconsumed, localUp, out Vector3 steppedPosition,
                        out Vector3 steppedRemaining))
                {
                    position = steppedPosition;
                    remaining = steppedRemaining;
                    HadBottomContact = true;
                    continue;
                }

                remaining = Vector3.ProjectOnPlane(unconsumed, hit.normal);
            }

            position = ResolvePenetration(position, rotation);
            _body.MoveRotation(rotation);
            _body.MovePosition(position);
        }

        public Vector3 GetBottomPoint(Vector3 position, Quaternion rotation)
        {
            GetWorldCapsule(position, rotation, out Vector3 point1, out Vector3 point2,
                out float radius);
            Vector3 up = rotation * Vector3.up;
            return Vector3.Dot(point1, up) <= Vector3.Dot(point2, up)
                ? point1 - up * radius
                : point2 - up * radius;
        }

        private void ConfigurePhysicsBody()
        {
            _capsule.direction = 1;
            _capsule.isTrigger = false;
            _body.useGravity = false;
            _body.isKinematic = true;
            _body.interpolation = RigidbodyInterpolation.Interpolate;
            _body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        private bool TryGetNearestCastHit(
            Vector3 point1,
            Vector3 point2,
            float radius,
            Vector3 direction,
            float distance,
            out RaycastHit nearestHit)
        {
            int hitCount = Physics.CapsuleCastNonAlloc(
                point1,
                point2,
                radius,
                direction,
                _hitBuffer,
                distance,
                collisionMask,
                QueryTriggerInteraction.Ignore);

            bool found = false;
            float nearestDistance = float.PositiveInfinity;
            nearestHit = default;
            for (int index = 0; index < hitCount; index++)
            {
                RaycastHit hit = _hitBuffer[index];
                if (IsOwnCollider(hit.collider) || hit.distance >= nearestDistance)
                {
                    continue;
                }

                found = true;
                nearestDistance = hit.distance;
                nearestHit = hit;
            }

            return found;
        }

        private Vector3 ResolvePenetration(Vector3 position, Quaternion rotation)
        {
            int iterations = Mathf.Clamp(depenetrationIterations, 1, 8);
            for (int iteration = 0; iteration < iterations; iteration++)
            {
                GetWorldCapsule(position, rotation, out Vector3 point1, out Vector3 point2,
                    out float radius);
                int overlapCount = Physics.OverlapCapsuleNonAlloc(
                    point1,
                    point2,
                    radius,
                    _overlapBuffer,
                    collisionMask,
                    QueryTriggerInteraction.Ignore);

                bool moved = false;
                for (int index = 0; index < overlapCount; index++)
                {
                    Collider other = _overlapBuffer[index];
                    if (IsOwnCollider(other) ||
                        !Physics.ComputePenetration(
                            _capsule,
                            position,
                            rotation,
                            other,
                            other.transform.position,
                            other.transform.rotation,
                            out Vector3 direction,
                            out float distance) ||
                        distance <= DistanceEpsilon)
                    {
                        continue;
                    }

                    position += direction * (distance + Mathf.Max(0f, contactOffset));
                    moved = true;
                }

                if (!moved)
                {
                    break;
                }
            }

            return position;
        }

        private bool TryStep(
            Vector3 position,
            Quaternion rotation,
            Vector3 unconsumed,
            Vector3 localUp,
            out Vector3 steppedPosition,
            out Vector3 steppedRemaining)
        {
            steppedPosition = position;
            steppedRemaining = unconsumed;

            float height = Mathf.Max(0f, stepHeight);
            Vector3 horizontal = Vector3.ProjectOnPlane(unconsumed, localUp);
            float horizontalDistance = horizontal.magnitude;
            if (height <= DistanceEpsilon || horizontalDistance <= DistanceEpsilon)
            {
                return false;
            }

            float offset = Mathf.Max(0f, contactOffset);
            GetWorldCapsule(position, rotation, out Vector3 point1, out Vector3 point2,
                out float radius);
            if (TryGetNearestCastHit(
                    point1,
                    point2,
                    radius,
                    localUp,
                    height + offset,
                    out RaycastHit ceilingHit) &&
                ceilingHit.distance < height + offset)
            {
                return false;
            }

            Vector3 raisedPosition = position + localUp * height;
            Vector3 horizontalDirection = horizontal / horizontalDistance;
            GetWorldCapsule(raisedPosition, rotation, out point1, out point2, out radius);
            if (TryGetNearestCastHit(
                    point1,
                    point2,
                    radius,
                    horizontalDirection,
                    horizontalDistance + offset,
                    out RaycastHit forwardHit) &&
                forwardHit.distance < horizontalDistance + offset)
            {
                return false;
            }

            Vector3 advancedPosition = raisedPosition + horizontal;
            GetWorldCapsule(advancedPosition, rotation, out point1, out point2, out radius);
            float downDistance = height + offset * 2f;
            if (!TryGetNearestCastHit(
                    point1,
                    point2,
                    radius,
                    -localUp,
                    downDistance,
                    out RaycastHit groundHit) ||
                Vector3.Dot(groundHit.normal.normalized, localUp.normalized) <
                Mathf.Cos(Mathf.Clamp(maxStepGroundAngle, 0f, 89f) * Mathf.Deg2Rad))
            {
                return false;
            }

            float downTravel = Mathf.Max(0f, groundHit.distance - offset);
            steppedPosition = advancedPosition - localUp * downTravel;
            steppedRemaining = Vector3.Project(unconsumed, localUp);
            return true;
        }

        private void GetWorldCapsule(
            Vector3 position,
            Quaternion rotation,
            out Vector3 point1,
            out Vector3 point2,
            out float radius)
        {
            Vector3 scale = transform.lossyScale;
            float height = Mathf.Max(
                _capsule.height * Mathf.Abs(scale.y),
                GetWorldRadius(scale) * 2f);
            radius = GetWorldRadius(scale);
            float segmentHalf = Mathf.Max(0f, height * 0.5f - radius);
            Vector3 center = position + rotation * Vector3.Scale(_capsule.center, scale);
            Vector3 up = rotation * Vector3.up;
            point1 = center + up * segmentHalf;
            point2 = center - up * segmentHalf;
        }

        private float GetWorldRadius(Vector3 scale)
        {
            float lateralScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z));
            return Mathf.Max(0.01f, _capsule.radius * lateralScale);
        }

        private void ClassifyContact(Vector3 normal, Vector3 localUp)
        {
            float upDot = Vector3.Dot(normal.normalized, localUp.normalized);
            float threshold = Mathf.Clamp01(minimumBottomDot);
            if (upDot >= threshold)
            {
                HadBottomContact = true;
            }
            else if (upDot <= -threshold)
            {
                HadTopContact = true;
            }
            else
            {
                HadSideContact = true;
            }
        }

        private bool IsSideContact(Vector3 normal, Vector3 localUp)
        {
            return Mathf.Abs(Vector3.Dot(normal.normalized, localUp.normalized)) <
                   Mathf.Clamp01(minimumBottomDot);
        }

        private bool IsOwnCollider(Collider candidate)
        {
            return candidate == null ||
                   candidate == _capsule ||
                   candidate.transform == transform ||
                   candidate.transform.IsChildOf(transform);
        }
    }
}
