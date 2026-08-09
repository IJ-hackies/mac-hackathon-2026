using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace TutorialEditor
{
    /// Imports every FBX in the Modular SciFi MegaKit pack (Walls, Platforms, Columns, Props,
    /// Decals, Aliens - ~190 pieces) into the project so they're all available to drag into a
    /// hand-built scene, and binds each one to a small shared material family built from the
    /// kit's own trim-sheet textures - so any combination of pieces automatically "matches", the
    /// same idea `Assets/Editor/World/LandingBaseAssetSetup.cs` already uses for its curated
    /// Ultimate Space Kit imports. Vendor sources under `asset packs/` are never modified; only
    /// project-owned runtime copies are created/updated, and `AssetDatabase.CopyAsset` silently
    /// fails outside `Assets/`, so plain `System.IO.File.Copy` is used instead (same workaround
    /// the Mech import under `PlayerSceneSetup` already needed).
    public static class ModularKitAssetSetup
    {
        private const string VendorRoot = "asset packs/visuals/Modular SciFi MegaKit[Standard]/FBX (Unity)/";
        private const string VendorTextureRoot = "asset packs/visuals/Modular SciFi MegaKit[Standard]/Textures/";
        public const string ModelFolder = "Assets/Art/Models/Environment/ModularSciFi";
        private const string MaterialFolder = "Assets/Art/Materials/ModularSciFi";
        private const string TextureFolder = "Assets/Art/Textures/ModularSciFi";

        // The kit's FBX (Unity) export groups every piece under these six category folders.
        private static readonly string[] VendorSubfolders = { "Walls", "Platforms", "Columns", "Props", "Decals", "Aliens" };

        // Grid unit and module heights measured directly from the vendor OBJ export (not
        // guessed): every floor/wall/ceiling piece is one 4x4 tile (or an exact multiple), the
        // lower wall band is 3 units tall, and the upper "Top" band adds another 2 for one clean
        // 5-unit floor-to-floor height with no seams. Kept here for reference while hand-building.
        public const float TileSize = 4f;
        public const float LowerWallHeight = 3f;
        public const float UpperWallHeight = 2f;
        public const float LevelHeight = LowerWallHeight + UpperWallHeight;

        [MenuItem("Tools/Tutorial/Import Modular Kit Assets")]
        public static void ImportModularKitAssets()
        {
            EnsureFolder(ModelFolder);
            EnsureFolder(MaterialFolder);
            EnsureFolder(TextureFolder);

            // The whole vendor texture set is small (~20 files) - copy all of it so any material
            // slot on any piece can be wired up later, not just the ones this pass builds
            // materials for.
            foreach (string vendorTexturePath in Directory.GetFiles(VendorTextureRoot, "*.png"))
            {
                CopyTexture(Path.GetFileName(vendorTexturePath), isNormalMap: Path.GetFileName(vendorTexturePath).Contains("_Normal"));
            }

            Material trim01 = CreateOrUpdateMaterial(MaterialFolder + "/M_Trim01.mat",
                Load("T_Trim_01_BaseColor.png"), Load("T_Trim_01_Normal.png"));
            Material trim02 = CreateOrUpdateMaterial(MaterialFolder + "/M_Trim02.mat",
                Load("T_Trim_02_BaseColor.png"), Load("T_Trim_02_Normal.png"));
            Material trim03 = CreateOrUpdateMaterial(MaterialFolder + "/M_Trim03.mat",
                Load("T_Trim_03_BaseColor.png"), Load("T_Trim_03_Normal.png"));
            Material trim03Dark = CreateOrUpdateMaterial(MaterialFolder + "/M_Trim03Dark.mat",
                Load("T_Trim_03_Cables.png"), Load("T_Trim_03_Normal.png"));
            Material paddedWall = CreateOrUpdateMaterial(MaterialFolder + "/M_PaddedWall.mat",
                Load("T_PaddedWall_BaseColor.png"), Load("T_PaddedWall_Normal.png"));
            Material decal = CreateOrUpdateMaterial(MaterialFolder + "/M_Decal.mat", Load("T_Decals.png"), null);
            Material glass = CreateOrUpdateGlassMaterial(MaterialFolder + "/M_Glass.mat");

            // Slot names read directly off the vendor .mtl files. AddRemap is a no-op for any
            // slot name a given mesh doesn't actually have, so applying the full dictionary to
            // every piece (walls, floors, doors, aliens, decals, everything) is safe.
            var remaps = new Dictionary<string, Material>
            {
                { "MI_Trim_01", trim01 },
                { "MI_Trim_02", trim02 },
                { "MI_Trim_03", trim03 },
                { "MI_Trim_03_Dark", trim03Dark },
                { "MI_PaddedWall", paddedWall },
                { "M_Decal_White", decal },
                { "M_Glass", glass },
            };

            int imported = 0;
            foreach (string subfolder in VendorSubfolders)
            {
                string vendorFolder = VendorRoot + subfolder;
                if (!Directory.Exists(vendorFolder))
                {
                    Debug.LogWarning($"ModularKitAssetSetup: vendor folder missing at {vendorFolder}.");
                    continue;
                }

                foreach (string vendorFile in Directory.GetFiles(vendorFolder, "*.fbx"))
                {
                    string modelPath = CopyModel(Path.GetFileName(vendorFile), subfolder);
                    if (modelPath == null) continue;
                    ConfigureModel(modelPath, remaps);
                    imported++;
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"ModularKitAssetSetup: imported {imported} pieces into {ModelFolder}/<Walls|Platforms|Columns|Props|Decals|Aliens>.");
        }

        public static GameObject LoadPiece(string subfolder, string name)
        {
            string path = $"{ModelFolder}/{subfolder}/{name}.fbx";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null)
            {
                Debug.LogError($"ModularKitAssetSetup: {path} is missing - run " +
                                "Tools/Tutorial/Import Modular Kit Assets first.");
            }
            return model;
        }

        private static string CopyModel(string fileName, string vendorSubfolder)
        {
            string vendorPath = $"{VendorRoot}{vendorSubfolder}/{fileName}";
            string projectFolder = $"{ModelFolder}/{vendorSubfolder}";
            string projectPath = $"{projectFolder}/{fileName}";

            EnsureFolder(projectFolder);

            if (!File.Exists(vendorPath))
            {
                Debug.LogError($"ModularKitAssetSetup: vendor source missing at {vendorPath}.");
                return null;
            }

            if (!File.Exists(projectPath))
            {
                File.Copy(vendorPath, projectPath);
                AssetDatabase.ImportAsset(projectPath, ImportAssetOptions.ForceSynchronousImport);
            }

            return projectPath;
        }

        private static void ConfigureModel(string modelPath, IReadOnlyDictionary<string, Material> remaps)
        {
            var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null)
            {
                Debug.LogError($"ModularKitAssetSetup: no imported FBX at {modelPath}.");
                return;
            }

            bool dirty = false;
            if (importer.importAnimation) { importer.importAnimation = false; dirty = true; }
            if (importer.isReadable) { importer.isReadable = false; dirty = true; }
            if (!importer.addCollider) { importer.addCollider = true; dirty = true; }

            var existing = importer.GetExternalObjectMap();
            foreach (var remap in remaps)
            {
                var identifier = new AssetImporter.SourceAssetIdentifier(typeof(Material), remap.Key);
                if (!existing.TryGetValue(identifier, out var current) || current != remap.Value)
                {
                    importer.AddRemap(identifier, remap.Value);
                    dirty = true;
                }
            }

            if (dirty) importer.SaveAndReimport();
        }

        private static Texture2D Load(string fileName) => AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureFolder}/{fileName}");

        private static void CopyTexture(string fileName, bool isNormalMap)
        {
            string vendorPath = VendorTextureRoot + fileName;
            string projectPath = $"{TextureFolder}/{fileName}";

            if (!File.Exists(projectPath))
            {
                File.Copy(vendorPath, projectPath);
                AssetDatabase.ImportAsset(projectPath, ImportAssetOptions.ForceSynchronousImport);
            }

            var importer = AssetImporter.GetAtPath(projectPath) as TextureImporter;
            if (importer != null && isNormalMap && importer.textureType != TextureImporterType.NormalMap)
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.SaveAndReimport();
            }
        }

        private static Material CreateOrUpdateMaterial(string path, Texture2D baseMap, Texture2D normalMap)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetTexture("_BaseMap", baseMap);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Metallic", 0.4f);
            material.SetFloat("_Smoothness", 0.35f);
            material.SetTexture("_BumpMap", normalMap);
            if (normalMap != null)
            {
                material.EnableKeyword("_NORMALMAP");
                material.SetFloat("_BumpScale", 1f);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        // Simple transparent blue-tinted glass for WallWindow's M_Glass slot - not the vendor's
        // own look (unauthored in this free pack), just a reasonable placeholder to swap out.
        private static Material CreateOrUpdateGlassMaterial(string path)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = Path.GetFileNameWithoutExtension(path) };
                AssetDatabase.CreateAsset(material, path);
            }

            material.SetFloat("_Surface", 1f); // Transparent
            material.SetFloat("_Blend", 0f);
            material.SetColor("_BaseColor", new Color(0.55f, 0.75f, 0.9f, 0.25f));
            material.SetFloat("_Metallic", 0.1f);
            material.SetFloat("_Smoothness", 0.9f);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            string folderName = Path.GetFileName(path);
            if (!string.IsNullOrEmpty(parent) && !AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
