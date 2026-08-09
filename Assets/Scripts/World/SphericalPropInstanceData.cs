using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace WorldRuntime
{
    /// <summary>
    /// Compact, scene-independent source data for the generated planet-prop renderer.
    /// An editor baker owns population of this asset; runtime code only reads it.
    /// </summary>
    [CreateAssetMenu(
        fileName = "SphericalPropInstanceData",
        menuName = "World/Spherical Prop Instance Data")]
    [PreferBinarySerialization]
    public sealed class SphericalPropInstanceData : ScriptableObject
    {
        [SerializeField] private string sourceRootName;
        [SerializeField] private Prototype[] prototypes = Array.Empty<Prototype>();
        [SerializeField] private Instance[] instances = Array.Empty<Instance>();

        /// <summary>
        /// Exact legacy generated-root name represented by this dataset. The renderer
        /// skips that root when this asset is valid, allowing categories to migrate
        /// independently while rock collision objects remain in the scene.
        /// </summary>
        public string SourceRootName => sourceRootName;

        /// <summary>Reusable mesh, material, and render-state records.</summary>
        public IReadOnlyList<Prototype> Prototypes => prototypes;

        /// <summary>Renderer-local transforms referring to <see cref="Prototypes"/>.</summary>
        public IReadOnlyList<Instance> Instances => instances;

        public int PrototypeCount => prototypes?.Length ?? 0;
        public int InstanceCount => instances?.Length ?? 0;

        /// <summary>
        /// Replaces the complete baked payload. This is intended for editor baking tools;
        /// callers must mark the asset dirty and save it through the Unity Editor API.
        /// </summary>
        public void SetBakedData(
            string sourceRoot,
            Prototype[] prototypeData,
            Instance[] instanceData)
        {
            sourceRootName = sourceRoot;
            prototypes = prototypeData != null
                ? (Prototype[])prototypeData.Clone()
                : Array.Empty<Prototype>();
            instances = instanceData != null
                ? (Instance[])instanceData.Clone()
                : Array.Empty<Instance>();
        }

        /// <summary>
        /// Replaces the payload without changing its category. Use the overload with
        /// <paramref name="sourceRoot"/> when a baker creates a new asset.
        /// </summary>
        public void SetBakedData(Prototype[] prototypeData, Instance[] instanceData)
        {
            SetBakedData(sourceRootName, prototypeData, instanceData);
        }

        [Serializable]
        public struct Prototype
        {
            [SerializeField] private Mesh mesh;
            [SerializeField] private Material[] materials;
            [SerializeField] private int layer;
            [SerializeField] private ShadowCastingMode shadowCastingMode;
            [SerializeField] private bool receiveShadows;
            [SerializeField] private uint renderingLayerMask;
            [SerializeField] private LightProbeUsage lightProbeUsage;
            [SerializeField] private ReflectionProbeUsage reflectionProbeUsage;

            public Prototype(
                Mesh mesh,
                Material[] materials,
                int layer,
                ShadowCastingMode shadowCastingMode,
                bool receiveShadows,
                uint renderingLayerMask,
                LightProbeUsage lightProbeUsage,
                ReflectionProbeUsage reflectionProbeUsage)
            {
                this.mesh = mesh;
                this.materials = materials != null
                    ? (Material[])materials.Clone()
                    : Array.Empty<Material>();
                this.layer = layer;
                this.shadowCastingMode = shadowCastingMode;
                this.receiveShadows = receiveShadows;
                this.renderingLayerMask = renderingLayerMask;
                this.lightProbeUsage = lightProbeUsage;
                this.reflectionProbeUsage = reflectionProbeUsage;
            }

            public Mesh Mesh => mesh;
            public int Layer => layer;
            public ShadowCastingMode ShadowCastingMode => shadowCastingMode;
            public bool ReceiveShadows => receiveShadows;
            public uint RenderingLayerMask => renderingLayerMask;
            public LightProbeUsage LightProbeUsage => lightProbeUsage;
            public ReflectionProbeUsage ReflectionProbeUsage => reflectionProbeUsage;

            public Material GetMaterialForSubmesh(int submeshIndex)
            {
                if (materials == null || materials.Length == 0)
                {
                    return null;
                }

                return materials[Mathf.Min(submeshIndex, materials.Length - 1)];
            }
        }

        [Serializable]
        public struct Instance
        {
            [SerializeField] private int prototypeIndex;
            [SerializeField] private Vector3 position;
            [SerializeField] private Quaternion rotation;
            [SerializeField] private Vector3 scale;

            public Instance(
                int prototypeIndex,
                Vector3 position,
                Quaternion rotation,
                Vector3 scale)
            {
                this.prototypeIndex = prototypeIndex;
                this.position = position;
                this.rotation = rotation;
                this.scale = scale;
            }

            public int PrototypeIndex => prototypeIndex;
            /// <summary>Position in the renderer component's local space.</summary>
            public Vector3 Position => position;
            /// <summary>Rotation in the renderer component's local space.</summary>
            public Quaternion Rotation => rotation;
            /// <summary>Scale in the renderer component's local space.</summary>
            public Vector3 Scale => scale;

            public Matrix4x4 BuildLocalMatrix()
            {
                return Matrix4x4.TRS(position, rotation, scale);
            }
        }
    }
}
