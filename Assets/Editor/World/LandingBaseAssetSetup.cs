using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        private const string VendorTrim01EmissivePath =
            "asset packs/visuals/Modular SciFi MegaKit[Standard]/Textures/T_Trim_01_Emissive.png";
        private const string Trim01EmissionMaskPath =
            SciFiTextureFolder + "/T_Trim_01_EmissionMask.png";

        private const string SpaceKitMaterialPath =
            MaterialFolder + "/M_LandingBaseSpaceKit.mat";
        private const string SciFiTrim01MaterialPath =
            MaterialFolder + "/M_LandingBaseSciFiTrim01Red.mat";
        private const string SciFiTrim02MaterialPath =
            MaterialFolder + "/M_LandingBaseSciFiTrim02Red.mat";
        private const string Arena1Trim01MaterialPath =
            MaterialFolder + "/M_Arena1Trim01DarkOrange.mat";
        private const string Arena2Trim01MaterialPath =
            MaterialFolder + "/M_Arena2Trim01BloodRed.mat";

        private static readonly Color LandingBaseEmission = new Color(0f, 3f, 2.2f, 1f);
        private static readonly Color Arena1Emission = new Color(3.5f, 1.25f, 0.08f, 1f);
        private static readonly Color Arena2Emission = new Color(3.5f, 0.06f, 0.03f, 1f);

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
            "Ramp",
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
            Texture2D trim01EmissionMask = CreateOrUpdateTrim01EmissionMask();
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
                smoothness: 0.4f,
                emissionMap: trim01EmissionMask,
                emissionColor: LandingBaseEmission);
            Material trim02 = CreateOrUpdateMaterial(
                SciFiTrim02MaterialPath,
                RequireAsset<Texture2D>(SciFiTextureFolder + "/T_Trim_02_BaseColor_Red.png"),
                RequireAsset<Texture2D>(SciFiTextureFolder + "/T_Trim_02_Normal.png"),
                metallic: 0.55f,
                smoothness: 0.4f);

            CreateOrUpdateMaterial(
                Arena1Trim01MaterialPath,
                baseMap: null,
                normalMap: RequireAsset<Texture2D>(
                    SciFiTextureFolder + "/T_Trim_01_Normal.png"),
                metallic: 0.15f,
                smoothness: 0.28f,
                baseColor: new Color(0.65f, 0.22f, 0.035f, 1f),
                emissionMap: trim01EmissionMask,
                emissionColor: Arena1Emission);
            CreateOrUpdateMaterial(
                Arena2Trim01MaterialPath,
                baseMap: null,
                normalMap: RequireAsset<Texture2D>(
                    SciFiTextureFolder + "/T_Trim_01_Normal.png"),
                metallic: 0.15f,
                smoothness: 0.28f,
                baseColor: new Color(0.3f, 0.012f, 0.02f, 1f),
                emissionMap: trim01EmissionMask,
                emissionColor: Arena2Emission);

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
            float smoothness,
            Color? baseColor = null,
            Texture2D emissionMap = null,
            Color? emissionColor = null)
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
            material.SetColor("_BaseColor", baseColor ?? Color.white);
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

            Color resolvedEmissionColor = emissionColor ?? Color.black;
            bool emissionEnabled = emissionMap != null &&
                                   resolvedEmissionColor.maxColorComponent > 0f;
            material.SetTexture("_EmissionMap", emissionMap);
            material.SetColor("_EmissionColor", resolvedEmissionColor);
            if (emissionEnabled)
            {
                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                material.globalIlluminationFlags =
                    MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static Texture2D CreateOrUpdateTrim01EmissionMask()
        {
            if (!File.Exists(VendorTrim01EmissivePath))
            {
                throw new InvalidOperationException(
                    $"Landing Base Asset Setup: vendor emission texture is missing at " +
                    $"{VendorTrim01EmissivePath}.");
            }

            byte[] sourceBytes = File.ReadAllBytes(VendorTrim01EmissivePath);
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            Texture2D mask = null;
            byte[] encodedMask;
            try
            {
                if (!source.LoadImage(sourceBytes, markNonReadable: false))
                {
                    throw new InvalidOperationException(
                        "Landing Base Asset Setup: Unity could not decode the vendor " +
                        "Trim01 emission texture.");
                }

                Color32[] sourcePixels = source.GetPixels32();
                var maskPixels = new Color32[sourcePixels.Length];
                for (int index = 0; index < sourcePixels.Length; index++)
                {
                    Color32 pixel = sourcePixels[index];
                    byte intensity = Math.Max(pixel.r, Math.Max(pixel.g, pixel.b));
                    maskPixels[index] = new Color32(intensity, intensity, intensity, 255);
                }

                mask = new Texture2D(
                    source.width,
                    source.height,
                    TextureFormat.RGBA32,
                    false,
                    true);
                mask.SetPixels32(maskPixels);
                mask.Apply(updateMipmaps: false, makeNoLongerReadable: false);
                encodedMask = mask.EncodeToPNG();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(source);
                if (mask != null)
                {
                    UnityEngine.Object.DestroyImmediate(mask);
                }
            }

            bool requiresImport = !File.Exists(Trim01EmissionMaskPath) ||
                                  !File.ReadAllBytes(Trim01EmissionMaskPath)
                                      .SequenceEqual(encodedMask);
            if (requiresImport)
            {
                File.WriteAllBytes(Trim01EmissionMaskPath, encodedMask);
                AssetDatabase.ImportAsset(
                    Trim01EmissionMaskPath,
                    ImportAssetOptions.ForceSynchronousImport |
                    ImportAssetOptions.ForceUpdate);
            }

            var importer = AssetImporter.GetAtPath(Trim01EmissionMaskPath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException(
                    $"Landing Base Asset Setup: generated emission mask was not imported at " +
                    $"{Trim01EmissionMaskPath}.");
            }

            bool importerChanged = importer.textureType != TextureImporterType.Default ||
                                   importer.sRGBTexture ||
                                   !importer.mipmapEnabled ||
                                   importer.alphaSource != TextureImporterAlphaSource.None ||
                                   importer.maxTextureSize != 1024 ||
                                   importer.textureCompression != TextureImporterCompression.CompressedHQ;
            if (importerChanged)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;
                importer.mipmapEnabled = true;
                importer.alphaSource = TextureImporterAlphaSource.None;
                importer.maxTextureSize = 1024;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;
                importer.SaveAndReimport();
            }

            return RequireAsset<Texture2D>(Trim01EmissionMaskPath);
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
