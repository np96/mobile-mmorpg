using System;
using System.Collections.Generic;
using SessionRpg.Unity.Arenas;
using UnityEditor;
using UnityEngine;

namespace SessionRpg.Editor.Arenas
{
    public readonly struct ArenaCatalogEntryAuthoring
    {
        public ArenaCatalogEntryAuthoring(
            ArenaContentId contentId,
            ArenaContentMode mode,
            bool isDefaultForMode,
            ArenaPresentation presentationPrefab,
            string presentationScenePath)
        {
            ContentId = contentId;
            Mode = mode;
            IsDefaultForMode = isDefaultForMode;
            PresentationPrefab = presentationPrefab;
            PresentationScenePath = presentationScenePath ?? string.Empty;
        }

        public ArenaContentId ContentId { get; }

        public ArenaContentMode Mode { get; }

        public bool IsDefaultForMode { get; }

        public ArenaPresentation PresentationPrefab { get; }

        public string PresentationScenePath { get; }

        public static ArenaCatalogEntryAuthoring ForPrefab(
            ArenaContentId contentId,
            ArenaContentMode mode,
            ArenaPresentation presentationPrefab,
            bool isDefaultForMode = false)
        {
            return new ArenaCatalogEntryAuthoring(
                contentId,
                mode,
                isDefaultForMode,
                presentationPrefab,
                string.Empty);
        }

        public static ArenaCatalogEntryAuthoring ForScene(
            ArenaContentId contentId,
            ArenaContentMode mode,
            SceneAsset presentationScene,
            bool isDefaultForMode = false)
        {
            string scenePath = presentationScene != null
                ? AssetDatabase.GetAssetPath(presentationScene)
                : string.Empty;
            return ForScene(contentId, mode, scenePath, isDefaultForMode);
        }

        public static ArenaCatalogEntryAuthoring ForScene(
            ArenaContentId contentId,
            ArenaContentMode mode,
            string presentationScenePath,
            bool isDefaultForMode = false)
        {
            return new ArenaCatalogEntryAuthoring(
                contentId,
                mode,
                isDefaultForMode,
                null,
                presentationScenePath);
        }

        public ArenaCatalogEntryAuthoring WithDefault(bool isDefaultForMode)
        {
            return new ArenaCatalogEntryAuthoring(
                ContentId,
                Mode,
                isDefaultForMode,
                PresentationPrefab,
                PresentationScenePath);
        }
    }

    /// <summary>
    /// Serialized authoring API shared by deterministic generators and manual
    /// Editor tooling. Runtime content remains read-only.
    /// </summary>
    public static class ArenaContentAuthoring
    {
        public static ArenaPresentation ConfigurePresentation(
            GameObject presentationRoot,
            ArenaContentId contentId,
            ArenaContentMode mode,
            ArenaPresentationCapabilities capabilities,
            Transform visualRoot,
            Transform gameplayProxyRoot,
            Transform groundRoot,
            float combatGroundHeight,
            int presentationVersion,
            ArenaCaptureAnchor[] captureAnchors)
        {
            if (presentationRoot == null)
            {
                throw new ArgumentNullException(nameof(presentationRoot));
            }

            ArenaPresentation presentation =
                presentationRoot.GetComponent<ArenaPresentation>();
            if (presentation == null)
            {
                presentation = Undo.AddComponent<ArenaPresentation>(presentationRoot);
            }

            Undo.RecordObject(presentation, "Configure arena presentation");
            var serialized = new SerializedObject(presentation);
            SetContentId(serialized.FindProperty("_contentId"), contentId);
            serialized.FindProperty("_mode").enumValueIndex = (int)mode;
            serialized.FindProperty("_presentationVersion").intValue = presentationVersion;
            serialized.FindProperty("_capabilities").intValue = (int)capabilities;
            serialized.FindProperty("_visualRoot").objectReferenceValue = visualRoot;
            serialized.FindProperty("_gameplayProxyRoot").objectReferenceValue =
                gameplayProxyRoot;
            serialized.FindProperty("_groundRoot").objectReferenceValue = groundRoot;
            serialized.FindProperty("_combatGroundHeight").floatValue =
                combatGroundHeight;

            SerializedProperty anchors = serialized.FindProperty("_captureAnchors");
            int anchorCount = captureAnchors != null ? captureAnchors.Length : 0;
            anchors.arraySize = anchorCount;
            for (int index = 0; index < anchorCount; index++)
            {
                anchors.GetArrayElementAtIndex(index).objectReferenceValue =
                    captureAnchors[index];
            }

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(presentation);
            return presentation;
        }

        public static ArenaCaptureAnchor CreateCaptureAnchor(
            Transform parent,
            ArenaCaptureAnchorKind kind,
            Vector3 localPosition,
            Quaternion localRotation,
            float fieldOfView = 50f,
            bool showHud = true)
        {
            if (parent == null)
            {
                throw new ArgumentNullException(nameof(parent));
            }

            var anchorObject = new GameObject(kind + " Capture Anchor");
            Undo.RegisterCreatedObjectUndo(anchorObject, "Create arena capture anchor");
            anchorObject.transform.SetParent(parent, false);
            anchorObject.transform.localPosition = localPosition;
            anchorObject.transform.localRotation = localRotation;
            ArenaCaptureAnchor anchor =
                Undo.AddComponent<ArenaCaptureAnchor>(anchorObject);
            ConfigureCaptureAnchor(anchor, kind, fieldOfView, showHud);
            return anchor;
        }

        public static void ConfigureCaptureAnchor(
            ArenaCaptureAnchor anchor,
            ArenaCaptureAnchorKind kind,
            float fieldOfView = 50f,
            bool showHud = true)
        {
            if (anchor == null)
            {
                throw new ArgumentNullException(nameof(anchor));
            }

            Undo.RecordObject(anchor, "Configure arena capture anchor");
            var serialized = new SerializedObject(anchor);
            serialized.FindProperty("_kind").enumValueIndex = (int)kind;
            serialized.FindProperty("_fieldOfView").floatValue =
                Mathf.Clamp(fieldOfView, 15f, 100f);
            serialized.FindProperty("_showHud").boolValue = showHud;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(anchor);
        }

        public static ArenaCatalogAsset CreateCatalogAsset(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                throw new ArgumentException(
                    "A project-relative asset path is required.",
                    nameof(assetPath));
            }

            if (!assetPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                !assetPath.EndsWith(".asset", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "Arena catalog path must be an Assets/*.asset path.",
                    nameof(assetPath));
            }

            if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
            {
                throw new InvalidOperationException(
                    "An asset already exists at '" + assetPath + "'.");
            }

            var catalog = ScriptableObject.CreateInstance<ArenaCatalogAsset>();
            AssetDatabase.CreateAsset(catalog, assetPath);
            AssetDatabase.SaveAssets();
            return catalog;
        }

        public static void ReplaceCatalogEntries(
            ArenaCatalogAsset catalog,
            params ArenaCatalogEntryAuthoring[] entries)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            ArenaCatalogEntryAuthoring[] sorted = entries != null
                ? (ArenaCatalogEntryAuthoring[])entries.Clone()
                : Array.Empty<ArenaCatalogEntryAuthoring>();
            Array.Sort(sorted, CompareEntries);

            Undo.RecordObject(catalog, "Replace arena catalog entries");
            var serialized = new SerializedObject(catalog);
            SerializedProperty values = serialized.FindProperty("_entries");
            values.arraySize = sorted.Length;
            for (int index = 0; index < sorted.Length; index++)
            {
                WriteEntry(values.GetArrayElementAtIndex(index), sorted[index]);
            }

            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(catalog);
        }

        public static void UpsertCatalogEntry(
            ArenaCatalogAsset catalog,
            ArenaCatalogEntryAuthoring entry)
        {
            if (catalog == null)
            {
                throw new ArgumentNullException(nameof(catalog));
            }

            var values = new List<ArenaCatalogEntryAuthoring>(catalog.Count + 1);
            bool replaced = false;
            for (int index = 0; index < catalog.Count; index++)
            {
                ArenaCatalogEntry existing = catalog.GetEntry(index);
                if (existing == null)
                {
                    continue;
                }

                ArenaCatalogEntryAuthoring value = FromRuntimeEntry(existing);
                if (existing.ContentId == entry.ContentId)
                {
                    value = entry;
                    replaced = true;
                }

                if (entry.IsDefaultForMode &&
                    value.ContentId != entry.ContentId &&
                    value.Mode == entry.Mode)
                {
                    value = value.WithDefault(false);
                }

                values.Add(value);
            }

            if (!replaced)
            {
                values.Add(entry);
            }

            ReplaceCatalogEntries(catalog, values.ToArray());
        }

        private static ArenaCatalogEntryAuthoring FromRuntimeEntry(
            ArenaCatalogEntry entry)
        {
            return new ArenaCatalogEntryAuthoring(
                entry.ContentId,
                entry.Mode,
                entry.IsDefaultForMode,
                entry.PresentationPrefab,
                entry.PresentationScenePath);
        }

        private static int CompareEntries(
            ArenaCatalogEntryAuthoring left,
            ArenaCatalogEntryAuthoring right)
        {
            int idComparison = string.CompareOrdinal(
                left.ContentId.Value,
                right.ContentId.Value);
            return idComparison != 0
                ? idComparison
                : left.Mode.CompareTo(right.Mode);
        }

        private static void WriteEntry(
            SerializedProperty property,
            ArenaCatalogEntryAuthoring entry)
        {
            SetContentId(property.FindPropertyRelative("_contentId"), entry.ContentId);
            property.FindPropertyRelative("_mode").enumValueIndex = (int)entry.Mode;
            property.FindPropertyRelative("_isDefaultForMode").boolValue =
                entry.IsDefaultForMode;
            property.FindPropertyRelative("_presentationPrefab").objectReferenceValue =
                entry.PresentationPrefab;
            property.FindPropertyRelative("_presentationScenePath").stringValue =
                entry.PresentationScenePath ?? string.Empty;
        }

        private static void SetContentId(
            SerializedProperty property,
            ArenaContentId contentId)
        {
            if (property == null)
            {
                throw new InvalidOperationException(
                    "Arena content ID serialization contract is missing.");
            }

            property.FindPropertyRelative("_value").stringValue = contentId.Value;
        }
    }
}
