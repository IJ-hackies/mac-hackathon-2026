using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Gameplay.Waves.Tests
{
    /// <summary>Pure-regression coverage for the shared, spherical enemy navigation helpers.</summary>
    public sealed class EnemyNavigationTests
    {
        private static readonly Type EnemyBaseType = Type.GetType("Enemies.EnemyBase, Assembly-CSharp");

        [Test]
        public void TangentRandomBasisNeverLeaksIntoTheRadialAxis()
        {
            Vector3 radialUp = new Vector3(.43f, .78f, -.45f).normalized;
            Vector3 direction = InvokeVector("BuildTangentDirection", radialUp, new Vector2(.6f, -.8f));

            Assert.That(direction.magnitude, Is.EqualTo(1f).Within(.0001f));
            Assert.That(Mathf.Abs(Vector3.Dot(direction, radialUp)), Is.LessThan(.0001f));
        }

        [Test]
        public void DetoursStayTangentAndUseOppositeSides()
        {
            Vector3 up = Vector3.up;
            Vector3 left = InvokeVector("BuildDetourDirection", Vector3.forward, up, 1);
            Vector3 right = InvokeVector("BuildDetourDirection", Vector3.forward, up, -1);

            Assert.That(Mathf.Abs(Vector3.Dot(left, up)), Is.LessThan(.0001f));
            Assert.That(Mathf.Abs(Vector3.Dot(right, up)), Is.LessThan(.0001f));
            Assert.That(Vector3.Dot(left, right), Is.LessThan(.1f));
        }

        [Test]
        public void DetourSelectionUsesTheOnlyClearSideBeforeItsPreference()
        {
            Assert.That(InvokeInt("ChooseDetourSide", true, false, -1), Is.EqualTo(1));
            Assert.That(InvokeInt("ChooseDetourSide", false, true, 1), Is.EqualTo(-1));
            Assert.That(InvokeInt("ChooseDetourSide", true, true, -1), Is.EqualTo(-1));
        }

        [Test]
        public void LocomotionFacingUsesActualTangentDisplacementInsteadOfRequestedDirection()
        {
            Vector3 before = new Vector3(10f, 4f, -2f);
            Vector3 after = before + new Vector3(-3f, 7f, 4f);

            Vector3 direction = InvokeVector(
                "ActualTangentDisplacement", before, after, Vector3.up);

            Assert.That(direction.x, Is.EqualTo(-.6f).Within(.0001f));
            Assert.That(direction.z, Is.EqualTo(.8f).Within(.0001f));
            Assert.That(Vector3.Dot(direction, Vector3.up), Is.EqualTo(0f).Within(.0001f));
        }

        [Test]
        public void BlockedOrRadialOnlyLocomotionReportsNoWalkDirection()
        {
            Vector3 before = new Vector3(10f, 4f, -2f);

            Assert.That(InvokeVector(
                "ActualTangentDisplacement", before, before, Vector3.up), Is.EqualTo(Vector3.zero));
            Assert.That(InvokeVector(
                "ActualTangentDisplacement", before, before + Vector3.up, Vector3.up), Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void WaveWrapperScalesAuthoredCombatDistances()
        {
            Assert.That(InvokeFloat("ScaleWorldDistance", 2.6936002f, 3f),
                Is.EqualTo(8.0808f).Within(.001f));
            Assert.That(InvokeFloat("ScaleWorldDistance", 7f, 3f),
                Is.EqualTo(21f).Within(.001f));
        }

        [Test]
        public void StaticObstacleProbeIgnoresActorPlayerGroundAndDynamicEnemies()
        {
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            int playerLayer = LayerMask.NameToLayer("Player");
            GameObject actor = new GameObject("Actor", typeof(BoxCollider));
            GameObject player = new GameObject("Player", typeof(BoxCollider));
            GameObject ally = new GameObject("Ally", typeof(BoxCollider));
            GameObject ground = new GameObject("Ground", typeof(BoxCollider));
            GameObject rock = new GameObject("Rock", typeof(BoxCollider));
            try
            {
                if (enemyLayer >= 0) { actor.layer = enemyLayer; ally.layer = enemyLayer; }
                if (playerLayer >= 0) player.layer = playerLayer;
                MethodInfo method = RequireMethod("IsStaticNavigationObstacle");
                object[] Common(Collider candidate) =>
                    new object[] { candidate, ground.GetComponent<Collider>(), actor.transform, player.transform };

                Assert.That((bool)method.Invoke(null, Common(actor.GetComponent<Collider>())), Is.False);
                Assert.That((bool)method.Invoke(null, Common(player.GetComponent<Collider>())), Is.False);
                Assert.That((bool)method.Invoke(null, Common(ally.GetComponent<Collider>())), Is.False);
                Assert.That((bool)method.Invoke(null, Common(ground.GetComponent<Collider>())), Is.False);
                Assert.That((bool)method.Invoke(null, Common(rock.GetComponent<Collider>())), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(actor);
                UnityEngine.Object.DestroyImmediate(player);
                UnityEngine.Object.DestroyImmediate(ally);
                UnityEngine.Object.DestroyImmediate(ground);
                UnityEngine.Object.DestroyImmediate(rock);
            }
        }

        [Test]
        public void GroundingPreservesScaledControllerFootprintBelowActorRoot()
        {
            GameObject actor = new GameObject("Scaled Grounding Test", typeof(CharacterController));
            try
            {
                actor.transform.localScale = Vector3.one * 3f;
                CharacterController controller = actor.GetComponent<CharacterController>();
                controller.center = new Vector3(0f, 1.7697041f, 0f);
                controller.height = 3.7820687f;
                controller.radius = 1.6936002f;

                float clearance = InvokeFloat(
                    "ControllerRootClearance", controller, actor.transform.position, Vector3.up);

                Assert.That(clearance, Is.EqualTo(.364f).Within(.002f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(actor);
            }
        }

        [Test]
        public void GroundingDoesNotPushRootUndergroundWhenControllerBottomIsAboveIt()
        {
            GameObject actor = new GameObject("Raised Controller Bottom Test", typeof(CharacterController));
            try
            {
                actor.transform.localScale = Vector3.one * 12f;
                CharacterController controller = actor.GetComponent<CharacterController>();
                controller.center = new Vector3(0f, 1.6f, 0f);
                controller.height = 2.9f;
                controller.radius = .75f;

                float clearance = InvokeFloat(
                    "ControllerRootClearance", controller, actor.transform.position, Vector3.up);

                Assert.That(clearance, Is.EqualTo(0f).Within(.0001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(actor);
            }
        }

        [Test]
        public void NavigationProbeUsesScaledLowerCapsuleSphereCenter()
        {
            GameObject actor = new GameObject("Lower Sphere Probe Test", typeof(CharacterController));
            try
            {
                actor.transform.position = new Vector3(4f, 2f, -3f);
                actor.transform.localScale = Vector3.one * 3f;
                CharacterController controller = actor.GetComponent<CharacterController>();
                controller.center = new Vector3(0f, 1.7697041f, 0f);
                controller.height = 3.7820687f;
                controller.radius = 1.6936002f;

                Vector3 center = InvokeVector("ControllerLowerSphereCenter", controller);

                Assert.That(center.x, Is.EqualTo(4f).Within(.001f));
                Assert.That(center.y, Is.EqualTo(6.716f).Within(.002f));
                Assert.That(center.z, Is.EqualTo(-3f).Within(.001f));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(actor);
            }
        }

        [Test]
        public void PlanetSurfaceDiscoverySkipsDisabledReferenceCollider()
        {
            GameObject planet = new GameObject("Planet Ground", typeof(SphereCollider));
            GameObject crater = new GameObject("Active Crater Mesh", typeof(MeshCollider));
            try
            {
                planet.GetComponent<SphereCollider>().enabled = false;
                crater.transform.SetParent(planet.transform, false);
                MeshCollider expected = crater.GetComponent<MeshCollider>();

                MethodInfo method = RequireMethod("FindActivePlanetMeshCollider");
                object actual = method.Invoke(null, new object[] { planet });

                Assert.That(actual, Is.SameAs(expected));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(planet);
            }
        }

        [TestCase("Enemies.EnemyFlyingAI")]
        [TestCase("Enemies.EnemySmallAI")]
        [TestCase("Enemies.EnemyLargeAI")]
        [TestCase("Enemies.BossAstronautAI")]
        [TestCase("Enemies.BossMechAI")]
        public void DeadEnemyUpdateCannotFightDeathCleanupMotion(string concreteTypeName)
        {
            Type concreteType = Type.GetType(concreteTypeName + ", Assembly-CSharp");
            Assert.That(concreteType, Is.Not.Null, concreteTypeName);
            GameObject root = new GameObject(concreteType.Name + " Death Guard Test");
            try
            {
                Component enemy = root.AddComponent(concreteType);
                Vector3 deathMotionPosition = new Vector3(4f, 7f, -2f);
                root.transform.position = deathMotionPosition;

                FieldInfo isDead = EnemyBaseType.GetField("isDead", BindingFlags.NonPublic | BindingFlags.Instance);
                MethodInfo update = concreteType.GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.That(isDead, Is.Not.Null);
                Assert.That(update, Is.Not.Null);
                isDead.SetValue(enemy, true);

                update.Invoke(enemy, null);

                Assert.That(root.transform.position, Is.EqualTo(deathMotionPosition));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DeathFallCannotWaitForeverForAnUnreachableSurface()
        {
            MethodInfo method = EnemyBaseType.GetMethod(
                "ShouldContinueDeathFall", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);

            Assert.That((bool)method.Invoke(null, new object[] { 0.5f, 1.2f, 3f, false }), Is.True);
            Assert.That((bool)method.Invoke(null, new object[] { 2f, 1.2f, 3f, true }), Is.True);
            Assert.That((bool)method.Invoke(null, new object[] { 1.2f, 1.2f, 3f, false }), Is.False);
            Assert.That((bool)method.Invoke(null, new object[] { 4.2f, 1.2f, 3f, true }), Is.False);
        }

        private static Vector3 InvokeVector(string methodName, params object[] arguments)
        {
            object value = RequireMethod(methodName).Invoke(null, arguments);
            return (Vector3)value;
        }

        private static int InvokeInt(string methodName, params object[] arguments)
        {
            object value = RequireMethod(methodName).Invoke(null, arguments);
            return Convert.ToInt32(value);
        }

        private static float InvokeFloat(string methodName, params object[] arguments)
        {
            object value = RequireMethod(methodName).Invoke(null, arguments);
            return Convert.ToSingle(value);
        }

        private static MethodInfo RequireMethod(string methodName)
        {
            Assert.That(EnemyBaseType, Is.Not.Null, "Assembly-CSharp must contain EnemyBase.");
            MethodInfo method = EnemyBaseType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, $"Expected EnemyBase.{methodName}.");
            return method;
        }
    }
}
