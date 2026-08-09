using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Gameplay.Waves.Tests
{
    public sealed class BossCutsceneRadialFrameTests
    {
        [Test]
        public void PlanetaryOrbitUsesTheArbitraryRadialUpAndStaysOutsideTheSurface()
        {
            Vector3 center = new Vector3(31f, -17f, 8f);
            Vector3 radialUp = new Vector3(2f, 3f, -4f).normalized;
            const float planetRadius = 150f;
            Vector3 mechSurfacePoint = center + radialUp * planetRadius;

            object frame = CreateFrame(
                mechSurfacePoint,
                center,
                true,
                radialUp);

            // Arena2's 3x wrapper and 4x mech make the normal 2.6-unit camera distance 31.2 world units.
            object pose = CalculateOrbit(
                frame,
                2.2f * 12f * 0.85f,
                2.6f * 12f,
                247f);

            Vector3 up = Get<Vector3>(frame, "Up");
            Vector3 forward = Get<Vector3>(frame, "Forward");
            Vector3 right = Get<Vector3>(frame, "Right");
            Vector3 position = Get<Vector3>(pose, "Position");
            Vector3 lookAt = Get<Vector3>(pose, "LookAt");

            Assert.That(Get<bool>(frame, "IsPlanetary"), Is.True);
            Assert.That(Vector3.Dot(up, radialUp), Is.GreaterThan(.9999f));
            Assert.That(Mathf.Abs(Vector3.Dot(forward, up)), Is.LessThan(.0001f));
            Assert.That(Mathf.Abs(Vector3.Dot(right, up)), Is.LessThan(.0001f));
            Assert.That((position - center).magnitude, Is.GreaterThan(planetRadius));
            Assert.That((position - center).magnitude, Is.GreaterThanOrEqualTo(planetRadius + .25f));
            Assert.That(Vector3.Dot(position - lookAt, up), Is.EqualTo(0f).Within(.001f));
            Assert.That(Vector3.Dot(Get<Vector3>(pose, "Up"), radialUp), Is.GreaterThan(.9999f));
        }

        [Test]
        public void FlatPrototypeFallbackRetainsWorldUpAndNormalOrbitCoordinates()
        {
            Vector3 surfacePoint = new Vector3(12f, 0f, -9f);
            object frame = CreateFrame(
                surfacePoint,
                Vector3.zero,
                false,
                Vector3.forward);
            object pose = CalculateOrbit(
                frame,
                3f,
                7f,
                0f);

            Assert.That(Get<bool>(frame, "IsPlanetary"), Is.False);
            Assert.That(Get<Vector3>(frame, "Up"), Is.EqualTo(Vector3.up));
            Assert.That(Get<Vector3>(frame, "Forward"), Is.EqualTo(Vector3.forward));
            Assert.That(Get<Vector3>(pose, "LookAt"), Is.EqualTo(surfacePoint + Vector3.up * 3f));
            Assert.That(Get<Vector3>(pose, "Position"), Is.EqualTo(surfacePoint + Vector3.up * 3f + Vector3.back * 7f));
            Assert.That(Get<Vector3>(pose, "Up"), Is.EqualTo(Vector3.up));
        }

        [Test]
        public void DegeneratePlanetCenterInputFallsBackToANonZeroFlatFrame()
        {
            object frame = CreateFrame(
                Vector3.zero,
                Vector3.zero,
                true,
                Vector3.up);
            object pose = CalculateOrbit(
                frame,
                0f,
                0f,
                90f);

            Assert.That(Get<bool>(frame, "IsPlanetary"), Is.False);
            Assert.That(Get<Vector3>(frame, "Up").sqrMagnitude, Is.GreaterThan(.99f));
            Assert.That(Get<Vector3>(frame, "Forward").sqrMagnitude, Is.GreaterThan(.99f));
            Assert.That(Get<Vector3>(frame, "Right").sqrMagnitude, Is.GreaterThan(.99f));
            Assert.That(Get<Vector3>(pose, "Up"), Is.EqualTo(Vector3.up));
            Assert.That(Get<Vector3>(pose, "Position"), Is.EqualTo(Vector3.zero));
        }

        private static object CreateFrame(Vector3 surfacePoint, Vector3 planetCenter, bool hasPlanetGround, Vector3 forwardHint)
        {
            return InvokeStatic("CreateCutsceneSurfaceFrame", surfacePoint, planetCenter, hasPlanetGround, forwardHint);
        }

        private static object CalculateOrbit(object frame, float radialHeight, float orbitDistance, float orbitDegrees)
        {
            return InvokeStatic("CalculateCutsceneOrbitPose", frame, radialHeight, orbitDistance, orbitDegrees);
        }

        private static T Get<T>(object value, string propertyName)
        {
            PropertyInfo property = value.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, propertyName);
            return (T)property.GetValue(value);
        }

        private static object InvokeStatic(string methodName, params object[] arguments)
        {
            Type controllerType = Type.GetType("Enemies.BossFightController, Assembly-CSharp");
            Assert.That(controllerType, Is.Not.Null, "BossFightController must compile into Assembly-CSharp.");
            MethodInfo method = controllerType.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(method, Is.Not.Null, methodName);
            return method.Invoke(null, arguments);
        }
    }
}
