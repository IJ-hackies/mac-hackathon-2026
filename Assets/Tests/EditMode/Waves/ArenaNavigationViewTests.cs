using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Gameplay.Waves.Tests
{
    public sealed class ArenaNavigationViewTests
    {
        private const float PlanetRadius = 100f;

        [Test]
        public void CameraTangentCardinalBearingsMapToTheExpectedScreenEdges()
        {
            using (NavigationFixture fixture = new NavigationFixture())
            {
                AssertDirection(Vector2.up, fixture.Place(Vector3.forward));
                AssertDirection(Vector2.right, fixture.Place(Vector3.right));
                AssertDirection(Vector2.down, fixture.Place(Vector3.back));
                AssertDirection(Vector2.left, fixture.Place(Vector3.left));
            }
        }

        [Test]
        public void RearSideCrossingDoesNotJumpToTheOppositeEdge()
        {
            using (NavigationFixture fixture = new NavigationFixture())
            {
                Vector2 justBeforeRear = fixture.Place(HeadingFromCameraForward(89f));
                Vector2 justAfterRear = fixture.Place(HeadingFromCameraForward(91f));

                Assert.That(justBeforeRear.x, Is.GreaterThan(0f));
                Assert.That(justAfterRear.x, Is.GreaterThan(0f));
                Assert.That(Vector2.Dot(justBeforeRear, justAfterRear), Is.GreaterThan(.99f));
            }
        }

        [Test]
        public void AntipodalHysteresisRetainsThePreviousGreatCircleBranch()
        {
            using (NavigationFixture fixture = new NavigationFixture())
            {
                Vector3 heading = (Vector3.right + Vector3.forward).normalized;
                Vector2 establishedBranch = fixture.Place(TargetOnGreatCircle(160f, heading));
                Vector2 beforeAntipode = fixture.Place(TargetOnGreatCircle(179f, heading));
                Vector2 atAntipode = fixture.Place(TargetOnGreatCircle(180f, heading));
                Vector2 afterAntipode = fixture.Place(TargetOnGreatCircle(181f, heading));

                AssertDirection(new Vector2(heading.x, heading.z).normalized, establishedBranch);
                Assert.That(Vector2.Dot(establishedBranch, beforeAntipode), Is.GreaterThan(.99f));
                Assert.That(Vector2.Dot(beforeAntipode, atAntipode), Is.GreaterThan(.99f));
                Assert.That(Vector2.Dot(atAntipode, afterAntipode), Is.GreaterThan(.99f));
            }
        }

        private static Vector3 HeadingFromCameraForward(float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
        }

        private static Vector3 TargetOnGreatCircle(float degrees, Vector3 heading)
        {
            float radians = degrees * Mathf.Deg2Rad;
            return (Vector3.up * Mathf.Cos(radians) + heading * Mathf.Sin(radians)) * PlanetRadius;
        }

        private static void AssertDirection(Vector2 expected, Vector2 actual)
        {
            Assert.That(Vector2.Dot(expected, actual), Is.GreaterThan(.999f));
        }

        private sealed class NavigationFixture : IDisposable
        {
            private readonly GameObject _root;
            private readonly RectTransform _marker;
            private readonly Component _view;
            private readonly MethodInfo _setNavigationGeometry;

            public NavigationFixture()
            {
                _root = new GameObject("Arena Navigation Test", typeof(RectTransform));
                RectTransform bounds = _root.GetComponent<RectTransform>();
                bounds.sizeDelta = new Vector2(1000f, 600f);

                var cameraObject = new GameObject("Navigation Camera", typeof(Camera));
                cameraObject.transform.SetParent(_root.transform, false);
                cameraObject.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);

                _marker = new GameObject("Marker", typeof(RectTransform)).GetComponent<RectTransform>();
                _marker.SetParent(_root.transform, false);

                Type viewType = RequireType("Player.UI.Waves.ArenaNavigationView, Assembly-CSharp");
                _view = _root.AddComponent(viewType);
                Invoke(_view, "Configure", null, cameraObject.GetComponent<Camera>(), _marker, bounds, null, null);
                _setNavigationGeometry = viewType.GetMethod("SetNavigationGeometry", BindingFlags.Public | BindingFlags.Instance);
                Assert.That(_setNavigationGeometry, Is.Not.Null);
            }

            public Vector2 Place(Vector3 targetDirection)
            {
                Vector3 target = targetDirection.normalized * PlanetRadius;
                _setNavigationGeometry.Invoke(_view, new object[] { Vector3.up * PlanetRadius, target, Vector3.zero, "ARENA" });
                return _marker.anchoredPosition.normalized;
            }

            public void Dispose()
            {
                UnityEngine.Object.DestroyImmediate(_root);
            }
        }

        private static Type RequireType(string name)
        {
            Type type = Type.GetType(name);
            Assert.That(type, Is.Not.Null, name);
            return type;
        }

        private static void Invoke(Component target, string methodName, params object[] arguments)
        {
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(method, Is.Not.Null, methodName);
            method.Invoke(target, arguments);
        }
    }
}
