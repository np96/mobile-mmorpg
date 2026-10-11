using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SessionRpg.Editor.Assets
{
    public enum FortressSourceModelId
    {
        WallStraight = 0,
        WallBroken = 1,
        Tower = 2,
        Brazier = 3,
        Wardstone = 4,
        FoundationRing = 5,
    }

    public readonly struct FortressSourceModelContract
    {
        public FortressSourceModelContract(
            FortressSourceModelId id,
            string fileName,
            Vector3 minimumSize,
            Vector3 maximumSize,
            int maximumTriangles,
            int expectedSubmeshCount,
            bool requiresGroundPivot = true,
            float expectedMinimumY = 0f,
            float expectedMaximumY = 0f)
        {
            Id = id;
            FileName = fileName;
            MinimumSize = minimumSize;
            MaximumSize = maximumSize;
            MaximumTriangles = maximumTriangles;
            ExpectedSubmeshCount = expectedSubmeshCount;
            RequiresGroundPivot = requiresGroundPivot;
            ExpectedMinimumY = expectedMinimumY;
            ExpectedMaximumY = expectedMaximumY;
        }

        public FortressSourceModelId Id { get; }

        public string FileName { get; }

        public Vector3 MinimumSize { get; }

        public Vector3 MaximumSize { get; }

        public int MaximumTriangles { get; }

        public int ExpectedSubmeshCount { get; }

        public bool RequiresGroundPivot { get; }

        public float ExpectedMinimumY { get; }

        public float ExpectedMaximumY { get; }

        public string AssetPath => FortressSourceArtValidator.SourceFolder + "/" + FileName;

        public string ModelName => System.IO.Path.GetFileNameWithoutExtension(FileName);
    }

    public sealed class FortressSourceArtSet
    {
        private readonly GameObject[] _models;

        internal FortressSourceArtSet(GameObject[] models)
        {
            _models = models ?? throw new ArgumentNullException(nameof(models));
        }

        public GameObject Get(FortressSourceModelId id)
        {
            int index = (int)id;
            if (index < 0 || index >= _models.Length || _models[index] == null)
            {
                throw new ArgumentOutOfRangeException(nameof(id), id, null);
            }

            return _models[index];
        }
    }

    public static class FortressSourceArtValidator
    {
        public const string SourceFolder =
            "Assets/SessionRpg/SourceArt/Environment";

        public const string ManifestAssetPath =
            SourceFolder + "/FortressKit.manifest.json";

        public const float FoundationMinimumPlanarRadius = 21.25f;
        public const float FoundationMaximumPlanarRadius = 25.5f;
        public const float FoundationMinimumY = -1.8f;
        public const float FoundationMaximumY = -0.45f;

        public const string ConfigureMenuPath =
            "Tools/Session RPG/Assets/Configure Original Fortress Imports";

        public const string ValidateMenuPath =
            "Tools/Session RPG/Assets/Validate Original Fortress Source Art";

        private const string ConfigureInstruction =
            "Run Tools > Session RPG > Assets > Configure Original Fortress Imports once, then rebuild the playable foundation.";

        private const int MaximumRendererCountPerSource = 1;
        private const int MaximumSubmeshCountPerSource = 2;
        private const int MaximumVerticesPerMesh = 65535;
        private const float GroundPivotTolerance = 0.08f;
        private const float FoundationBoundsTolerance = 0.03f;
        private const float TransformTolerance = 0.0001f;

        private static readonly string[] StoneMaterialSlots =
        {
            "Fortress_Stone",
        };

        private static readonly string[] StoneAndEmberMaterialSlots =
        {
            "Fortress_Stone",
            "Fortress_Ember",
        };

        private static readonly FortressSourceModelContract[] RequiredModels =
        {
            new FortressSourceModelContract(
                FortressSourceModelId.WallStraight,
                "FortressWallStraight.fbx",
                new Vector3(7.1f, 2.55f, 1.05f),
                new Vector3(8.25f, 3.85f, 1.95f),
                1400,
                1),
            new FortressSourceModelContract(
                FortressSourceModelId.WallBroken,
                "FortressWallBroken.fbx",
                new Vector3(7.1f, 1.35f, 1.05f),
                new Vector3(8.25f, 3.85f, 1.95f),
                1400,
                1),
            new FortressSourceModelContract(
                FortressSourceModelId.Tower,
                "FortressTower.fbx",
                new Vector3(7.1f, 4.35f, 3.45f),
                new Vector3(8.25f, 6.05f, 4.95f),
                3200,
                2),
            new FortressSourceModelContract(
                FortressSourceModelId.Brazier,
                "FortressBrazier.fbx",
                new Vector3(0.9f, 1.65f, 0.9f),
                new Vector3(1.8f, 2.8f, 1.8f),
                650,
                2),
            new FortressSourceModelContract(
                FortressSourceModelId.Wardstone,
                "Wardstone.fbx",
                new Vector3(1.85f, 2.25f, 1.85f),
                new Vector3(2.05f, 2.4f, 2.05f),
                1500,
                2),
            new FortressSourceModelContract(
                FortressSourceModelId.FoundationRing,
                "FortressFoundationRing.fbx",
                new Vector3(50.7f, 1.3f, 50.4f),
                new Vector3(50.9f, 1.4f, 50.65f),
                8000,
                1,
                false,
                FoundationMinimumY,
                FoundationMaximumY),
        };

        public static int ContractCount => RequiredModels.Length;

        public static FortressSourceModelContract GetContract(int index)
        {
            if (index < 0 || index >= RequiredModels.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return RequiredModels[index];
        }

        [MenuItem(ValidateMenuPath, priority = 2080)]
        public static void ValidateFromMenu()
        {
            try
            {
                ValidateOrThrow();
                const string message =
                    "Original fortress source models and importer settings are valid.";
                Debug.Log(message);
                ShowDialog("Fortress source validation", message);
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogError(exception.Message);
                ShowDialog("Fortress source validation failed", exception.Message);
            }
        }

        [MenuItem(ConfigureMenuPath, priority = 2081)]
        public static void ConfigureFromMenu()
        {
            try
            {
                int changedCount = ConfigureImports();
                ValidateOrThrow();
                string message = changedCount == 0
                    ? "Original fortress import settings were already correct."
                    : "Configured and reimported " + changedCount +
                      " original fortress model(s).";
                Debug.Log(message);
                ShowDialog("Fortress source imports", message);
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogError(exception.Message);
                ShowDialog("Fortress source import failed", exception.Message);
            }
        }

        public static void ValidateForCommandLine()
        {
            ValidateOrThrow();
            Debug.Log("Original fortress source-art validation passed.");
        }

        public static void ConfigureForCommandLine()
        {
            int changedCount = ConfigureImports();
            ValidateOrThrow();
            Debug.Log(
                "Original fortress import configuration passed; changed " +
                changedCount + " asset(s).");
        }

        public static FortressSourceArtSet LoadValidated()
        {
            ValidateOrThrow();
            GameObject[] models = new GameObject[RequiredModels.Length];
            for (int index = 0; index < RequiredModels.Length; index++)
            {
                models[(int)RequiredModels[index].Id] =
                    AssetDatabase.LoadAssetAtPath<GameObject>(
                        RequiredModels[index].AssetPath);
            }

            return new FortressSourceArtSet(models);
        }

        public static void ValidateOrThrow()
        {
            var errors = new List<string>();
            int[] importedTriangleCounts = new int[RequiredModels.Length];
            for (int index = 0; index < RequiredModels.Length; index++)
            {
                importedTriangleCounts[index] = ValidateModel(
                    RequiredModels[index],
                    errors);
            }

            ValidateManifest(importedTriangleCounts, errors);

            if (errors.Count > 0)
            {
                throw new InvalidOperationException(BuildErrorMessage(errors));
            }
        }

        public static int ConfigureImports()
        {
            var missing = new List<string>();
            for (int index = 0; index < RequiredModels.Length; index++)
            {
                FortressSourceModelContract contract = RequiredModels[index];
                if (!(AssetImporter.GetAtPath(contract.AssetPath) is ModelImporter))
                {
                    missing.Add(
                        "Missing or invalid original FBX at '" +
                        contract.AssetPath + "'. Export it from the tracked Blender " +
                        "fortress source before configuring imports.");
                }
            }

            if (missing.Count > 0)
            {
                throw new InvalidOperationException(BuildErrorMessage(missing));
            }

            int changedCount = 0;
            for (int index = 0; index < RequiredModels.Length; index++)
            {
                if (ConfigureImporter(RequiredModels[index].AssetPath))
                {
                    changedCount++;
                }
            }

            AssetDatabase.SaveAssets();
            return changedCount;
        }

        private static bool ConfigureImporter(string assetPath)
        {
            ModelImporter importer = AssetImporter.GetAtPath(assetPath)
                as ModelImporter;
            if (importer == null)
            {
                throw new InvalidOperationException(
                    "No ModelImporter exists for original fortress FBX '" +
                    assetPath + "'.");
            }

            bool changed = false;
            changed |= SetValue(importer.globalScale, 1f, value => importer.globalScale = value);
            changed |= SetValue(importer.useFileScale, true, value => importer.useFileScale = value);
            changed |= SetValue(
                importer.bakeAxisConversion,
                true,
                value => importer.bakeAxisConversion = value);
            changed |= SetValue(
                importer.animationType,
                ModelImporterAnimationType.None,
                value => importer.animationType = value);
            changed |= SetValue(
                importer.importAnimation,
                false,
                value => importer.importAnimation = value);
            changed |= SetValue(
                importer.importAnimatedCustomProperties,
                false,
                value => importer.importAnimatedCustomProperties = value);
            changed |= SetValue(
                importer.importBlendShapes,
                false,
                value => importer.importBlendShapes = value);
            changed |= SetValue(
                importer.importCameras,
                false,
                value => importer.importCameras = value);
            changed |= SetValue(
                importer.importLights,
                false,
                value => importer.importLights = value);
            changed |= SetValue(
                importer.importVisibility,
                false,
                value => importer.importVisibility = value);
            changed |= SetValue(
                importer.importConstraints,
                false,
                value => importer.importConstraints = value);
            changed |= SetValue(
                importer.materialImportMode,
                ModelImporterMaterialImportMode.None,
                value => importer.materialImportMode = value);
            changed |= SetValue(importer.isReadable, false, value => importer.isReadable = value);
            changed |= SetValue(importer.addCollider, false, value => importer.addCollider = value);
            changed |= SetValue(importer.keepQuads, false, value => importer.keepQuads = value);
            changed |= SetValue(importer.weldVertices, true, value => importer.weldVertices = value);
            changed |= SetValue(
                importer.optimizeMeshPolygons,
                true,
                value => importer.optimizeMeshPolygons = value);
            changed |= SetValue(
                importer.optimizeMeshVertices,
                true,
                value => importer.optimizeMeshVertices = value);
            changed |= SetValue(
                importer.meshCompression,
                ModelImporterMeshCompression.Off,
                value => importer.meshCompression = value);
            changed |= SetValue(
                importer.generateSecondaryUV,
                false,
                value => importer.generateSecondaryUV = value);
            changed |= SetValue(
                importer.importNormals,
                ModelImporterNormals.Import,
                value => importer.importNormals = value);
            changed |= SetValue(
                importer.importTangents,
                ModelImporterTangents.CalculateMikk,
                value => importer.importTangents = value);
            changed |= SetValue(
                importer.indexFormat,
                ModelImporterIndexFormat.Auto,
                value => importer.indexFormat = value);
            changed |= SetValue(
                importer.preserveHierarchy,
                true,
                value => importer.preserveHierarchy = value);
            changed |= SetValue(
                importer.sortHierarchyByName,
                true,
                value => importer.sortHierarchyByName = value);

            if (changed)
            {
                importer.SaveAndReimport();
            }

            return changed;
        }

        private static int ValidateModel(
            FortressSourceModelContract contract,
            List<string> errors)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(
                contract.AssetPath);
            ModelImporter importer = AssetImporter.GetAtPath(contract.AssetPath)
                as ModelImporter;
            if (model == null || importer == null)
            {
                errors.Add(
                    "Missing or invalid original fortress FBX at '" +
                    contract.AssetPath + "'. Export '" + contract.ModelName +
                    "' from SourceArt/Blender/Environment/FortressKit.blend.");
                return -1;
            }

            ValidateImporter(contract, importer, errors);
            if (model.transform.localPosition.sqrMagnitude > TransformTolerance ||
                Quaternion.Angle(
                    model.transform.localRotation,
                    Quaternion.identity) > TransformTolerance ||
                (model.transform.localScale - Vector3.one).sqrMagnitude >
                TransformTolerance)
            {
                errors.Add(
                    "Fortress FBX '" + contract.AssetPath +
                    "' root transform must remain at identity.");
            }

            if (!string.Equals(model.name, contract.ModelName, StringComparison.Ordinal))
            {
                errors.Add(
                    "Fortress FBX '" + contract.AssetPath + "' imported as root '" +
                    model.name + "'; expected '" + contract.ModelName + "'.");
            }

            UnityEngine.Object[] importedAssets = AssetDatabase.LoadAllAssetsAtPath(
                contract.AssetPath);
            for (int index = 0; index < importedAssets.Length; index++)
            {
                if (importedAssets[index] is Material)
                {
                    errors.Add(
                        "Fortress FBX '" + contract.AssetPath +
                        "' imported an embedded Material asset. " +
                        ConfigureInstruction);
                    break;
                }
            }

            if (model.GetComponentsInChildren<Collider>(true).Length != 0 ||
                model.GetComponentsInChildren<Animator>(true).Length != 0 ||
                model.GetComponentsInChildren<Animation>(true).Length != 0 ||
                model.GetComponentsInChildren<Camera>(true).Length != 0 ||
                model.GetComponentsInChildren<Light>(true).Length != 0)
            {
                errors.Add(
                    "Fortress FBX '" + contract.AssetPath +
                    "' must contain static visual meshes only: no Collider, Animator, " +
                    "Animation, Camera, or Light components.");
            }

            if (model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length != 0)
            {
                errors.Add(
                    "Fortress FBX '" + contract.AssetPath +
                    "' contains a skinned renderer; join it into one static mesh in Blender.");
            }

            MeshRenderer[] renderers = model.GetComponentsInChildren<MeshRenderer>(true);
            MeshFilter[] filters = model.GetComponentsInChildren<MeshFilter>(true);
            if (renderers.Length != MaximumRendererCountPerSource ||
                filters.Length != MaximumRendererCountPerSource)
            {
                errors.Add(
                    "Fortress FBX '" + contract.AssetPath + "' has " +
                    renderers.Length + " MeshRenderer(s) and " + filters.Length +
                    "; expected one joined static mesh for the mobile draw budget.");
                return -1;
            }

            Mesh mesh = filters[0].sharedMesh;
            if (mesh == null)
            {
                errors.Add(
                    "Fortress FBX '" + contract.AssetPath + "' has no imported mesh.");
                return -1;
            }

            string expectedMeshName = contract.ModelName + "_Mesh";
            if (!string.Equals(
                    filters[0].gameObject.name,
                    expectedMeshName,
                    StringComparison.Ordinal))
            {
                errors.Add(
                    "Fortress FBX '" + contract.AssetPath + "' mesh object is '" +
                    filters[0].gameObject.name + "'; expected '" +
                    expectedMeshName + "'.");
            }

            if (!mesh.HasVertexAttribute(
                    UnityEngine.Rendering.VertexAttribute.Normal) ||
                !mesh.HasVertexAttribute(
                    UnityEngine.Rendering.VertexAttribute.TexCoord0) ||
                !mesh.HasVertexAttribute(
                    UnityEngine.Rendering.VertexAttribute.Tangent))
            {
                errors.Add(
                    "Fortress mesh '" + contract.ModelName +
                    "' must retain imported normals and UV0 from Blender and " +
                    "calculate MikkTSpace tangents for its shared normal map.");
            }

            if (mesh.vertexCount > MaximumVerticesPerMesh)
            {
                errors.Add(
                    "Fortress mesh '" + contract.ModelName + "' has " +
                    mesh.vertexCount + " vertices; maximum is " +
                    MaximumVerticesPerMesh + ".");
            }

            int submeshCount = mesh.subMeshCount;
            if (submeshCount != contract.ExpectedSubmeshCount ||
                renderers[0].sharedMaterials.Length !=
                contract.ExpectedSubmeshCount ||
                submeshCount > MaximumSubmeshCountPerSource)
            {
                errors.Add(
                    "Fortress mesh '" + contract.ModelName + "' has " +
                    submeshCount + " submesh draw(s) and " +
                    renderers[0].sharedMaterials.Length +
                    " renderer slot(s); expected " +
                    contract.ExpectedSubmeshCount + " and at most " +
                    MaximumSubmeshCountPerSource + ".");
            }

            int triangleCount = CountTriangles(mesh, contract.AssetPath, errors);
            if (triangleCount <= 0 || triangleCount > contract.MaximumTriangles)
            {
                errors.Add(
                    "Fortress mesh '" + contract.ModelName + "' has " +
                    triangleCount + " triangles; expected 1.." +
                    contract.MaximumTriangles + " for the Balanced mobile budget.");
            }

            Bounds bounds = TransformBounds(
                mesh.bounds,
                model.transform.worldToLocalMatrix *
                filters[0].transform.localToWorldMatrix);
            ValidateBounds(contract, bounds, errors);
            return triangleCount;
        }

        private static void ValidateImporter(
            FortressSourceModelContract contract,
            ModelImporter importer,
            List<string> errors)
        {
            bool valid =
                Mathf.Approximately(importer.globalScale, 1f) &&
                importer.useFileScale &&
                importer.bakeAxisConversion &&
                importer.animationType == ModelImporterAnimationType.None &&
                !importer.importAnimation &&
                !importer.importAnimatedCustomProperties &&
                !importer.importBlendShapes &&
                !importer.importCameras &&
                !importer.importLights &&
                !importer.importVisibility &&
                !importer.importConstraints &&
                importer.materialImportMode == ModelImporterMaterialImportMode.None &&
                !importer.isReadable &&
                !importer.addCollider &&
                !importer.keepQuads &&
                importer.weldVertices &&
                importer.optimizeMeshPolygons &&
                importer.optimizeMeshVertices &&
                importer.meshCompression == ModelImporterMeshCompression.Off &&
                !importer.generateSecondaryUV &&
                importer.importNormals == ModelImporterNormals.Import &&
                importer.importTangents == ModelImporterTangents.CalculateMikk &&
                importer.indexFormat == ModelImporterIndexFormat.Auto &&
                importer.preserveHierarchy &&
                importer.sortHierarchyByName;
            if (!valid)
            {
                errors.Add(
                    "Original fortress importer drifted at '" +
                    contract.AssetPath + "'. " + ConfigureInstruction);
            }
        }

        private static int CountTriangles(
            Mesh mesh,
            string assetPath,
            List<string> errors)
        {
            long triangleCount = 0;
            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                if (mesh.GetTopology(submesh) != UnityEngine.MeshTopology.Triangles)
                {
                    errors.Add(
                        "Fortress FBX '" + assetPath + "' submesh " + submesh +
                        " is not triangulated. Triangulate the Blender export.");
                    continue;
                }

                triangleCount += (long)mesh.GetIndexCount(submesh) / 3L;
            }

            return triangleCount > int.MaxValue ? int.MaxValue : (int)triangleCount;
        }

        private static void ValidateBounds(
            FortressSourceModelContract contract,
            Bounds bounds,
            List<string> errors)
        {
            Vector3 size = bounds.size;
            Vector3 minimum = contract.MinimumSize;
            Vector3 maximum = contract.MaximumSize;
            bool validSize =
                size.x >= minimum.x && size.x <= maximum.x &&
                size.y >= minimum.y && size.y <= maximum.y &&
                size.z >= minimum.z && size.z <= maximum.z;
            if (!validSize)
            {
                errors.Add(
                    "Fortress mesh '" + contract.ModelName + "' bounds are " +
                    Format(size) + "; expected Unity XYZ within " +
                    Format(minimum) + ".." + Format(maximum) +
                    ". Check Blender units and -Z forward/+Y up export axes.");
            }

            if (contract.RequiresGroundPivot &&
                Mathf.Abs(bounds.min.y) > GroundPivotTolerance)
            {
                errors.Add(
                    "Fortress mesh '" + contract.ModelName + "' ground is y=" +
                    bounds.min.y.ToString("0.###") +
                    "; keep the exported ground pivot at y=0 ± " +
                    GroundPivotTolerance.ToString("0.##") + ".");
            }

            if (!contract.RequiresGroundPivot &&
                (Mathf.Abs(bounds.min.y - contract.ExpectedMinimumY) >
                    FoundationBoundsTolerance ||
                 Mathf.Abs(bounds.max.y - contract.ExpectedMaximumY) >
                    FoundationBoundsTolerance))
            {
                errors.Add(
                    "Fortress mesh '" + contract.ModelName +
                    "' vertical bounds are " +
                    bounds.min.y.ToString("0.###") + ".." +
                    bounds.max.y.ToString("0.###") + "; expected " +
                    contract.ExpectedMinimumY.ToString("0.###") + ".." +
                    contract.ExpectedMaximumY.ToString("0.###") +
                    " so the visual foundation remains recessed below gameplay.");
            }
        }

        private static void ValidateManifest(
            IReadOnlyList<int> importedTriangleCounts,
            List<string> errors)
        {
            TextAsset manifestText = AssetDatabase.LoadAssetAtPath<TextAsset>(
                ManifestAssetPath);
            if (manifestText == null)
            {
                errors.Add(
                    "Missing original fortress manifest at '" +
                    ManifestAssetPath + "'. Regenerate the Blender source art.");
                return;
            }

            FortressManifest manifest;
            try
            {
                manifest = JsonUtility.FromJson<FortressManifest>(
                    manifestText.text);
            }
            catch (ArgumentException exception)
            {
                errors.Add(
                    "Original fortress manifest is invalid JSON: " +
                    exception.Message);
                return;
            }

            if (manifest == null || manifest.assets == null)
            {
                errors.Add("Original fortress manifest is incomplete.");
                return;
            }

            if (manifest.schema != 1 ||
                !manifest.original_project_asset ||
                manifest.external_mesh_or_texture_sources == null ||
                manifest.external_mesh_or_texture_sources.Length != 0 ||
                !SameStrings(
                    manifest.material_contract,
                    StoneAndEmberMaterialSlots))
            {
                errors.Add(
                    "Original fortress manifest must retain schema 1, original " +
                    "project provenance, no external mesh or texture sources, " +
                    "and the ordered Stone/Ember material contract.");
            }

            var seen = new bool[RequiredModels.Length];
            for (int index = 0; index < manifest.assets.Length; index++)
            {
                FortressManifestAsset asset = manifest.assets[index];
                if (asset == null ||
                    !TryGetContract(asset.asset, out FortressSourceModelContract contract))
                {
                    errors.Add(
                        "Original fortress manifest contains an unknown or empty " +
                        "asset at index " + index + ".");
                    continue;
                }

                int contractIndex = (int)contract.Id;
                if (seen[contractIndex])
                {
                    errors.Add(
                        "Original fortress manifest repeats asset '" +
                        contract.ModelName + "'.");
                    continue;
                }

                seen[contractIndex] = true;
                IReadOnlyList<string> expectedMaterials =
                    GetExpectedMaterialSlots(contract.Id);
                bool modelContractValid =
                    string.Equals(
                        asset.root_object,
                        contract.ModelName,
                        StringComparison.Ordinal) &&
                    string.Equals(
                        asset.mesh_object,
                        contract.ModelName + "_Mesh",
                        StringComparison.Ordinal) &&
                    string.Equals(
                        asset.fbx,
                        contract.AssetPath,
                        StringComparison.Ordinal) &&
                    asset.submeshes == contract.ExpectedSubmeshCount &&
                    asset.triangles > 0 &&
                    asset.triangles <= contract.MaximumTriangles &&
                    asset.vertices > 0 &&
                    asset.vertices <= MaximumVerticesPerMesh &&
                    asset.visual_only &&
                    SameStrings(asset.material_slots, expectedMaterials) &&
                    SameStrings(asset.uv_layers, new[] { "UV0" });
                if (!modelContractValid)
                {
                    errors.Add(
                        "Original fortress manifest asset '" +
                        contract.ModelName +
                        "' drifted from its hierarchy, material, UV, or mobile " +
                        "budget contract.");
                }

                if (contractIndex < importedTriangleCounts.Count &&
                    importedTriangleCounts[contractIndex] >= 0 &&
                    asset.triangles != importedTriangleCounts[contractIndex])
                {
                    errors.Add(
                        "Original fortress manifest reports " +
                        asset.triangles + " triangles for '" +
                        contract.ModelName + "' but Unity imported " +
                        importedTriangleCounts[contractIndex] +
                        ". Regenerate the FBX and manifest together.");
                }

                if (contract.Id == FortressSourceModelId.FoundationRing)
                {
                    ValidateFoundationManifestAsset(asset, errors);
                }
            }

            for (int index = 0; index < seen.Length; index++)
            {
                if (!seen[index])
                {
                    errors.Add(
                        "Original fortress manifest is missing required asset '" +
                        RequiredModels[index].ModelName + "'.");
                }
            }

            ValidateFoundationManifestSummary(
                manifest.foundation_ring,
                importedTriangleCounts[(int)FortressSourceModelId.FoundationRing],
                errors);
        }

        private static void ValidateFoundationManifestAsset(
            FortressManifestAsset asset,
            List<string> errors)
        {
            FortressFoundationDesignContract design =
                asset.foundation_ring_design_contract;
            bool radialContractValid =
                asset.minimum_planar_vertex_radius >=
                    FoundationMinimumPlanarRadius &&
                asset.maximum_planar_vertex_radius <=
                    FoundationMaximumPlanarRadius;
            bool boundsContractValid =
                SameFloats(
                    asset.bounds_min_blender_xyz,
                    new[] { -25.4039f, -25.27f, FoundationMinimumY },
                    FoundationBoundsTolerance) &&
                SameFloats(
                    asset.bounds_max_blender_xyz,
                    new[] { 25.4039f, 25.27f, FoundationMaximumY },
                    FoundationBoundsTolerance);
            bool designContractValid =
                design != null &&
                Mathf.Abs(
                    design.inner_radius_minimum_meters -
                    FoundationMinimumPlanarRadius) <=
                    FoundationBoundsTolerance &&
                Mathf.Abs(
                    design.outer_radius_maximum_meters -
                    FoundationMaximumPlanarRadius) <=
                    FoundationBoundsTolerance &&
                Mathf.Abs(design.top_height_meters - FoundationMaximumY) <=
                    FoundationBoundsTolerance &&
                Mathf.Abs(design.bottom_height_meters - FoundationMinimumY) <=
                    FoundationBoundsTolerance &&
                design.flat_reachable_ledges == 0 &&
                design.joined_static_meshes == 1 &&
                design.mobile_triangle_target == 8000;
            if (!radialContractValid ||
                !boundsContractValid ||
                !designContractValid)
            {
                errors.Add(
                    "Fortress foundation manifest must keep every vertex at " +
                    "radius 21.25..25.5 m, vertical bounds -1.8..-0.45 m, " +
                    "one joined visual mesh, no reachable ledges, and at most " +
                    "8,000 triangles.");
            }
        }

        private static void ValidateFoundationManifestSummary(
            FortressFoundationSummary summary,
            int importedTriangleCount,
            List<string> errors)
        {
            bool valid =
                summary != null &&
                string.Equals(
                    summary.asset,
                    "FortressFoundationRing",
                    StringComparison.Ordinal) &&
                summary.inner_radius_meters >=
                    FoundationMinimumPlanarRadius &&
                summary.outer_radius_meters <=
                    FoundationMaximumPlanarRadius &&
                Mathf.Abs(summary.top_height_meters - FoundationMaximumY) <=
                    FoundationBoundsTolerance &&
                Mathf.Abs(summary.bottom_height_meters - FoundationMinimumY) <=
                    FoundationBoundsTolerance &&
                summary.renderers == 1 &&
                summary.submesh_draws == 1 &&
                summary.triangles > 0 &&
                summary.triangles <= 8000 &&
                (importedTriangleCount < 0 ||
                 summary.triangles == importedTriangleCount) &&
                !summary.authoritative_gameplay_geometry;
            if (!valid)
            {
                errors.Add(
                    "Fortress foundation manifest summary must describe one " +
                    "visual-only renderer/draw within the recessed radial and " +
                    "mobile triangle contracts.");
            }
        }

        private static bool TryGetContract(
            string modelName,
            out FortressSourceModelContract contract)
        {
            for (int index = 0; index < RequiredModels.Length; index++)
            {
                if (string.Equals(
                        RequiredModels[index].ModelName,
                        modelName,
                        StringComparison.Ordinal))
                {
                    contract = RequiredModels[index];
                    return true;
                }
            }

            contract = default;
            return false;
        }

        private static IReadOnlyList<string> GetExpectedMaterialSlots(
            FortressSourceModelId id)
        {
            switch (id)
            {
                case FortressSourceModelId.WallStraight:
                case FortressSourceModelId.WallBroken:
                case FortressSourceModelId.FoundationRing:
                    return StoneMaterialSlots;

                case FortressSourceModelId.Tower:
                case FortressSourceModelId.Brazier:
                case FortressSourceModelId.Wardstone:
                    return StoneAndEmberMaterialSlots;

                default:
                    throw new ArgumentOutOfRangeException(nameof(id), id, null);
            }
        }

        private static bool SameStrings(
            IReadOnlyList<string> first,
            IReadOnlyList<string> second)
        {
            if (first == null || second == null || first.Count != second.Count)
            {
                return false;
            }

            for (int index = 0; index < first.Count; index++)
            {
                if (!string.Equals(
                        first[index],
                        second[index],
                        StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool SameFloats(
            IReadOnlyList<float> first,
            IReadOnlyList<float> second,
            float tolerance)
        {
            if (first == null || second == null || first.Count != second.Count)
            {
                return false;
            }

            for (int index = 0; index < first.Count; index++)
            {
                if (Mathf.Abs(first[index] - second[index]) > tolerance)
                {
                    return false;
                }
            }

            return true;
        }

        private static Bounds TransformBounds(Bounds bounds, Matrix4x4 matrix)
        {
            Vector3 minimum = bounds.min;
            Vector3 maximum = bounds.max;
            Vector3 first = matrix.MultiplyPoint3x4(minimum);
            Bounds transformed = new Bounds(first, Vector3.zero);
            for (int x = 0; x <= 1; x++)
            {
                for (int y = 0; y <= 1; y++)
                {
                    for (int z = 0; z <= 1; z++)
                    {
                        transformed.Encapsulate(matrix.MultiplyPoint3x4(new Vector3(
                            x == 0 ? minimum.x : maximum.x,
                            y == 0 ? minimum.y : maximum.y,
                            z == 0 ? minimum.z : maximum.z)));
                    }
                }
            }

            return transformed;
        }

        private static bool SetValue<T>(
            T current,
            T expected,
            Action<T> apply)
        {
            if (EqualityComparer<T>.Default.Equals(current, expected))
            {
                return false;
            }

            apply(expected);
            return true;
        }

        private static string BuildErrorMessage(IReadOnlyList<string> errors)
        {
            var builder = new StringBuilder(
                "Original fortress source-art validation failed:");
            for (int index = 0; index < errors.Count; index++)
            {
                builder.Append("\n- ");
                builder.Append(errors[index]);
            }

            return builder.ToString();
        }

        private static string Format(Vector3 value)
        {
            return "(" + value.x.ToString("0.###") + ", " +
                   value.y.ToString("0.###") + ", " +
                   value.z.ToString("0.###") + ")";
        }

        private static void ShowDialog(string title, string message)
        {
            if (!UnityEngine.Application.isBatchMode)
            {
                EditorUtility.DisplayDialog(title, message, "OK");
            }
        }

        [Serializable]
        private sealed class FortressManifest
        {
            public int schema;
            public bool original_project_asset;
            public string[] external_mesh_or_texture_sources;
            public string[] material_contract;
            public FortressManifestAsset[] assets;
            public FortressFoundationSummary foundation_ring;
        }

        [Serializable]
        private sealed class FortressManifestAsset
        {
            public string asset;
            public string root_object;
            public string mesh_object;
            public int triangles;
            public int vertices;
            public int submeshes;
            public string[] material_slots;
            public string[] uv_layers;
            public float[] bounds_min_blender_xyz;
            public float[] bounds_max_blender_xyz;
            public float minimum_planar_vertex_radius;
            public float maximum_planar_vertex_radius;
            public bool visual_only;
            public string fbx;
            public FortressFoundationDesignContract foundation_ring_design_contract;
        }

        [Serializable]
        private sealed class FortressFoundationDesignContract
        {
            public float inner_radius_minimum_meters;
            public float outer_radius_maximum_meters;
            public float top_height_meters;
            public float bottom_height_meters;
            public int flat_reachable_ledges;
            public int joined_static_meshes;
            public int mobile_triangle_target;
        }

        [Serializable]
        private sealed class FortressFoundationSummary
        {
            public string asset;
            public float inner_radius_meters;
            public float outer_radius_meters;
            public float top_height_meters;
            public float bottom_height_meters;
            public int renderers;
            public int submesh_draws;
            public int triangles;
            public bool authoritative_gameplay_geometry;
        }
    }
}
