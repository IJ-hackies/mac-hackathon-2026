using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Player.Tests
{
    public sealed class RadialCapsuleMotorStepTests
    {
        private const BindingFlags InstanceNonPublic =
            BindingFlags.Instance | BindingFlags.NonPublic;

        private GameObject _motorObject;
        private GameObject _seamObject;
        private Mesh _seamMesh;
        private Component _motor;
        private MethodInfo _classifyContact;
        private MethodInfo _stepObstaclePredicate;

        [SetUp]
        public void SetUp()
        {
            Type motorType = Type.GetType("Player.RadialCapsuleMotor, Assembly-CSharp");
            Assert.That(motorType, Is.Not.Null, "The radial capsule motor must remain available.");

            _motorObject = new GameObject("RadialCapsuleMotorStepTests");
            _motorObject.AddComponent<Rigidbody>();
            CapsuleCollider capsule = _motorObject.AddComponent<CapsuleCollider>();
            capsule.height = 2.55f;
            capsule.radius = 0.55f;
            capsule.center = new Vector3(0f, 1.275f, 0f);
            _motor = _motorObject.AddComponent(motorType);

            _classifyContact = motorType.GetMethod("ClassifyContact", InstanceNonPublic);
            Assert.That(_classifyContact, Is.Not.Null);

            _stepObstaclePredicate = motorType
                .GetMethods(InstanceNonPublic)
                .SingleOrDefault(method =>
                {
                    ParameterInfo[] parameters = method.GetParameters();
                    return method.ReturnType == typeof(bool) &&
                           method.Name.IndexOf("Step", StringComparison.OrdinalIgnoreCase) >= 0 &&
                           parameters.Length == 2 &&
                           parameters.All(parameter => parameter.ParameterType == typeof(Vector3));
                });
            Assert.That(
                _stepObstaclePredicate,
                Is.Not.Null,
                "The motor needs a dedicated two-vector step-obstacle predicate.");
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_motorObject);
            UnityEngine.Object.DestroyImmediate(_seamObject);
            UnityEngine.Object.DestroyImmediate(_seamMesh);
        }

        [Test]
        public void SteepUpwardStairLip_RemainsGroundContactButCanTriggerStepSequence()
        {
            // A 60-degree bevel is too steep to stand on (45-degree maximum step ground),
            // but mesh seams often expose exactly this kind of upward-facing normal.
            Vector3 steepUpwardLip = new Vector3(-Mathf.Sqrt(0.75f), 0.5f, 0f);

            _classifyContact.Invoke(_motor, new object[] { steepUpwardLip, Vector3.up });

            Assert.That(GetContactState("HadBottomContact"), Is.True);
            Assert.That(GetContactState("HadSideContact"), Is.False);
            Assert.That(IsStepObstacle(steepUpwardLip), Is.True);
        }

        [TestCase(0f, 1f, 0f)]
        [TestCase(0f, -1f, 0f)]
        [TestCase(0.6f, 0.8f, 0f)]
        public void WalkableGroundAndCeilings_CannotTriggerStepSequence(float x, float y, float z)
        {
            Assert.That(IsStepObstacle(new Vector3(x, y, z)), Is.False);
        }

        [Test]
        public void Move_WithSteepBeveledLipAndStepEnabled_ClimbsOntoWalkableFlatWithoutJump()
        {
            const float stepHeight = 0.25f;
            float bevelRun = stepHeight / Mathf.Sqrt(3f);
            CreateBeveledSeam(stepHeight, bevelRun);
            Physics.SyncTransforms();

            Rigidbody body = _motorObject.GetComponent<Rigidbody>();
            body.position = new Vector3(-1f, 0f, 0f);
            Physics.SyncTransforms();

            MethodInfo move = _motor.GetType().GetMethod("Move");
            Assert.That(move, Is.Not.Null);

            // No upward movement is supplied. The motor must use its existing step path to
            // clear the steep bevel and settle on the adjacent walkable flat surface.
            move.Invoke(_motor, new object[]
            {
                new Vector3(2f, 0f, 0f),
                Quaternion.identity,
                Vector3.up,
                true
            });
            Physics.SyncTransforms();

            Vector3 bottom = (Vector3)_motor.GetType()
                .GetMethod("GetBottomPoint")
                .Invoke(_motor, new object[] { body.position, Quaternion.identity });

            Assert.That(body.position.x, Is.GreaterThan(bevelRun));
            Assert.That(bottom.y, Is.InRange(stepHeight, stepHeight + 0.03f));
        }

        private bool IsStepObstacle(Vector3 normal)
        {
            return (bool)_stepObstaclePredicate.Invoke(_motor, new object[] { normal, Vector3.up });
        }

        private bool GetContactState(string propertyName)
        {
            PropertyInfo property = _motor.GetType().GetProperty(propertyName);
            Assert.That(property, Is.Not.Null, propertyName);
            return (bool)property.GetValue(_motor);
        }

        private void CreateBeveledSeam(float stepHeight, float bevelRun)
        {
            _seamObject = new GameObject("Steep Beveled Step Seam");
            _seamMesh = new Mesh
            {
                name = "Steep Beveled Step Seam Mesh",
                vertices = new[]
                {
                    new Vector3(0f, 0f, -1f),
                    new Vector3(0f, 0f, 1f),
                    new Vector3(bevelRun, stepHeight, -1f),
                    new Vector3(bevelRun, stepHeight, 1f),
                    new Vector3(3f, stepHeight, -1f),
                    new Vector3(3f, stepHeight, 1f)
                },
                triangles = new[]
                {
                    0, 1, 2,
                    2, 1, 3,
                    2, 3, 4,
                    4, 3, 5
                }
            };
            _seamMesh.RecalculateNormals();
            _seamObject.AddComponent<MeshCollider>().sharedMesh = _seamMesh;
        }
    }
}
