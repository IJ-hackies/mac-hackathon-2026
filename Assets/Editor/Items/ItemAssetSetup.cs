using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace ItemsEditor
{
    /// <summary>
    /// Imports the Ultimate Space Kit pickup FBXs into the project-owned runtime
    /// art library and configures them as visual-only (non-collidable) models.
    /// Mirrors WorldEditor.UltimateSpaceRockAssetSetup's copy/configure/validate shape.
    /// </summary>
    public static class ItemAssetSetup
    {
        private const string RuntimeModelFolder = "Assets/Art/Models/Items";
        private const string RuntimeMaterialFolder = "Assets/Art/Materials/Items";
        private const string ItemMaterialPath = RuntimeMaterialFolder + "/M_PlanetItem.mat";
        private const string SpacePalettePath = "Assets/Art/Textures/T_SpacePalette.png";
        private const string VendorRelativeFolder =
            "asset packs/visuals/Ultimate Space Kit - March 2023/Items/FBX";

        private static readonly string[] ModelNames =
        {
            "Pickup_Health",
            "Pickup_Bullets",
            "Pickup_Thunder",
        };

        [MenuItem("Tools/Items/Prepare Item Assets")]
        public static void PrepareAssets()
        {
            EnsureFolder(RuntimeModelFolder);
            EnsureFolder(RuntimeMaterialFolder);
            CopyVendorModelsWhenMissing();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            Material itemMaterial = CreateOrUpdateItemMaterial();
            var configuredPaths = new List<string>(ModelNames.Length);
            foreach (string modelName in ModelNames)
            {
                string modelPath = $"{RuntimeModelFolder}/{modelName}.fbx";
                ConfigureModelImporter(modelPath, itemMaterial);
                configuredPaths.Add(modelPath);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            ValidateConfiguredModels(configuredPaths, itemMaterial);

            Debug.Log(
                $"Item Asset Setup: imported and validated {configuredPaths.Count} " +
                $"Ultimate Space Kit pickup models in {RuntimeModelFolder}.");
        }

        private static void CopyVendorModelsWhenMissing()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                throw new InvalidOperationException(
                    "Item Asset Setup could not resolve the project root.");
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
                        $"Item Asset Setup is missing vendor source '{source}'.", source);
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
                    $"Item Asset Setup could not import '{modelPath}'.");
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

            // Pickups are never solid obstacles - ItemPickup adds its own trigger
            // SphereCollider at runtime instead of relying on generated mesh collision.
            if (importer.addCollider)
            {
                importer.addCollider = false;
                requiresReimport = true;
            }

            var identifier = new AssetImporter.SourceAssetIdentifier(typeof(Material), "Atlas");
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

        private static Material CreateOrUpdateItemMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                throw new InvalidOperationException(
                    "Item Asset Setup: Universal Render Pipeline/Lit is unavailable.");
            }

            Texture2D palette = AssetDatabase.LoadAssetAtPath<Texture2D>(SpacePalettePath);
            if (palette == null)
            {
                throw new InvalidOperationException(
                    $"Item Asset Setup: required palette is missing at {SpacePalettePath}.");
            }

            Material material = AssetDatabase.LoadAssetAtPath<Material>(ItemMaterialPath);
            if (material == null)
            {
                material = new Material(shader)
                {
                    name = Path.GetFileNameWithoutExtension(ItemMaterialPath)
                };
                AssetDatabase.CreateAsset(material, ItemMaterialPath);
            }
            else
            {
                material.shader = shader;
            }

            material.SetTexture("_BaseMap", palette);
            material.SetColor("_BaseColor", Color.white);
            material.SetColor("_Color", Color.white);
            material.SetFloat("_Metallic", 0.1f);
            material.SetFloat("_Smoothness", 0.4f);
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
                        $"Item Asset Setup: Unity could not load {modelPath} as a model.");
                }

                Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
                if (renderers.Length == 0)
                {
                    throw new InvalidOperationException(
                        $"Item Asset Setup: {modelPath} has no renderable meshes.");
                }

                foreach (Renderer renderer in renderers)
                {
                    foreach (Material material in renderer.sharedMaterials)
                    {
                        if (material != expectedMaterial)
                        {
                            throw new InvalidOperationException(
                                $"Item Asset Setup: {modelPath} is not remapped to " +
                                $"{ItemMaterialPath}.");
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
