using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace WorldRuntime.Tests
{
    public sealed class SphericalPropInstanceDataTests
    {
        private const string TemporaryDataPath =
            "Assets/Tests/EditMode/WorldRuntime/__InstanceData.asset";
        private const string TemporaryMeshPath =
            "Assets/Tests/EditMode/WorldRuntime/__InstanceMesh.asset";
        private const string TemporaryMaterialPath =
            "Assets/Tests/EditMode/WorldRuntime/__InstanceMaterial.mat";

        [Test]
        public void FullPlanetVisibilityRequest_RemainsActiveUntilEveryLeaseIsDisposed()
        {
            Type rendererType = RequireType(
                "WorldRuntime.SphericalPropInstancingRenderer, Assembly-CSharp");
            var owner = new GameObject("Spherical Prop Visibility Test");
            IDisposable firstRequest = null;
            IDisposable secondRequest = null;

            try
            {
                Component renderer = owner.AddComponent(rendererType);
                MethodInfo requestVisibility = rendererType.GetMethod(
                    "RequestFullPlanetVisibility",
                    BindingFlags.Instance | BindingFlags.Public);
                Assert.That(requestVisibility, Is.Not.Null);
                Assert.That(ReadProperty<bool>(renderer, "IsFullPlanetVisibilityRequested"), Is.False);

                firstRequest = (IDisposable)requestVisibility.Invoke(renderer, null);
                secondRequest = (IDisposable)requestVisibility.Invoke(renderer, null);
                Assert.That(ReadProperty<bool>(renderer, "IsFullPlanetVisibilityRequested"), Is.True);

                firstRequest.Dispose();
                firstRequest = null;
                Assert.That(ReadProperty<bool>(renderer, "IsFullPlanetVisibilityRequested"), Is.True);

                secondRequest.Dispose();
                secondRequest = null;
                Assert.That(ReadProperty<bool>(renderer, "IsFullPlanetVisibilityRequested"), Is.False);
            }
            finally
            {
                firstRequest?.Dispose();
                secondRequest?.Dispose();
                UnityEngine.Object.DestroyImmediate(owner);
            }
        }

        [Test]
        public void BinaryAsset_RoundTripsPrototypeAndRendererLocalTrs()
        {
            Type dataType = RequireType("WorldRuntime.SphericalPropInstanceData, Assembly-CSharp");
            Type prototypeType = RequireType(
                "WorldRuntime.SphericalPropInstanceData+Prototype, Assembly-CSharp");
            Type instanceType = RequireType(
                "WorldRuntime.SphericalPropInstanceData+Instance, Assembly-CSharp");

            AssetDatabase.DeleteAsset(TemporaryDataPath);
            AssetDatabase.DeleteAsset(TemporaryMeshPath);
            AssetDatabase.DeleteAsset(TemporaryMaterialPath);

            try
            {
                Mesh mesh = CreateMesh();
                Material material = CreateMaterial();
                AssetDatabase.CreateAsset(mesh, TemporaryMeshPath);
                AssetDatabase.CreateAsset(material, TemporaryMaterialPath);

                object prototype = Activator.CreateInstance(
                    prototypeType,
                    mesh,
                    new[] { material },
                    7,
                    ShadowCastingMode.TwoSided,
                    false,
                    13u,
                    LightProbeUsage.Off,
                    ReflectionProbeUsage.Off);
                Vector3 position = new Vector3(12.5f, -3.25f, 8.75f);
                Quaternion rotation = Quaternion.Euler(17f, -93f, 41f);
                Vector3 scale = new Vector3(2f, 3f, 4f);
                object instance = Activator.CreateInstance(
                    instanceType,
                    0,
                    position,
                    rotation,
                    scale);

                Array prototypes = Array.CreateInstance(prototypeType, 1);
                prototypes.SetValue(prototype, 0);
                Array instances = Array.CreateInstance(instanceType, 1);
                instances.SetValue(instance, 0);
                var data = (ScriptableObject)ScriptableObject.CreateInstance(dataType);
                data.name = "InstanceData";
                dataType.GetMethod(
                        "SetBakedData",
                        new[] { typeof(string), prototypes.GetType(), instances.GetType() })
                    ?.Invoke(data, new object[] { "Generated Planet Vegetation", prototypes, instances });
                AssetDatabase.CreateAsset(data, TemporaryDataPath);
                AssetDatabase.SaveAssets();

                Assert.That(
                    dataType.IsDefined(typeof(PreferBinarySerialization), inherit: false),
                    Is.True,
                    "Planet prop instances must remain binary serialized.");
                byte[] assetBytes = File.ReadAllBytes(TemporaryDataPath);
                Assert.That(assetBytes, Is.Not.Empty);
                Assert.That(
                    System.Text.Encoding.UTF8.GetString(assetBytes, 0, Math.Min(assetBytes.Length, 5)),
                    Is.Not.EqualTo("%YAML"),
                    "The compact instance payload unexpectedly serialized as text YAML.");
                AssetDatabase.ImportAsset(TemporaryDataPath, ImportAssetOptions.ForceSynchronousImport);
                ScriptableObject reloaded = AssetDatabase.LoadAssetAtPath<ScriptableObject>(TemporaryDataPath);
                Assert.That(reloaded, Is.Not.Null);
                Assert.That(ReadProperty<string>(reloaded, "SourceRootName"),
                    Is.EqualTo("Generated Planet Vegetation"));
                Assert.That(ReadProperty<int>(reloaded, "PrototypeCount"), Is.EqualTo(1));
                Assert.That(ReadProperty<int>(reloaded, "InstanceCount"), Is.EqualTo(1));

                object reloadedPrototype = First(ReadProperty<IEnumerable>(reloaded, "Prototypes"));
                Assert.That(ReadProperty<Mesh>(reloadedPrototype, "Mesh"), Is.SameAs(mesh));
                Assert.That(ReadProperty<int>(reloadedPrototype, "Layer"), Is.EqualTo(7));
                Assert.That(ReadProperty<ShadowCastingMode>(reloadedPrototype, "ShadowCastingMode"),
                    Is.EqualTo(ShadowCastingMode.TwoSided));
                Assert.That(ReadProperty<bool>(reloadedPrototype, "ReceiveShadows"), Is.False);
                Assert.That(ReadProperty<uint>(reloadedPrototype, "RenderingLayerMask"), Is.EqualTo(13u));

                object reloadedInstance = First(ReadProperty<IEnumerable>(reloaded, "Instances"));
                Assert.That(ReadProperty<int>(reloadedInstance, "PrototypeIndex"), Is.Zero);
                Assert.That(ReadProperty<Vector3>(reloadedInstance, "Position"), Is.EqualTo(position));
                Assert.That(ReadProperty<Quaternion>(reloadedInstance, "Rotation"), Is.EqualTo(rotation));
                Assert.That(ReadProperty<Vector3>(reloadedInstance, "Scale"), Is.EqualTo(scale));
                MethodInfo buildLocalMatrix = instanceType.GetMethod("BuildLocalMatrix");
                Assert.That(buildLocalMatrix, Is.Not.Null);
                Matrix4x4 actual = (Matrix4x4)buildLocalMatrix.Invoke(reloadedInstance, null);
                AssertMatrixEqual(Matrix4x4.TRS(position, rotation, scale), actual);
            }
            finally
            {
                AssetDatabase.DeleteAsset(TemporaryDataPath);
                AssetDatabase.DeleteAsset(TemporaryMeshPath);
                AssetDatabase.DeleteAsset(TemporaryMaterialPath);
                AssetDatabase.Refresh();
            }
        }

        private static Mesh CreateMesh()
        {
            var mesh = new Mesh { name = "World Runtime Test Mesh" };
            mesh.vertices = new[] { Vector3.zero, Vector3.right, Vector3.up };
            mesh.triangles = new[] { 0, 1, 2 };
            return mesh;
        }

        private static Material CreateMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ??
                            Shader.Find("Hidden/InternalErrorShader");
            Assert.That(shader, Is.Not.Null);
            var material = new Material(shader) { name = "World Runtime Test Material" };
            material.enableInstancing = true;
            return material;
        }

        private static T ReadProperty<T>(object instance, string name)
        {
            PropertyInfo property = instance.GetType().GetProperty(name);
            Assert.That(property, Is.Not.Null, name);
            return (T)property.GetValue(instance);
        }

        private static object First(IEnumerable source)
        {
            foreach (object value in source)
            {
                return value;
            }

            Assert.Fail("Expected a non-empty serialized collection.");
            return null;
        }

        private static Type RequireType(string qualifiedName)
        {
            Type type = Type.GetType(qualifiedName);
            Assert.That(type, Is.Not.Null, qualifiedName);
            return type;
        }

        private static void AssertMatrixEqual(Matrix4x4 expected, Matrix4x4 actual)
        {
            for (int row = 0; row < 4; row++)
            {
                for (int column = 0; column < 4; column++)
                {
                    Assert.That(actual[row, column], Is.EqualTo(expected[row, column]).Within(0.0001f),
                        $"Matrix element [{row}, {column}] differs.");
                }
            }
        }
    }
}
