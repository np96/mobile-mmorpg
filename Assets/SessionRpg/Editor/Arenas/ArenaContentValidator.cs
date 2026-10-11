using System;
using System.Collections.Generic;
using SessionRpg.Unity.Arenas;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SessionRpg.Editor.Arenas
{
    public static class ArenaContentValidator
    {
        public const int WorldObstacleLayer = 8;
        public const int CombatantLayer = 9;
        public const int GroundSurfaceLayer = 10;
        public const float MaximumGroundTiltDegrees = 0.25f;
        public const float GroundHeightTolerance = 0.025f;

        public const int AuthoritativeGameplayLayerMask =
            (1 << WorldObstacleLayer) |
            (1 << CombatantLayer) |
            (1 << GroundSurfaceLayer);

        public static ArenaContentValidationReport ValidatePresentation(
            ArenaPresentation presentation)
        {
            var report = new ArenaContentValidationReport();
            if (presentation == null)
            {
                report.Add(
                    ArenaContentValidationCode.NullPresentation,
                    "ArenaPresentation is required.");
                return report;
            }

            ValidatePresentationRoot(presentation, report);
            ValidateIdentity(presentation, report);
            ValidateCompositionRoots(presentation, report);
            ValidateFlatGround(presentation, report);
            ValidateVisualLayers(presentation, report);
            ValidateGameplayProxies(presentation, report);
            ValidateCaptureAnchors(presentation, report);
            return report;
        }

        public static ArenaContentValidationReport ValidateCatalog(
            ArenaCatalogAsset catalog)
        {
            var report = new ArenaContentValidationReport();
            if (catalog == null)
            {
                report.Add(
                    ArenaContentValidationCode.NullCatalog,
                    "ArenaCatalogAsset is required.");
                return report;
            }

            if (catalog.Count == 0)
            {
                report.Add(
                    ArenaContentValidationCode.EmptyCatalog,
                    "Arena catalog must contain at least one presentation entry.",
                    catalog);
                return report;
            }

            var ids = new HashSet<ArenaContentId>();
            var entriesPerMode = new Dictionary<ArenaContentMode, int>();
            var defaultsPerMode = new Dictionary<ArenaContentMode, int>();
            for (int index = 0; index < catalog.Count; index++)
            {
                ArenaCatalogEntry entry = catalog.GetEntry(index);
                if (entry == null)
                {
                    report.Add(
                        ArenaContentValidationCode.NullCatalogEntry,
                        "Arena catalog entry " + index + " is null.",
                        catalog);
                    continue;
                }

                if (!entry.ContentId.IsValid)
                {
                    report.Add(
                        ArenaContentValidationCode.InvalidContentId,
                        "Arena catalog entry " + index + " has invalid stable ID '" +
                        entry.ContentId + "'.",
                        catalog);
                }
                else if (!ids.Add(entry.ContentId))
                {
                    report.Add(
                        ArenaContentValidationCode.DuplicateCatalogId,
                        "Arena catalog contains stable ID '" + entry.ContentId +
                        "' more than once.",
                        catalog);
                }

                if (entry.Mode == ArenaContentMode.None ||
                    !Enum.IsDefined(typeof(ArenaContentMode), entry.Mode))
                {
                    report.Add(
                        ArenaContentValidationCode.InvalidMode,
                        "Arena catalog entry '" + entry.ContentId + "' has no valid mode.",
                        catalog);
                }
                else
                {
                    Increment(entriesPerMode, entry.Mode);
                    if (entry.IsDefaultForMode)
                    {
                        Increment(defaultsPerMode, entry.Mode);
                    }
                }

                bool hasPrefab = entry.HasPrefab;
                bool hasScene = entry.HasScene;
                if (hasPrefab && hasScene)
                {
                    report.Add(
                        ArenaContentValidationCode.AmbiguousCatalogSource,
                        "Arena '" + entry.ContentId +
                        "' must use either a prefab or an additive scene, not both.",
                        catalog);
                    continue;
                }

                if (!hasPrefab && !hasScene)
                {
                    report.Add(
                        ArenaContentValidationCode.MissingCatalogSource,
                        "Arena '" + entry.ContentId +
                        "' requires a presentation prefab or additive scene path.",
                        catalog);
                    continue;
                }

                if (hasPrefab)
                {
                    LoadAndValidatePrefab(entry, report);
                }
                else
                {
                    LoadAndValidateScene(entry, report);
                }
            }

            foreach (KeyValuePair<ArenaContentMode, int> pair in entriesPerMode)
            {
                defaultsPerMode.TryGetValue(pair.Key, out int defaultCount);
                if (defaultCount == 0)
                {
                    report.Add(
                        ArenaContentValidationCode.MissingModeDefault,
                        "Arena catalog has no default presentation for " + pair.Key + ".",
                        catalog);
                }
                else if (defaultCount > 1)
                {
                    report.Add(
                        ArenaContentValidationCode.DuplicateModeDefault,
                        "Arena catalog has " + defaultCount +
                        " default presentations for " + pair.Key + ".",
                        catalog);
                }
            }

            return report;
        }

        public static void ValidatePresentationOrThrow(ArenaPresentation presentation)
        {
            ThrowIfInvalid(ValidatePresentation(presentation));
        }

        public static void ValidateCatalogOrThrow(ArenaCatalogAsset catalog)
        {
            ThrowIfInvalid(ValidateCatalog(catalog));
        }

        private static void ValidatePresentationRoot(
            ArenaPresentation presentation,
            ArenaContentValidationReport report)
        {
            if (presentation.transform.parent != null)
            {
                report.Add(
                    ArenaContentValidationCode.PresentationIsNotRoot,
                    "ArenaPresentation '" + presentation.name +
                    "' must be attached to the presentation root GameObject.",
                    presentation);
            }
        }

        private static void ValidateIdentity(
            ArenaPresentation presentation,
            ArenaContentValidationReport report)
        {
            if (!presentation.ContentId.IsValid)
            {
                report.Add(
                    ArenaContentValidationCode.InvalidContentId,
                    "ArenaPresentation '" + presentation.name +
                    "' has invalid stable ID '" + presentation.ContentId +
                    "'. Use lowercase ASCII letters, numbers and underscores.",
                    presentation);
            }

            if (presentation.Mode == ArenaContentMode.None ||
                !Enum.IsDefined(typeof(ArenaContentMode), presentation.Mode))
            {
                report.Add(
                    ArenaContentValidationCode.InvalidMode,
                    "ArenaPresentation '" + presentation.name + "' has no valid mode.",
                    presentation);
            }

            if (presentation.PresentationVersion < 1)
            {
                report.Add(
                    ArenaContentValidationCode.InvalidPresentationVersion,
                    "ArenaPresentation version must be at least 1.",
                    presentation);
            }
        }

        private static void ValidateCompositionRoots(
            ArenaPresentation presentation,
            ArenaContentValidationReport report)
        {
            ValidateRequiredRoot(
                presentation,
                presentation.VisualRoot,
                ArenaContentValidationCode.MissingVisualRoot,
                "visual",
                report);
            ValidateRequiredRoot(
                presentation,
                presentation.GameplayProxyRoot,
                ArenaContentValidationCode.MissingGameplayProxyRoot,
                "gameplay proxy",
                report);
            ValidateRequiredRoot(
                presentation,
                presentation.GroundRoot,
                ArenaContentValidationCode.MissingGroundRoot,
                "flat ground",
                report);

            if (presentation.VisualRoot != null &&
                presentation.GameplayProxyRoot != null &&
                (IsSameOrChild(
                     presentation.VisualRoot,
                     presentation.GameplayProxyRoot) ||
                 IsSameOrChild(
                     presentation.GameplayProxyRoot,
                     presentation.VisualRoot)))
            {
                report.Add(
                    ArenaContentValidationCode.OverlappingVisualAndProxyRoots,
                    "VisualRoot and GameplayProxyRoot must be separate hierarchies so art " +
                    "cannot silently become authoritative cover.",
                    presentation);
            }
        }

        private static void ValidateFlatGround(
            ArenaPresentation presentation,
            ArenaContentValidationReport report)
        {
            if (!presentation.HasCapability(
                    ArenaPresentationCapabilities.FlatCombatGround))
            {
                report.Add(
                    ArenaContentValidationCode.MissingFlatGroundCapability,
                    "Current combat is planar. ArenaPresentation must declare " +
                    "FlatCombatGround until height is authoritative in Core.",
                    presentation);
            }

            Transform groundRoot = presentation.GroundRoot;
            if (groundRoot == null)
            {
                return;
            }

            Collider[] colliders = groundRoot.GetComponentsInChildren<Collider>(true);
            if (colliders.Length == 0)
            {
                report.Add(
                    ArenaContentValidationCode.MissingGroundCollider,
                    "GroundRoot must contain at least one non-trigger collider.",
                    groundRoot);
                return;
            }

            int validGroundColliderCount = 0;
            for (int index = 0; index < colliders.Length; index++)
            {
                Collider collider = colliders[index];
                if (collider == null || !collider.enabled)
                {
                    continue;
                }

                if (collider.isTrigger)
                {
                    report.Add(
                        ArenaContentValidationCode.TriggerGroundCollider,
                        "Ground collider '" + collider.name + "' cannot be a trigger.",
                        collider);
                }

                if (collider.gameObject.layer != GroundSurfaceLayer)
                {
                    report.Add(
                        ArenaContentValidationCode.GroundColliderLayerMismatch,
                        "Ground collider '" + collider.name + "' must use the " +
                        "GroundSurface layer so ground queries and authoring validation " +
                        "share the same geometry.",
                        collider);
                    continue;
                }

                if (!collider.isTrigger)
                {
                    validGroundColliderCount++;
                }

                float tilt = Vector3.Angle(collider.transform.up, Vector3.up);
                if (tilt > MaximumGroundTiltDegrees)
                {
                    report.Add(
                        ArenaContentValidationCode.TiltedGroundCollider,
                        "Ground collider '" + collider.name + "' is tilted " +
                        tilt.ToString("0.###") + " degrees; current combat ground must be flat.",
                        collider);
                }

                float topHeight = collider.bounds.max.y;
                if (Mathf.Abs(topHeight - presentation.CombatGroundHeight) >
                    GroundHeightTolerance)
                {
                    report.Add(
                        ArenaContentValidationCode.GroundHeightMismatch,
                        "Ground collider '" + collider.name + "' top is at y=" +
                        topHeight.ToString("0.###") + ", expected y=" +
                        presentation.CombatGroundHeight.ToString("0.###") + " ± " +
                        GroundHeightTolerance.ToString("0.###") + ".",
                        collider);
                }
            }

            if (validGroundColliderCount == 0)
            {
                report.Add(
                    ArenaContentValidationCode.MissingGroundCollider,
                    "GroundRoot must contain at least one enabled, non-trigger " +
                    "GroundSurface collider.",
                    groundRoot);
            }
        }

        private static void ValidateVisualLayers(
            ArenaPresentation presentation,
            ArenaContentValidationReport report)
        {
            Transform visualRoot = presentation.VisualRoot;
            if (visualRoot == null)
            {
                return;
            }

            Transform[] transforms = visualRoot.GetComponentsInChildren<Transform>(true);
            for (int index = 0; index < transforms.Length; index++)
            {
                Transform candidate = transforms[index];
                if ((AuthoritativeGameplayLayerMask & (1 << candidate.gameObject.layer)) == 0)
                {
                    continue;
                }

                report.Add(
                    ArenaContentValidationCode.VisualUsesAuthoritativeLayer,
                    "Visual object '" + candidate.name + "' uses authoritative layer " +
                    candidate.gameObject.layer +
                    ". Move collision to GameplayProxyRoot and put art on a visual-only layer.",
                    candidate);
            }
        }

        private static void ValidateGameplayProxies(
            ArenaPresentation presentation,
            ArenaContentValidationReport report)
        {
            Transform proxyRoot = presentation.GameplayProxyRoot;
            if (proxyRoot == null)
            {
                return;
            }

            Rigidbody[] rigidbodies = proxyRoot.GetComponentsInChildren<Rigidbody>(true);
            for (int index = 0; index < rigidbodies.Length; index++)
            {
                report.Add(
                    ArenaContentValidationCode.RigidbodyOnGameplayProxy,
                    "Gameplay proxy '" + rigidbodies[index].name +
                    "' cannot use Rigidbody motion.",
                    rigidbodies[index]);
            }
        }

        private static void ValidateCaptureAnchors(
            ArenaPresentation presentation,
            ArenaContentValidationReport report)
        {
            bool hasOverview = false;
            bool hasGameplay = false;
            bool hasDenseCombat = false;
            var kinds = new HashSet<ArenaCaptureAnchorKind>();
            for (int index = 0; index < presentation.CaptureAnchorCount; index++)
            {
                ArenaCaptureAnchor anchor = presentation.GetCaptureAnchor(index);
                if (anchor == null)
                {
                    report.Add(
                        ArenaContentValidationCode.NullCaptureAnchor,
                        "Capture anchor entry " + index + " is null.",
                        presentation);
                    continue;
                }

                if (!IsSameOrChild(anchor.transform, presentation.transform))
                {
                    report.Add(
                        ArenaContentValidationCode.CaptureAnchorOutsidePresentation,
                        "Capture anchor '" + anchor.name +
                        "' must be inside its ArenaPresentation hierarchy.",
                        anchor);
                }

                if (anchor.Kind == ArenaCaptureAnchorKind.None ||
                    !Enum.IsDefined(typeof(ArenaCaptureAnchorKind), anchor.Kind))
                {
                    report.Add(
                        ArenaContentValidationCode.InvalidCaptureAnchor,
                        "Capture anchor '" + anchor.name + "' has no valid kind.",
                        anchor);
                    continue;
                }

                if (!kinds.Add(anchor.Kind))
                {
                    report.Add(
                        ArenaContentValidationCode.DuplicateCaptureAnchor,
                        "ArenaPresentation has more than one " + anchor.Kind +
                        " capture anchor.",
                        anchor);
                }

                hasOverview |= anchor.Kind == ArenaCaptureAnchorKind.Overview;
                hasGameplay |= anchor.Kind == ArenaCaptureAnchorKind.Gameplay;
                hasDenseCombat |= anchor.Kind == ArenaCaptureAnchorKind.DenseCombat;
            }

            RequireCaptureAnchor(
                hasOverview,
                ArenaContentValidationCode.MissingOverviewAnchor,
                ArenaCaptureAnchorKind.Overview,
                presentation,
                report);
            RequireCaptureAnchor(
                hasGameplay,
                ArenaContentValidationCode.MissingGameplayAnchor,
                ArenaCaptureAnchorKind.Gameplay,
                presentation,
                report);
            RequireCaptureAnchor(
                hasDenseCombat,
                ArenaContentValidationCode.MissingDenseCombatAnchor,
                ArenaCaptureAnchorKind.DenseCombat,
                presentation,
                report);
        }

        private static void LoadAndValidatePrefab(
            ArenaCatalogEntry entry,
            ArenaContentValidationReport report)
        {
            ArenaPresentation prefab = entry.PresentationPrefab;
            string path = prefab != null ? AssetDatabase.GetAssetPath(prefab.gameObject) : string.Empty;
            if (prefab == null || string.IsNullOrWhiteSpace(path))
            {
                report.Add(
                    ArenaContentValidationCode.MissingPresentationPrefab,
                    "Arena '" + entry.ContentId + "' has no saved presentation prefab.",
                    prefab);
                return;
            }

            if (PrefabUtility.GetPrefabAssetType(prefab.gameObject) == PrefabAssetType.NotAPrefab)
            {
                report.Add(
                    ArenaContentValidationCode.InvalidPresentationPrefab,
                    "Arena '" + entry.ContentId + "' presentation must reference a prefab asset.",
                    prefab);
                return;
            }

            GameObject contents = null;
            try
            {
                contents = PrefabUtility.LoadPrefabContents(path);
                ArenaPresentation loaded = contents.GetComponent<ArenaPresentation>();
                if (loaded == null)
                {
                    report.Add(
                        ArenaContentValidationCode.InvalidPresentationPrefab,
                        "Presentation prefab '" + path +
                        "' requires ArenaPresentation on its root.",
                        prefab);
                    return;
                }

                report.Merge(ValidatePresentation(loaded));
                ValidateEntryMatchesPresentation(entry, loaded, report, prefab);
            }
            finally
            {
                if (contents != null)
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }
        }

        private static void LoadAndValidateScene(
            ArenaCatalogEntry entry,
            ArenaContentValidationReport report)
        {
            string path = entry.PresentationScenePath;
            SceneAsset sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            if (sceneAsset == null)
            {
                report.Add(
                    ArenaContentValidationCode.MissingPresentationScene,
                    "Arena '" + entry.ContentId + "' scene does not exist at '" + path + "'.");
                return;
            }

            Scene scene = SceneManager.GetSceneByPath(path);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            try
            {
                if (opened)
                {
                    scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                }

                ArenaPresentation presentation = null;
                int presentationCount = 0;
                GameObject[] roots = scene.GetRootGameObjects();
                for (int rootIndex = 0; rootIndex < roots.Length; rootIndex++)
                {
                    ArenaPresentation[] candidates =
                        roots[rootIndex].GetComponentsInChildren<ArenaPresentation>(true);
                    presentationCount += candidates.Length;
                    if (presentation == null && candidates.Length > 0)
                    {
                        presentation = candidates[0];
                    }
                }

                if (presentationCount != 1 || presentation == null)
                {
                    report.Add(
                        ArenaContentValidationCode.InvalidPresentationScene,
                        "Additive scene '" + path +
                        "' must contain exactly one ArenaPresentation root; found " +
                        presentationCount + ".",
                        sceneAsset);
                    return;
                }

                report.Merge(ValidatePresentation(presentation));
                ValidateEntryMatchesPresentation(entry, presentation, report, sceneAsset);
            }
            finally
            {
                if (opened && scene.IsValid() && scene.isLoaded)
                {
                    EditorSceneManager.CloseScene(scene, true);
                }
            }
        }

        private static void ValidateEntryMatchesPresentation(
            ArenaCatalogEntry entry,
            ArenaPresentation presentation,
            ArenaContentValidationReport report,
            UnityEngine.Object context)
        {
            if (entry == null || presentation == null)
            {
                return;
            }

            if (entry.ContentId != presentation.ContentId)
            {
                report.Add(
                    ArenaContentValidationCode.PresentationIdentityMismatch,
                    "Catalog ID '" + entry.ContentId + "' does not match presentation ID '" +
                    presentation.ContentId + "'.",
                    context);
            }

            if (entry.Mode != presentation.Mode)
            {
                report.Add(
                    ArenaContentValidationCode.PresentationModeMismatch,
                    "Catalog mode " + entry.Mode + " does not match presentation mode " +
                    presentation.Mode + " for '" + entry.ContentId + "'.",
                    context);
            }
        }

        private static void ValidateRequiredRoot(
            ArenaPresentation presentation,
            Transform value,
            ArenaContentValidationCode missingCode,
            string label,
            ArenaContentValidationReport report)
        {
            if (value == null)
            {
                report.Add(
                    missingCode,
                    "ArenaPresentation requires a serialized " + label + " root.",
                    presentation);
                return;
            }

            if (!IsSameOrChild(value, presentation.transform))
            {
                report.Add(
                    ArenaContentValidationCode.RootOutsidePresentation,
                    "The " + label + " root '" + value.name +
                    "' must be inside the ArenaPresentation hierarchy.",
                    value);
            }
        }

        private static void RequireCaptureAnchor(
            bool present,
            ArenaContentValidationCode code,
            ArenaCaptureAnchorKind kind,
            ArenaPresentation presentation,
            ArenaContentValidationReport report)
        {
            if (!present)
            {
                report.Add(
                    code,
                    "ArenaPresentation requires one " + kind + " capture anchor.",
                    presentation);
            }
        }

        private static bool IsSameOrChild(Transform candidate, Transform root)
        {
            return candidate != null && root != null &&
                (candidate == root || candidate.IsChildOf(root));
        }

        private static void Increment(
            Dictionary<ArenaContentMode, int> values,
            ArenaContentMode key)
        {
            values.TryGetValue(key, out int count);
            values[key] = count + 1;
        }

        private static void ThrowIfInvalid(ArenaContentValidationReport report)
        {
            if (!report.IsValid)
            {
                throw new BuildFailedException(report.ToString());
            }
        }
    }
}
