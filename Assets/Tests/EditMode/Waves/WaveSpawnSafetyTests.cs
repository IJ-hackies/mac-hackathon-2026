using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Gameplay.Waves.Tests
{
    public sealed class WaveSpawnSafetyTests
    {
        private static readonly System.Type FootprintType = System.Type.GetType("Gameplay.Waves.WaveSpawnFootprint, Assembly-CSharp");
        private static readonly System.Type SamplerType = System.Type.GetType("Gameplay.Waves.WaveSurfaceSpawnSampler, Assembly-CSharp");

        [Test]
        public void FootprintIncludesInactiveNestedStageAndFinalThreeXSpawnScale()
        {
            GameObject root = new GameObject("Boss Root");
            GameObject stage = new GameObject("Inactive Mech Stage", typeof(BoxCollider));
            try
            {
                root.transform.localScale = new Vector3(2f, 1f, 1f);
                stage.transform.SetParent(root.transform, false);
                stage.transform.localPosition = new Vector3(3f, 2f, 0f);
                stage.GetComponent<BoxCollider>().size = new Vector3(2f, 2f, 2f);
                stage.SetActive(false);

                object footprint = CreateFootprint(root, 3f);

                Assert.That(Get<int>(footprint, "ShapeCount"), Is.EqualTo(1));
                Assert.That(Get<float>(footprint, "TangentRadius"), Is.EqualTo(24f).Within(.001f));
                Assert.That(Get<float>(footprint, "OutwardExtent"), Is.EqualTo(9f).Within(.001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FootprintIncludesCharacterControllerWhenNoColliderExists()
        {
            GameObject root = new GameObject("Enemy Root");
            GameObject movement = new GameObject("Movement", typeof(CharacterController));
            try
            {
                movement.transform.SetParent(root.transform, false);
                CharacterController controller = movement.GetComponent<CharacterController>();
                controller.radius = 1.25f;
                controller.height = 4f;
                controller.center = new Vector3(0f, 2f, 0f);

                object footprint = CreateFootprint(root, 3f);

                Assert.That(Get<int>(footprint, "ShapeCount"), Is.EqualTo(1));
                Assert.That(Get<float>(footprint, "TangentRadius"), Is.EqualTo(3.75f).Within(.001f));
                Assert.That(Get<float>(footprint, "OutwardExtent"), Is.EqualTo(12f).Within(.001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TriggerOnlyDamageVolumeDoesNotInflateSpawnFootprint()
        {
            GameObject root = new GameObject("Mech Root");
            GameObject body = new GameObject("Body", typeof(BoxCollider));
            GameObject legHitVolume = new GameObject("Leg Damage Trigger", typeof(SphereCollider));
            try
            {
                body.transform.SetParent(root.transform, false);
                body.GetComponent<BoxCollider>().size = Vector3.one * 2f;
                legHitVolume.transform.SetParent(root.transform, false);
                SphereCollider trigger = legHitVolume.GetComponent<SphereCollider>();
                trigger.radius = 50f;
                trigger.isTrigger = true;

                object footprint = CreateFootprint(root, 3f);

                Assert.That(Get<int>(footprint, "ShapeCount"), Is.EqualTo(1));
                Assert.That(Get<float>(footprint, "TangentRadius"), Is.EqualTo(Mathf.Sqrt(18f)).Within(.001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AreaFallbackDirectionUsesUnitRadialsRatherThanAnInsidePlanetChord()
        {
            Vector3 planetCenter = new Vector3(10f, -3f, 4f);
            var poles = new List<Vector3>
            {
                planetCenter + new Vector3(100f, 0f, 0f),
                planetCenter + new Vector3(0f, 120f, 0f),
                planetCenter + new Vector3(0f, 0f, 140f)
            };

            MethodInfo average = RequireType(FootprintType, "WaveSpawnFootprint").GetMethod(
                "AverageRadialDirection", BindingFlags.Public | BindingFlags.Static);
            Assert.That(average, Is.Not.Null);
            Vector3 direction = (Vector3)average.Invoke(null, new object[] { poles, planetCenter });
            Vector3 surfacePoint = planetCenter + direction * 120f;

            Assert.That(direction.magnitude, Is.EqualTo(1f).Within(.0001f));
            Assert.That(Vector3.Distance(surfacePoint, planetCenter), Is.EqualTo(120f).Within(.0001f));
            Assert.That(Vector3.Distance(surfacePoint, planetCenter), Is.GreaterThan(Vector3.Distance((poles[0] + poles[1] + poles[2]) / 3f, planetCenter)));
        }

        [Test]
        public void ClearanceAllowsSupportingGroundButRejectsAProspectiveObstacle()
        {
            GameObject prospectiveRoot = new GameObject("Prospective Enemy", typeof(BoxCollider));
            GameObject ground = new GameObject("Planet Ground", typeof(BoxCollider));
            GameObject rock = new GameObject("Rock", typeof(BoxCollider));
            try
            {
                BoxCollider body = prospectiveRoot.GetComponent<BoxCollider>();
                body.center = Vector3.up;
                body.size = new Vector3(1f, 2f, 1f);
                object footprint = CreateFootprint(prospectiveRoot, 3f);
                body.enabled = false; // The prospective object is not part of the physics scene yet.

                BoxCollider groundCollider = ground.GetComponent<BoxCollider>();
                ground.transform.position = new Vector3(0f, -.5f, 0f);
                ground.transform.localScale = new Vector3(100f, 1f, 100f);
                rock.GetComponent<BoxCollider>().size = Vector3.one;
                rock.SetActive(false);
                Physics.SyncTransforms();

                System.Type samplerType = RequireType(SamplerType, "WaveSurfaceSpawnSampler");
                object sampler = System.Activator.CreateInstance(samplerType);
                MethodInfo clear = samplerType.GetMethod(
                    "IsFootprintClear", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.That(clear, Is.Not.Null);
                object[] arguments = { Vector3.zero, Quaternion.identity, footprint, groundCollider };
                Assert.That((bool)clear.Invoke(sampler, arguments), Is.True);

                rock.transform.position = new Vector3(1f, 3f, 0f);
                rock.SetActive(true);
                Physics.SyncTransforms();
                Assert.That((bool)clear.Invoke(sampler, arguments), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(prospectiveRoot);
                Object.DestroyImmediate(ground);
                Object.DestroyImmediate(rock);
            }
        }

        [Test]
        public void ArenaSurfaceUsesConfiguredPlanetWhenBoundaryPolesAreFarAboveGround()
        {
            GameObject center = new GameObject("Planet Center");
            GameObject ground = new GameObject("Planet Ground", typeof(BoxCollider));
            GameObject prop = new GameObject("Arena Prop", typeof(BoxCollider));
            try
            {
                ground.transform.SetParent(center.transform, false);
                ground.transform.position = Vector3.right * 110f;
                ground.GetComponent<BoxCollider>().size = new Vector3(10f, 100f, 100f);
                prop.transform.position = Vector3.right * 145f;
                prop.GetComponent<BoxCollider>().size = new Vector3(4f, 100f, 100f);
                Physics.SyncTransforms();

                System.Type samplerType = RequireType(SamplerType, "WaveSurfaceSpawnSampler");
                object sampler = System.Activator.CreateInstance(samplerType);
                MethodInfo findSurface = samplerType.GetMethod(
                    "TryFindArenaSurface", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.That(findSurface, Is.Not.Null);
                object[] arguments =
                {
                    Vector3.right * 185f,
                    Vector3.left,
                    160f,
                    center.transform.position,
                    center.transform,
                    default(RaycastHit)
                };

                Assert.That((bool)findSurface.Invoke(sampler, arguments), Is.True);
                RaycastHit hit = (RaycastHit)arguments[5];
                Assert.That(hit.collider, Is.SameAs(ground.GetComponent<BoxCollider>()));
                Assert.That(hit.point.x, Is.EqualTo(115f).Within(.001f));
            }
            finally
            {
                Object.DestroyImmediate(prop);
                Object.DestroyImmediate(center);
            }
        }

        private static object CreateFootprint(GameObject root, float scale)
        {
            System.Type type = RequireType(FootprintType, "WaveSpawnFootprint");
            MethodInfo method = type.GetMethod("FromPrefab", BindingFlags.Public | BindingFlags.Static);
            Assert.That(method, Is.Not.Null);
            return method.Invoke(null, new object[] { root, scale });
        }


        private static T Get<T>(object value, string propertyName)
        {
            PropertyInfo property = value.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
            Assert.That(property, Is.Not.Null, propertyName);
            return (T)property.GetValue(value);
        }

        private static System.Type RequireType(System.Type type, string typeName)
        {
            Assert.That(type, Is.Not.Null, $"Assembly-CSharp must contain {typeName}.");
            return type;
        }
    }
}
