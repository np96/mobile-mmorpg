using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace SessionRpg.Editor.Assets
{
    public static class ArenaFloorSourceArtValidator
    {
        public const string SourceAssetPath =
            "Assets/SessionRpg/SourceArt/Environment/ArenaFloorVisual.fbx";

        public const string ManifestAssetPath =
            "Assets/SessionRpg/SourceArt/Environment/ArenaFloorVisual.manifest.json";

        public const string SourceModelName = "ArenaFloorVisual";
        public const string SourceMeshObjectName = "ArenaFloorVisual_Mesh";
        public const int ExpectedSubmeshCount = 3;
        public const int MaximumTriangles = 28000;
        public const int MaximumVertices = 65535;

        public const string ConfigureMenuPath =
            "Tools/Session RPG/Assets/Configure Original Arena Floor Import";

        public const string ValidateMenuPath =
            "Tools/Session RPG/Assets/Validate Original Arena Floor Source Art";

        private const string ConfigureInstruction =
            "Run Tools > Session RPG > Assets > Configure Original Arena Floor Import once, then rebuild the playable foundation.";

        private const float BoundsTolerance = 0.015f;
        private const float TransformTolerance = 0.0001f;

        private static readonly string[] ExpectedMaterialSlots =
        {
            "ArenaFloor_Stone",
            "ArenaFloor_Trim",
            "ArenaFloor_Grout",
        };

        private static readonly Vector3 ExpectedBoundsMinimum =
            new Vector3(-15f, -0.12f, -15f);

        private static readonly Vector3 ExpectedBoundsMaximum =
            new Vector3(15f, 0f, 15f);

        public static string GetExpectedMaterialSlotName(int index)
        {
            if (index < 0 || index >= ExpectedMaterialSlots.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return ExpectedMaterialSlots[index];
        }

        [MenuItem(ValidateMenuPath, priority = 2082)]
        public static void ValidateFromMenu()
        {
            try
            {
                ValidateOrThrow();
                const string message =
                    "Original arena-floor source model and importer settings are valid.";
                Debug.Log(message);
                ShowDialog("Arena-floor source validation", message);
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogError(exception.Message);
                ShowDialog(
                    "Arena-floor source validation failed",
                    exception.Message);
            }
        }

        [MenuItem(ConfigureMenuPath, priority = 2083)]
        public static void ConfigureFromMenu()
        {
            try
            {
                bool changed = ConfigureImport();
                ValidateOrThrow();
                string message = changed
                    ? "Configured and reimported the original arena-floor model."
                    : "Original arena-floor import settings were already correct.";
                Debug.Log(message);
                ShowDialog("Arena-floor source import", message);
            }
            catch (InvalidOperationException exception)
            {
                Debug.LogError(exception.Message);
                ShowDialog("Arena-floor source import failed", exception.Message);
            }
        }

        public static void ValidateForCommandLine()
        {
            ValidateOrThrow();
            Debug.Log("Original arena-floor source-art validation passed.");
        }

        public static void ConfigureForCommandLine()
        {
            bool changed = ConfigureImport();
            ValidateOrThrow();
            Debug.Log(
                "Original arena-floor import configuration passed; changed " +
                (changed ? "1" : "0") + " asset(s).");
        }

        public static GameObject LoadValidated()
        {
            ValidateOrThrow();
            return AssetDatabase.LoadAssetAtPath<GameObject>(SourceAssetPath);
        }

        public static void ValidateOrThrow()
        {
            var errors = new List<string>();
            int triangleCount = ValidateImportedModel(errors);
            ValidateManifest(triangleCount, errors);
            if (errors.Count > 0)
            {
                throw new InvalidOperationException(BuildErrorMessage(errors));
            }
        }

        public static bool ConfigureImport()
        {
            ModelImporter importer = AssetImporter.GetAtPath(SourceAssetPath)
                as ModelImporter;
            if (importer == null)
            {
                throw new InvalidOperationException(
                    "Missing or invalid original arena-floor FBX at '" +
                    SourceAssetPath + "'. Generate it from " +
                    "SourceArt/Blender/Environment/ArenaFloor.blend before " +
                    "configuring imports.");
            }

            bool changed = false;
            changed |= SetValue(
                importer.globalScale,
                1f,
                value => importer.globalScale = value);
            changed |= SetValue(
                importer.useFileScale,
                true,
                value => importer.useFileScale = value);
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
            changed |= SetValue(
                importer.isReadable,
                false,
                value => importer.isReadable = value);
            changed |= SetValue(
                importer.addCollider,
                false,
                value => importer.addCollider = value);
            changed |= SetValue(
                importer.keepQuads,
                false,
                value => importer.keepQuads = value);
            changed |= SetValue(
                importer.weldVertices,
                true,
                value => importer.weldVertices = value);
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

            AssetDatabase.SaveAssets();
            return changed;
        }

        private static int ValidateImportedModel(List<string> errors)
        {
            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(
                SourceAssetPath);
            ModelImporter importer = AssetImporter.GetAtPath(SourceAssetPath)
                as ModelImporter;
            if (model == null || importer == null)
            {
                errors.Add(
                    "Missing or invalid original arena-floor FBX at '" +
                    SourceAssetPath + "'. Generate it from " +
                    "SourceArt/Blender/Environment/ArenaFloor.blend.");
                return -1;
            }

            ValidateImporter(importer, errors);
            if (!string.Equals(
                    model.name,
                    SourceModelName,
                    StringComparison.Ordinal))
            {
                errors.Add(
                    "Arena-floor FBX imported as root '" + model.name +
                    "'; expected '" + SourceModelName + "'.");
            }

            if (model.transform.localPosition.sqrMagnitude > TransformTolerance ||
                Quaternion.Angle(
                    model.transform.localRotation,
                    Quaternion.identity) > TransformTolerance ||
                (model.transform.localScale - Vector3.one).sqrMagnitude >
                TransformTolerance)
            {
                errors.Add(
                    "Arena-floor FBX root transform must remain identity so the " +
                    "ground-center pivot is authoritative.");
            }

            UnityEngine.Object[] importedAssets = AssetDatabase.LoadAllAssetsAtPath(
                SourceAssetPath);
            for (int index = 0; index < importedAssets.Length; index++)
            {
                if (importedAssets[index] is Material)
                {
                    errors.Add(
                        "Arena-floor FBX imported an embedded Material asset. " +
                        ConfigureInstruction);
                    break;
                }
            }

            if (model.GetComponentsInChildren<Collider>(true).Length != 0 ||
                model.GetComponentsInChildren<Animator>(true).Length != 0 ||
                model.GetComponentsInChildren<Animation>(true).Length != 0 ||
                model.GetComponentsInChildren<Camera>(true).Length != 0 ||
                model.GetComponentsInChildren<Light>(true).Length != 0 ||
                model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length != 0)
            {
                errors.Add(
                    "Arena-floor FBX must contain only one static visual mesh: no " +
                    "Collider, Animator, Animation, Camera, Light, or skinned renderer.");
            }

            MeshRenderer[] renderers =
                model.GetComponentsInChildren<MeshRenderer>(true);
            MeshFilter[] filters = model.GetComponentsInChildren<MeshFilter>(true);
            if (renderers.Length != 1 || filters.Length != 1)
            {
                errors.Add(
                    "Arena-floor FBX has " + renderers.Length +
                    " MeshRenderer(s) and " + filters.Length +
                    " MeshFilter(s); expected one joined mobile visual.");
                return -1;
            }

            MeshFilter filter = filters[0];
            Mesh mesh = filter.sharedMesh;
            if (mesh == null)
            {
                errors.Add("Arena-floor FBX has no imported static mesh.");
                return -1;
            }

            if (!string.Equals(
                    filter.gameObject.name,
                    SourceMeshObjectName,
                    StringComparison.Ordinal))
            {
                errors.Add(
                    "Arena-floor mesh object is '" + filter.gameObject.name +
                    "'; expected '" + SourceMeshObjectName + "'.");
            }

            if (!mesh.HasVertexAttribute(
                    UnityEngine.Rendering.VertexAttribute.Normal) ||
                !mesh.HasVertexAttribute(
                    UnityEngine.Rendering.VertexAttribute.Tangent) ||
                !mesh.HasVertexAttribute(
                    UnityEngine.Rendering.VertexAttribute.TexCoord0))
            {
                errors.Add(
                    "Arena-floor mesh must retain normals and UV0 and calculate " +
                    "MikkTSpace tangents for the URP normal map.");
            }

            if (mesh.vertexCount > MaximumVertices)
            {
                errors.Add(
                    "Arena-floor mesh has " + mesh.vertexCount +
                    " vertices; maximum is " + MaximumVertices + ".");
            }

            if (mesh.subMeshCount != ExpectedSubmeshCount ||
                renderers[0].sharedMaterials.Length != ExpectedSubmeshCount)
            {
                errors.Add(
                    "Arena-floor mesh has " + mesh.subMeshCount +
                    " submeshes and " + renderers[0].sharedMaterials.Length +
                    " renderer slots; expected exactly three ordered slots: " +
                    string.Join(", ", ExpectedMaterialSlots) + ".");
            }

            int triangleCount = CountTriangles(mesh, errors);
            if (triangleCount <= 0 || triangleCount > MaximumTriangles)
            {
                errors.Add(
                    "Arena-floor mesh has " + triangleCount +
                    " triangles; expected 1.." + MaximumTriangles +
                    " for the Balanced mobile budget.");
            }

            Bounds bounds = TransformBounds(
                mesh.bounds,
                model.transform.worldToLocalMatrix *
                filter.transform.localToWorldMatrix);
            ValidateBounds(bounds, errors);
            return triangleCount;
        }

        private static void ValidateImporter(
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
                    "Original arena-floor importer drifted at '" +
                    SourceAssetPath + "'. " + ConfigureInstruction);
            }
        }

        private static int CountTriangles(Mesh mesh, List<string> errors)
        {
            long triangleCount = 0;
            for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                if (mesh.GetTopology(submesh) != UnityEngine.MeshTopology.Triangles)
                {
                    errors.Add(
                        "Arena-floor submesh " + submesh +
                        " is not triangulated. Regenerate the Blender export.");
                    continue;
                }

                triangleCount += (long)mesh.GetIndexCount(submesh) / 3L;
            }

            return triangleCount > int.MaxValue
                ? int.MaxValue
                : (int)triangleCount;
        }

        private static void ValidateBounds(Bounds bounds, List<string> errors)
        {
            Vector3 minimum = bounds.min;
            Vector3 maximum = bounds.max;
            if (!Approximately(minimum, ExpectedBoundsMinimum) ||
                !Approximately(maximum, ExpectedBoundsMaximum))
            {
                errors.Add(
                    "Arena-floor bounds are " + Format(minimum) + ".." +
                    Format(maximum) + "; expected " +
                    Format(ExpectedBoundsMinimum) + ".." +
                    Format(ExpectedBoundsMaximum) +
                    " in Unity XYZ. Preserve the 15 m radius, 0 m top, " +
                    "12 cm recessed relief, and ground-center pivot.");
            }
        }

        private static void ValidateManifest(
            int importedTriangleCount,
            List<string> errors)
        {
            TextAsset manifestText = AssetDatabase.LoadAssetAtPath<TextAsset>(
                ManifestAssetPath);
            if (manifestText == null)
            {
                errors.Add(
                    "Missing arena-floor source manifest at '" +
                    ManifestAssetPath + "'. Regenerate the Blender source art.");
                return;
            }

            ArenaFloorManifest manifest;
            try
            {
                manifest = JsonUtility.FromJson<ArenaFloorManifest>(
                    manifestText.text);
            }
            catch (ArgumentException exception)
            {
                errors.Add(
                    "Arena-floor source manifest is invalid JSON: " +
                    exception.Message);
                return;
            }

            if (manifest == null || manifest.asset == null ||
                manifest.design_contract == null)
            {
                errors.Add("Arena-floor source manifest is incomplete.");
                return;
            }

            bool provenanceValid =
                manifest.schema == 1 &&
                manifest.original_project_asset &&
                manifest.external_mesh_or_texture_sources != null &&
                manifest.external_mesh_or_texture_sources.Length == 0;
            if (!provenanceValid)
            {
                errors.Add(
                    "Arena-floor manifest must identify schema 1 original project " +
                    "art with no external mesh or texture sources.");
            }

            if (!SameStrings(
                    manifest.material_contract,
                    ExpectedMaterialSlots) ||
                !SameStrings(
                    manifest.asset.material_slots,
                    ExpectedMaterialSlots))
            {
                errors.Add(
                    "Arena-floor manifest material order must be exactly: " +
                    string.Join(", ", ExpectedMaterialSlots) + ".");
            }

            bool modelContractValid =
                string.Equals(
                    manifest.asset.asset,
                    SourceModelName,
                    StringComparison.Ordinal) &&
                string.Equals(
                    manifest.asset.root_object,
                    SourceModelName,
                    StringComparison.Ordinal) &&
                string.Equals(
                    manifest.asset.mesh_object,
                    SourceMeshObjectName,
                    StringComparison.Ordinal) &&
                string.Equals(
                    manifest.asset.fbx,
                    SourceAssetPath,
                    StringComparison.Ordinal) &&
                manifest.asset.submeshes == ExpectedSubmeshCount &&
                manifest.asset.vertices > 0 &&
                manifest.asset.vertices <= MaximumVertices &&
                manifest.asset.triangles > 0 &&
                manifest.asset.triangles <= MaximumTriangles &&
                manifest.asset.visual_only;
            if (!modelContractValid)
            {
                errors.Add(
                    "Arena-floor manifest model, hierarchy, budget, or visual-only " +
                    "contract does not match the Unity source contract.");
            }

            if (importedTriangleCount >= 0 &&
                manifest.asset.triangles != importedTriangleCount)
            {
                errors.Add(
                    "Arena-floor manifest reports " +
                    manifest.asset.triangles + " triangles but Unity imported " +
                    importedTriangleCount + ". Regenerate source and manifest together.");
            }

            bool geometryContractValid =
                Mathf.Abs(manifest.design_contract.radius_meters - 15f) <=
                BoundsTolerance &&
                Mathf.Abs(manifest.design_contract.visual_top_meters) <=
                BoundsTolerance &&
                Mathf.Abs(
                    manifest.design_contract.maximum_vertical_relief_meters -
                    0.12f) <= BoundsTolerance &&
                !manifest.design_contract.authoritative_collision &&
                !manifest.design_contract.raised_playable_ledges &&
                !manifest.design_contract.emissive_or_debug_rings;
            if (!geometryContractValid)
            {
                errors.Add(
                    "Arena-floor manifest drifted from its 15 m visual-only flat " +
                    "combat-surface contract.");
            }
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

        private static bool Approximately(Vector3 first, Vector3 second)
        {
            return Mathf.Abs(first.x - second.x) <= BoundsTolerance &&
                   Mathf.Abs(first.y - second.y) <= BoundsTolerance &&
                   Mathf.Abs(first.z - second.z) <= BoundsTolerance;
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
                "Original arena-floor source-art validation failed:");
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
        private sealed class ArenaFloorManifest
        {
            public int schema;
            public bool original_project_asset;
            public string[] external_mesh_or_texture_sources;
            public string[] material_contract;
            public ArenaFloorManifestDesign design_contract;
            public ArenaFloorManifestAsset asset;
        }

        [Serializable]
        private sealed class ArenaFloorManifestDesign
        {
            public float radius_meters;
            public float visual_top_meters;
            public float maximum_vertical_relief_meters;
            public bool authoritative_collision;
            public bool raised_playable_ledges;
            public bool emissive_or_debug_rings;
        }

        [Serializable]
        private sealed class ArenaFloorManifestAsset
        {
            public string asset;
            public string root_object;
            public string mesh_object;
            public int triangles;
            public int vertices;
            public int submeshes;
            public string[] material_slots;
            public bool visual_only;
            public string fbx;
        }
    }
}
