using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace WorldRuntime.Tests
{
    public sealed class SphericalPropBakeSceneTests
    {
        private const string SampleScenePath = "Assets/Scenes/SampleScene.unity";
        private const string VegetationRootName = "Generated Planet Vegetation";
        private const string RockRootName = "Generated Planet Rocks";
        private const string VegetationAssetPath =
            "Assets/Art/Generated/PlanetProps/SampleScene_Vegetation.asset";
        private const string RockAssetPath =
            "Assets/Art/Generated/PlanetProps/SampleScene_Rocks.asset";

        [Test]
        public void SampleScene_UsesTwoCompactDataSetsAndRetainsOnlyRockCollisionObjects()
        {
            Type rendererType = RequireType(
                "WorldRuntime.SphericalPropInstancingRenderer, Assembly-CSharp");
            Type dataType = RequireType(
                "WorldRuntime.SphericalPropInstanceData, Assembly-CSharp");
            Scene scene = SceneManager.GetSceneByPath(SampleScenePath);
            bool openedForTest = !scene.IsValid() || !scene.isLoaded;
            if (openedForTest)
            {
                scene = EditorSceneManager.OpenScene(SampleScenePath, OpenSceneMode.Additive);
            }

            try
            {
                GameObject vegetationRoot = FindRoot(scene, VegetationRootName);
                Assert.That(vegetationRoot, Is.Null,
                    "Vegetation authoring instances must not remain in the runtime scene.");

                GameObject rockRoot = FindRoot(scene, RockRootName);
                Assert.That(rockRoot, Is.Not.Null,
                    "The rock hierarchy remains solely to supply collision.");
                Assert.That(rockRoot.GetComponentsInChildren<MeshRenderer>(true), Is.Empty);
                Assert.That(rockRoot.GetComponentsInChildren<MeshFilter>(true), Is.Empty);
                MeshCollider[] rockColliders = rockRoot.GetComponentsInChildren<MeshCollider>(true);
                Assert.That(rockColliders, Has.Length.EqualTo(1100));
                foreach (MeshCollider collider in rockColliders)
                {
                    Assert.That(collider, Is.Not.Null);
                    Assert.That(collider.enabled, Is.True, collider.name);
                    Assert.That(collider.sharedMesh, Is.Not.Null, collider.name);
                }

                Component[] renderers = scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren(rendererType, true))
                    .ToArray();
                Assert.That(renderers, Has.Length.EqualTo(1));
                IEnumerable assignedDataSets = ReadProperty<IEnumerable>(
                    renderers[0], "BakedInstanceDataSets");
                List<ScriptableObject> dataSets = assignedDataSets.Cast<ScriptableObject>().ToList();
                Assert.That(dataSets, Has.Count.EqualTo(2));
                Assert.That(dataSets.All(AssetDatabase.Contains), Is.True);

                ScriptableObject vegetationData = AssetDatabase.LoadAssetAtPath<ScriptableObject>(
                    VegetationAssetPath);
                ScriptableObject rockData = AssetDatabase.LoadAssetAtPath<ScriptableObject>(RockAssetPath);
                Assert.That(vegetationData, Is.Not.Null);
                Assert.That(rockData, Is.Not.Null);
                Assert.That(vegetationData.GetType(), Is.EqualTo(dataType));
                Assert.That(rockData.GetType(), Is.EqualTo(dataType));
                Assert.That(dataSets, Is.EquivalentTo(new[] { vegetationData, rockData }));

                AssertDataSet(vegetationData, VegetationRootName, 16000, 18);
                AssertDataSet(rockData, RockRootName, 1100, 7);
                Assert.That(
                    ReadProperty<int>(vegetationData, "InstanceCount") +
                    ReadProperty<int>(rockData, "InstanceCount"),
                    Is.EqualTo(17100));
            }
            finally
            {
                if (openedForTest && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, removeScene: true);
                }
            }
        }

        private static void AssertDataSet(
            ScriptableObject data,
            string expectedRoot,
            int expectedInstances,
            int expectedPrototypes)
        {
            Assert.That(ReadProperty<string>(data, "SourceRootName"), Is.EqualTo(expectedRoot));
            Assert.That(ReadProperty<int>(data, "InstanceCount"), Is.EqualTo(expectedInstances));
            Assert.That(ReadProperty<int>(data, "PrototypeCount"), Is.EqualTo(expectedPrototypes));

            List<object> prototypes = ReadProperty<IEnumerable>(data, "Prototypes").Cast<object>().ToList();
            List<object> instances = ReadProperty<IEnumerable>(data, "Instances").Cast<object>().ToList();
            Assert.That(prototypes, Has.Count.EqualTo(expectedPrototypes));
            Assert.That(instances, Has.Count.EqualTo(expectedInstances));
            foreach (object prototype in prototypes)
            {
                Mesh mesh = ReadProperty<Mesh>(prototype, "Mesh");
                Assert.That(mesh, Is.Not.Null);
                Assert.That(mesh.subMeshCount, Is.GreaterThan(0), mesh.name);
                MethodInfo materialForSubmesh = prototype.GetType().GetMethod("GetMaterialForSubmesh");
                Assert.That(materialForSubmesh, Is.Not.Null);
                for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                {
                    Material material = (Material)materialForSubmesh.Invoke(prototype, new object[] { submesh });
                    Assert.That(material, Is.Not.Null, $"{mesh.name} submesh {submesh}");
                    Assert.That(material.enableInstancing, Is.True, material.name);
                }
            }

            foreach (object instance in instances)
            {
                int prototypeIndex = ReadProperty<int>(instance, "PrototypeIndex");
                Assert.That(prototypeIndex, Is.InRange(0, prototypes.Count - 1));
                AssertFinite(ReadProperty<Vector3>(instance, "Position"));
                AssertFinite(ReadProperty<Quaternion>(instance, "Rotation"));
                AssertFinite(ReadProperty<Vector3>(instance, "Scale"));
                Assert.That(ReadProperty<Vector3>(instance, "Scale").x, Is.Not.EqualTo(0f));
                Assert.That(ReadProperty<Vector3>(instance, "Scale").y, Is.Not.EqualTo(0f));
                Assert.That(ReadProperty<Vector3>(instance, "Scale").z, Is.Not.EqualTo(0f));
            }
        }

        private static GameObject FindRoot(Scene scene, string name)
        {
            return scene.GetRootGameObjects().FirstOrDefault(root => root.name == name);
        }

        private static T ReadProperty<T>(object instance, string name)
        {
            PropertyInfo property = instance.GetType().GetProperty(name);
            Assert.That(property, Is.Not.Null, name);
            return (T)property.GetValue(instance);
        }

        private static Type RequireType(string qualifiedName)
        {
            Type type = Type.GetType(qualifiedName);
            Assert.That(type, Is.Not.Null, qualifiedName);
            return type;
        }

        private static void AssertFinite(Vector3 value)
        {
            Assert.That(IsFinite(value.x) && IsFinite(value.y) && IsFinite(value.z), Is.True);
        }

        private static void AssertFinite(Quaternion value)
        {
            Assert.That(
                IsFinite(value.x) && IsFinite(value.y) &&
                IsFinite(value.z) && IsFinite(value.w),
                Is.True);
        }

        private static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
