using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace WorldEditor
{
    /// <summary>
    /// Imports every Ultimate Space Kit rock FBX into the project-owned runtime
    /// art library and configures the models as static planet obstacles.
    /// </summary>
    public static class UltimateSpaceRockAssetSetup
    {
        private const string RuntimeModelFolder =
            "Assets/Art/Models/Environment/PlanetRocks";
        private const string RuntimeMaterialFolder =
            "Assets/Art/Materials/PlanetRocks";
        private const string RockMaterialPath =
            RuntimeMaterialFolder + "/M_PlanetRock.mat";
        private const string SpacePalettePath =
            "Assets/Art/Textures/T_SpacePalette.png";
        private const string VendorRelativeFolder =
            "asset packs/visuals/Ultimate Space Kit - March 2023/Environment/FBX";

        private static readonly string[] ModelNames =
        {
            "Rock_1",
            "Rock_2",
            "Rock_3",
            "Rock_4",
            "Rock_Large_1",
            "Rock_Large_2",
            "Rock_Large_3"
        };

        [MenuItem("Tools/Planet Design/Prepare Planet Rock Assets")]
        public static void PrepareAssets()
        {
            EnsureFolder(RuntimeModelFolder);
            EnsureFolder(RuntimeMaterialFolder);
            CopyVendorModelsWhenMissing();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            Material rockMaterial = CreateOrUpdateRockMaterial();
            var configuredPaths = new List<string>(ModelNames.Length);
            foreach (string modelName in ModelNames)
            {
                string modelPath = $"{RuntimeModelFolder}/{modelName}.fbx";
                ConfigureModelImporter(modelPath, rockMaterial);
                configuredPaths.Add(modelPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ValidateConfiguredModels(configuredPaths, rockMaterial);

            Debug.Log(
                $"Planet Rock Asset Setup: imported and validated {configuredPaths.Count} " +
                $"Ultimate Space Kit rock models in {RuntimeModelFolder}.");
        }

        private static void CopyVendorModelsWhenMissing()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                throw new InvalidOperationException(
                    "Planet Rock Asset Setup could not resolve the project root.");
            }

            string vendorFolder = Path.Combine(
                projectRoot,
                VendorRelativeFolder.Replace('/', Path.DirectorySeparatorChar));
            string runtimeFolder = Path.Combine(
                projectRoot,
                RuntimeModelFolder.Replace('/', Path.DirectorySeparatorChar));

            foreach (string modelName in ModelNames)
            {
                string source = Path.Combine(vendorFolder, modelName + ".fbx");
                string destination = Path.Combine(runtimeFolder, modelName + ".fbx");
                if (!File.Exists(source))
                {
                    throw new FileNotFoundException(
                        $"Planet Rock Asset Setup is missing vendor source '{source}'.",
                        source);
                }

                if (!File.Exists(destination))
                {
                    File.Copy(source, destination, overwrite: false);
                }
            }
        }

        private static void ConfigureModelImporter(string modelPath, Material material)
        {
            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null)
            {
                throw new InvalidOperationException(
                    $"Planet Rock Asset Setup could not import '{modelPath}'.");
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

            var identifier = new AssetImporter.SourceAssetIdentifier(
                typeof(Material),
                "Atlas");
            IReadOnlyDictionary<AssetImporter.SourceAssetIdentifier, UnityEngine.Object> remaps =
                importer.GetExternalObjectMap();
            if (!remaps.TryGetValue(identifier, out UnityEngine.Object current) ||
                current != material)
            {
                importer.AddRemap(identifier, material);
                requiresReimport = true;
            }

            if (requiresReimport)
            {
                importer.SaveAndReimport();
            }
        }

        private static Material CreateOrUpdateRockMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "Planet Rock Asset Setup: Universal Render Pipeline/Lit is unavailable.");
            }

            Texture2D palette = AssetDatabase.LoadAssetAtPath<Texture2D>(SpacePalettePath);
            if (palette == null)
            {
                throw new InvalidOperationException(
                    $"Planet Rock Asset Setup: required palette is missing at {SpacePalettePath}.");
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(RockMaterialPath);
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = Path.GetFileNameWithoutExtension(RockMaterialPath)
                };
                AssetDatabase.CreateAsset(material, RockMaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            material.SetTexture("_BaseMap", palette);
            material.SetColor("_BaseColor", Color.white);
            material.SetColor("_Color", Color.white);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 0.08f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ValidateConfiguredModels(
            IEnumerable<string> modelPaths,
            Material expectedMaterial)
        {
            foreach (string modelPath in modelPaths)
            {
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                if (model == null)
                {
                    throw new InvalidOperationException(
                        $"Planet Rock Asset Setup: Unity could not load {modelPath} as a model.");
                }

                Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0)
                {
                    throw new InvalidOperationException(
                        $"Planet Rock Asset Setup: {modelPath} has no renderable meshes.");
                }

                if (model.GetComponentsInChildren<Collider>(true).Length == 0)
                {
                    throw new InvalidOperationException(
                        $"Planet Rock Asset Setup: {modelPath} imported without collision geometry.");
                }

                foreach (Renderer renderer in renderers)
                {
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        if (material != expectedMaterial)
                        {
                            throw new InvalidOperationException(
                                $"Planet Rock Asset Setup: {modelPath} is not remapped to " +
                                $"{RockMaterialPath}.");
                        }
                    }
                }
            }
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
