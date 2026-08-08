using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace WorldEditor
{
    /// <summary>
    /// Applies the project's URP materials to the vendor FBX material slots used
    /// by the curated landing-base kit. The source packs remain untouched.
    /// </summary>
    public static class LandingBaseAssetSetup
    {
        private const string ModelFolder = "Assets/Art/Models/Environment/LandingBase";
        private const string MaterialFolder = "Assets/Art/Materials/LandingBase";
        private const string SciFiTextureFolder = "Assets/Art/Textures/ModularSciFi";
        private const string SpacePalettePath = "Assets/Art/Textures/T_SpacePalette.png";

        private const string SpaceKitMaterialPath =
            MaterialFolder + "/M_LandingBaseSpaceKit.mat";
        private const string SciFiTrim01MaterialPath =
            MaterialFolder + "/M_LandingBaseSciFiTrim01Red.mat";
        private const string SciFiTrim02MaterialPath =
            MaterialFolder + "/M_LandingBaseSciFiTrim02Red.mat";

        private static readonly string[] SpaceKitModels =
        {
            "Base_Large",
            "Building_L",
            "GeodesicDome",
            "House_Cylinder",
            "House_Door",
            "House_Long",
            "House_Open",
            "House_OpenBack",
            "House_Single_Support",
            "House_Single",
            "MetalSupport",
            "Stairs",
            "Roof_Antenna",
            "Roof_Opening",
            "Roof_VentL",
            "Roof_Radar",
            "SolarPanel_Roof",
            "SolarPanel_Structure"
        };

        [MenuItem("Tools/Planet Design/Configure Landing Base Assets")]
        public static void ConfigureLandingBaseAssets()
        {
            EnsureFolder(MaterialFolder);
            ConfigureSciFiTextures();

            Texture2D spacePalette = RequireAsset<Texture2D>(SpacePalettePath);
            Material spaceKitMaterial = CreateOrUpdateMaterial(
                SpaceKitMaterialPath,
                spacePalette,
                normalMap: null,
                metallic: 0f,
                smoothness: 0.15f);

            Material trim01 = CreateOrUpdateMaterial(
                SciFiTrim01MaterialPath,
                RequireAsset<Texture2D>(SciFiTextureFolder + "/T_Trim_01_BaseColor_Red.png"),
                RequireAsset<Texture2D>(SciFiTextureFolder + "/T_Trim_01_Normal.png"),
                metallic: 0.55f,
                smoothness: 0.4f);
            Material trim02 = CreateOrUpdateMaterial(
                SciFiTrim02MaterialPath,
                RequireAsset<Texture2D>(SciFiTextureFolder + "/T_Trim_02_BaseColor_Red.png"),
                RequireAsset<Texture2D>(SciFiTextureFolder + "/T_Trim_02_Normal.png"),
                metallic: 0.55f,
                smoothness: 0.4f);

            var configuredPaths = new List<string>();
            foreach (string modelName in SpaceKitModels)
            {
                string modelPath = $"{ModelFolder}/{modelName}.fbx";
                ConfigureModel(modelPath, new Dictionary<string, Material>
                {
                    { "Atlas", spaceKitMaterial }
                });
                configuredPaths.Add(modelPath);
            }

            string columnPath = $"{ModelFolder}/Column_Hollow.fbx";
            ConfigureModel(columnPath, new Dictionary<string, Material>
            {
                { "MI_Trim_01", trim01 },
                { "MI_Trim_02", trim02 }
            });
            configuredPaths.Add(columnPath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            ValidateConfiguredModels(configuredPaths);

            Debug.Log(
                $"Landing Base Asset Setup: configured and validated {configuredPaths.Count} models in {ModelFolder}.");
        }

        [MenuItem("Tools/Planet Design/Configure Landing Base Assets", true)]
        private static bool ValidateConfigureLandingBaseAssets()
        {
            return Directory.Exists(ModelFolder);
        }

        private static void ConfigureModel(
            string modelPath,
            IReadOnlyDictionary<string, Material> materialRemaps)
        {
            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null)
            {
                throw new InvalidOperationException(
                    $"Landing Base Asset Setup: no imported FBX found at {modelPath}.");
            }

            bool requiresReimport = false;
            if (importer.importAnimation)
            {
                importer.importAnimation = false;
                requiresReimport = true;
            }

            if (importer.isReadable)
            {
                importer.isReadable = false;
                requiresReimport = true;
            }

            if (!importer.addCollider)
            {
                importer.addCollider = true;
                requiresReimport = true;
            }

            IReadOnlyDictionary<AssetImporter.SourceAssetIdentifier, UnityEngine.Object> existingRemaps =
                importer.GetExternalObjectMap();

            foreach (KeyValuePair<string, Material> remap in materialRemaps)
            {
                var identifier = new AssetImporter.SourceAssetIdentifier(
                    typeof(Material),
                    remap.Key);
                if (!existingRemaps.TryGetValue(identifier, out UnityEngine.Object current) ||
                    current != remap.Value)
                {
                    importer.AddRemap(identifier, remap.Value);
                    requiresReimport = true;
                }
            }

            if (requiresReimport)
            {
                importer.SaveAndReimport();
            }
        }

        private static Material CreateOrUpdateMaterial(
            string path,
            Texture2D baseMap,
            Texture2D normalMap,
            float metallic,
            float smoothness)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "Landing Base Asset Setup: Universal Render Pipeline/Lit shader is unavailable.");
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = Path.GetFileNameWithoutExtension(path)
                };
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = shader;
            }

            material.SetTexture("_BaseMap", baseMap);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);
            material.SetTexture("_BumpMap", normalMap);
            if (normalMap != null)
            {
                material.EnableKeyword("_NORMALMAP");
                material.SetFloat("_BumpScale", 1f);
            }
            else
            {
                material.DisableKeyword("_NORMALMAP");
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureSciFiTextures()
        {
            string[] normalMapPaths =
            {
                SciFiTextureFolder + "/T_Trim_01_Normal.png",
                SciFiTextureFolder + "/T_Trim_02_Normal.png"
            };

            foreach (string path in normalMapPaths)
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    throw new InvalidOperationException(
                        $"Landing Base Asset Setup: no texture found at {path}.");
                }

                if (importer.textureType != TextureImporterType.NormalMap || importer.sRGBTexture)
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    importer.sRGBTexture = false;
                    importer.SaveAndReimport();
                }
            }

            string[] ormPaths =
            {
                SciFiTextureFolder + "/T_Trim_01_ORM.png",
                SciFiTextureFolder + "/T_Trim_02_ORM.png"
            };

            foreach (string path in ormPaths)
            {
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    throw new InvalidOperationException(
                        $"Landing Base Asset Setup: no texture found at {path}.");
                }

                if (importer.sRGBTexture)
                {
                    importer.sRGBTexture = false;
                    importer.SaveAndReimport();
                }
            }
        }

        private static void ValidateConfiguredModels(IEnumerable<string> modelPaths)
        {
            foreach (string modelPath in modelPaths)
            {
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                if (model == null)
                {
                    throw new InvalidOperationException(
                        $"Landing Base Asset Setup: Unity could not load {modelPath} as a model.");
                }

                if (model.GetComponentsInChildren<Collider>(true).Length == 0)
                {
                    throw new InvalidOperationException(
                        $"Landing Base Asset Setup: {modelPath} imported without collision geometry.");
                }
            }
        }

        private static T RequireAsset<T>(string path) where T : UnityEngine.Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
            {
                throw new InvalidOperationException(
                    $"Landing Base Asset Setup: required asset is missing at {path}.");
            }

            return asset;
        }

        private static void EnsureFolder(string folderPath)
        {
            string[] segments = folderPath.Split('/');
            string current = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string next = current + "/" + segments[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, segments[index]);
                }

                current = next;
            }
        }

    }
}
