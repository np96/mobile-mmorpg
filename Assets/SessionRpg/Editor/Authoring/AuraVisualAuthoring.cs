using System;
using System.Collections.Generic;
using SessionRpg.Unity.Gameplay;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SessionRpg.Editor.Authoring
{
    internal static class AuraVisualAuthoring
    {
        private const int RingPointCount = 40;

        public static AuraVisualPresenter Build(GameObject combatant, Material material)
        {
            if (combatant == null)
            {
                throw new ArgumentNullException(nameof(combatant));
            }

            if (material == null)
            {
                throw new ArgumentNullException(nameof(material));
            }

            AuraVisualPresenter presenter = combatant.GetComponent<AuraVisualPresenter>();
            if (presenter == null)
            {
                presenter = combatant.AddComponent<AuraVisualPresenter>();
            }

            GameObject visualRoot = new GameObject("AuraVisuals");
            visualRoot.transform.SetParent(combatant.transform, false);

            BuildWard(
                visualRoot.transform,
                material,
                out GameObject wardRoot,
                out Transform[] wardBands,
                out Renderer[] wardRenderers);
            BuildGuardian(
                visualRoot.transform,
                material,
                out GameObject guardianRoot,
                out Transform[] guardianRunes,
                out Renderer[] guardianRenderers);
            BuildFrozen(
                visualRoot.transform,
                material,
                out GameObject frozenRoot,
                out Transform[] frozenSpikes,
                out Renderer[] frozenRenderers);
            BuildControl(
                visualRoot.transform,
                material,
                out GameObject controlRoot,
                out Transform[] controlRunes,
                out Renderer[] controlRenderers);
            BuildBurning(
                visualRoot.transform,
                material,
                out GameObject burningRoot,
                out Transform[] flameWisps,
                out Renderer[] burningRenderers);
            BuildPoison(
                visualRoot.transform,
                material,
                out GameObject poisonRoot,
                out Transform[] poisonNodes,
                out Renderer[] poisonRenderers);
            BuildStealth(
                visualRoot.transform,
                material,
                out GameObject stealthRoot,
                out Transform[] stealthWisps,
                out Renderer[] stealthRenderers);
            BuildPolymorph(
                visualRoot.transform,
                material,
                out GameObject polymorphRoot,
                out Renderer[] polymorphRenderers);
            BuildPersistentMotifs(
                visualRoot.transform,
                material,
                out GameObject[] persistentRoots,
                out Transform[] persistentMotionRoots,
                out Renderer[] persistentRenderers,
                out int[] persistentRendererStarts,
                out int[] persistentRendererCounts);

            SerializedObject serialized = new SerializedObject(presenter);
            serialized.Update();
            SetObject(serialized, "_wardRoot", wardRoot);
            SetArray(serialized, "_wardBands", wardBands);
            SetArray(serialized, "_wardRenderers", wardRenderers);
            SetObject(serialized, "_guardianRoot", guardianRoot);
            SetArray(serialized, "_guardianRunes", guardianRunes);
            SetArray(serialized, "_guardianRenderers", guardianRenderers);
            SetObject(serialized, "_frozenRoot", frozenRoot);
            SetArray(serialized, "_frozenSpikes", frozenSpikes);
            SetArray(serialized, "_frozenRenderers", frozenRenderers);
            SetObject(serialized, "_controlRoot", controlRoot);
            SetArray(serialized, "_controlRunes", controlRunes);
            SetArray(serialized, "_controlRenderers", controlRenderers);
            SetObject(serialized, "_burningRoot", burningRoot);
            SetArray(serialized, "_flameWisps", flameWisps);
            SetArray(serialized, "_burningRenderers", burningRenderers);
            SetObject(serialized, "_poisonRoot", poisonRoot);
            SetArray(serialized, "_poisonNodes", poisonNodes);
            SetArray(serialized, "_poisonRenderers", poisonRenderers);
            SetObject(serialized, "_stealthRoot", stealthRoot);
            SetArray(serialized, "_stealthWisps", stealthWisps);
            SetArray(serialized, "_stealthRenderers", stealthRenderers);
            SetObject(serialized, "_polymorphRoot", polymorphRoot);
            SetArray(serialized, "_polymorphRenderers", polymorphRenderers);
            SetArray(serialized, "_persistentRoots", persistentRoots);
            SetArray(serialized, "_persistentMotionRoots", persistentMotionRoots);
            SetArray(serialized, "_persistentRenderers", persistentRenderers);
            SetIntArray(serialized, "_persistentRendererStarts", persistentRendererStarts);
            SetIntArray(serialized, "_persistentRendererCounts", persistentRendererCounts);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return presenter;
        }

        private static void BuildWard(
            Transform parent,
            Material material,
            out GameObject root,
            out Transform[] bands,
            out Renderer[] renderers)
        {
            root = CreateRoot("WardBands", parent);
            bands = new Transform[3];
            renderers = new Renderer[3];
            float[] heights = { 0.5f, 1.12f, 1.72f };
            float[] radii = { 0.62f, 0.72f, 0.52f };
            for (int index = 0; index < bands.Length; index++)
            {
                LineRenderer ring = CreateRing(
                    "WardBand_" + index,
                    root.transform,
                    new Vector3(0f, heights[index], 0f),
                    radii[index],
                    0.038f,
                    material,
                    index == 1 ? 6 : 4);
                bands[index] = ring.transform;
                renderers[index] = ring;
            }

            root.SetActive(false);
        }

        private static void BuildGuardian(
            Transform parent,
            Material material,
            out GameObject root,
            out Transform[] runes,
            out Renderer[] renderers)
        {
            root = CreateRoot("GuardianRunes", parent);
            runes = new Transform[4];
            renderers = new Renderer[4];
            for (int index = 0; index < runes.Length; index++)
            {
                float angle = index * 90f;
                Vector3 position = Quaternion.Euler(0f, angle, 0f) *
                    new Vector3(0f, 1.1f, 0.79f);
                GameObject rune = CreatePrimitive(
                    "GuardianRune_" + index,
                    PrimitiveType.Cube,
                    root.transform,
                    position,
                    Quaternion.Euler(0f, angle, 45f),
                    new Vector3(0.11f, 0.42f, 0.045f),
                    material);
                runes[index] = rune.transform;
                renderers[index] = rune.GetComponent<Renderer>();
            }

            root.SetActive(false);
        }

        private static void BuildFrozen(
            Transform parent,
            Material material,
            out GameObject root,
            out Transform[] spikes,
            out Renderer[] renderers)
        {
            root = CreateRoot("FrozenSpikes", parent);
            spikes = new Transform[6];
            renderers = new Renderer[7];
            LineRenderer baseRing = CreateRing(
                "FrozenSeal",
                root.transform,
                new Vector3(0f, 0.055f, 0f),
                0.62f,
                0.055f,
                material,
                8);
            renderers[0] = baseRing;

            for (int index = 0; index < spikes.Length; index++)
            {
                float angle = index * 60f;
                float height = index % 2 == 0 ? 0.52f : 0.38f;
                Vector3 radial = Quaternion.Euler(0f, angle, 0f) *
                    new Vector3(0f, 0f, 0.55f);
                GameObject spike = CreatePrimitive(
                    "IceSpike_" + index,
                    PrimitiveType.Cube,
                    root.transform,
                    radial + new Vector3(0f, height * 0.42f, 0f),
                    Quaternion.Euler(18f, angle, 45f),
                    new Vector3(0.11f, height, 0.11f),
                    material);
                spikes[index] = spike.transform;
                renderers[index + 1] = spike.GetComponent<Renderer>();
            }

            root.SetActive(false);
        }

        private static void BuildControl(
            Transform parent,
            Material material,
            out GameObject root,
            out Transform[] runes,
            out Renderer[] renderers)
        {
            root = CreateRoot("ControlCrown", parent);
            runes = new Transform[3];
            renderers = new Renderer[4];
            LineRenderer crown = CreateRing(
                "ControlHalo",
                root.transform,
                new Vector3(0f, 2.17f, 0f),
                0.42f,
                0.035f,
                material,
                3);
            renderers[0] = crown;

            for (int index = 0; index < runes.Length; index++)
            {
                float angle = index * 120f;
                Vector3 position = Quaternion.Euler(0f, angle, 0f) *
                    new Vector3(0f, 2.25f, 0.46f);
                GameObject rune = CreatePrimitive(
                    "ControlDiamond_" + index,
                    PrimitiveType.Cube,
                    root.transform,
                    position,
                    Quaternion.Euler(45f, angle, 45f),
                    new Vector3(0.14f, 0.14f, 0.14f),
                    material);
                runes[index] = rune.transform;
                renderers[index + 1] = rune.GetComponent<Renderer>();
            }

            root.SetActive(false);
        }

        private static void BuildBurning(
            Transform parent,
            Material material,
            out GameObject root,
            out Transform[] wisps,
            out Renderer[] renderers)
        {
            root = CreateRoot("FlameWisps", parent);
            wisps = new Transform[4];
            renderers = new Renderer[4];
            for (int index = 0; index < wisps.Length; index++)
            {
                float angle = (index * 90f) + 35f;
                Vector3 position = Quaternion.Euler(0f, angle, 0f) *
                    new Vector3(0f, 0.34f, 0.5f);
                GameObject wisp = CreatePrimitive(
                    "FlameWisp_" + index,
                    PrimitiveType.Capsule,
                    root.transform,
                    position,
                    Quaternion.Euler(0f, angle, index % 2 == 0 ? 16f : -16f),
                    new Vector3(0.13f, 0.3f, 0.13f),
                    material);
                wisps[index] = wisp.transform;
                renderers[index] = wisp.GetComponent<Renderer>();
            }

            root.SetActive(false);
        }

        private static void BuildPoison(
            Transform parent,
            Material material,
            out GameObject root,
            out Transform[] nodes,
            out Renderer[] renderers)
        {
            root = CreateRoot("PoisonOrbit", parent);
            nodes = new Transform[3];
            renderers = new Renderer[4];
            LineRenderer triangle = CreateRing(
                "PoisonTriangle",
                root.transform,
                new Vector3(0f, 0.72f, 0f),
                0.55f,
                0.035f,
                material,
                3);
            renderers[0] = triangle;
            for (int index = 0; index < nodes.Length; index++)
            {
                float angle = index * 120f;
                Vector3 position = Quaternion.Euler(0f, angle, 0f) *
                    new Vector3(0f, 0.76f, 0.55f);
                GameObject node = CreatePrimitive(
                    "PoisonNode_" + index,
                    PrimitiveType.Sphere,
                    root.transform,
                    position,
                    Quaternion.identity,
                    Vector3.one * 0.13f,
                    material);
                nodes[index] = node.transform;
                renderers[index + 1] = node.GetComponent<Renderer>();
            }

            root.SetActive(false);
        }

        private static void BuildStealth(
            Transform parent,
            Material material,
            out GameObject root,
            out Transform[] wisps,
            out Renderer[] renderers)
        {
            root = CreateRoot("FriendlyStealthTrace", parent);
            wisps = new Transform[4];
            renderers = new Renderer[5];
            LineRenderer brokenSilhouette = CreateRing(
                "StealthTrace",
                root.transform,
                new Vector3(0f, 0.065f, 0f),
                0.52f,
                0.028f,
                material,
                8);
            renderers[0] = brokenSilhouette;
            for (int index = 0; index < wisps.Length; index++)
            {
                float angle = index * 90f;
                Vector3 position = Quaternion.Euler(0f, angle, 0f) *
                    new Vector3(0f, 0.09f, 0.52f);
                GameObject wisp = CreatePrimitive(
                    "ShadowTrace_" + index,
                    PrimitiveType.Sphere,
                    root.transform,
                    position,
                    Quaternion.identity,
                    new Vector3(0.12f, 0.035f, 0.22f),
                    material);
                wisps[index] = wisp.transform;
                renderers[index + 1] = wisp.GetComponent<Renderer>();
            }

            root.SetActive(false);
        }

        private static void BuildPolymorph(
            Transform parent,
            Material material,
            out GameObject root,
            out Renderer[] renderers)
        {
            root = CreateRoot("PolymorphCloud", parent);
            renderers = new Renderer[6];
            Vector3[] cloudPositions =
            {
                new Vector3(-0.22f, 1.92f, 0f),
                new Vector3(0.02f, 2.04f, 0f),
                new Vector3(0.26f, 1.91f, 0f),
                new Vector3(0.03f, 1.83f, 0.08f),
            };
            for (int index = 0; index < cloudPositions.Length; index++)
            {
                GameObject cloud = CreatePrimitive(
                    "WoolCloud_" + index,
                    PrimitiveType.Sphere,
                    root.transform,
                    cloudPositions[index],
                    Quaternion.identity,
                    new Vector3(0.27f, 0.22f, 0.19f),
                    material);
                renderers[index] = cloud.GetComponent<Renderer>();
            }

            for (int index = 0; index < 2; index++)
            {
                GameObject horn = CreatePrimitive(
                    "CurlHorn_" + index,
                    PrimitiveType.Cube,
                    root.transform,
                    new Vector3(index == 0 ? -0.27f : 0.27f, 2.17f, 0f),
                    Quaternion.Euler(0f, 0f, index == 0 ? -38f : 38f),
                    new Vector3(0.08f, 0.22f, 0.08f),
                    material);
                renderers[index + 4] = horn.GetComponent<Renderer>();
            }

            root.SetActive(false);
        }

        private static void BuildPersistentMotifs(
            Transform parent,
            Material material,
            out GameObject[] roots,
            out Transform[] motionRoots,
            out Renderer[] renderers,
            out int[] rendererStarts,
            out int[] rendererCounts)
        {
            int motifCount = AuraVisualPresenter.AuthoredPersistentMotifCount;
            roots = new GameObject[motifCount];
            motionRoots = new Transform[motifCount];
            rendererStarts = new int[motifCount];
            rendererCounts = new int[motifCount];
            var rendererList = new List<Renderer>(48);

            for (int index = 0; index < motifCount; index++)
            {
                PersistentAuraVisual motif = (PersistentAuraVisual)(1 << index);
                GameObject root = CreateRoot(GetPersistentMotifName(motif), parent);
                GameObject motionRoot = CreateRoot("Motion", root.transform);
                roots[index] = root;
                motionRoots[index] = motionRoot.transform;
                rendererStarts[index] = rendererList.Count;
                BuildPersistentMotifGeometry(
                    motif,
                    motionRoot.transform,
                    material,
                    rendererList);
                rendererCounts[index] = rendererList.Count - rendererStarts[index];
                root.SetActive(false);
            }

            renderers = rendererList.ToArray();
        }

        private static void BuildPersistentMotifGeometry(
            PersistentAuraVisual motif,
            Transform parent,
            Material material,
            List<Renderer> renderers)
        {
            switch (motif)
            {
                case PersistentAuraVisual.VampiricTouch:
                    AddRing(renderers, "VampiricHex", parent, 1.12f, 0.56f, 0.038f, 6, material);
                    AddPrimitive(renderers, "FangLeft", PrimitiveType.Cube, parent,
                        new Vector3(-0.2f, 1.08f, 0.48f), Quaternion.Euler(0f, 0f, -18f),
                        new Vector3(0.08f, 0.34f, 0.055f), material);
                    AddPrimitive(renderers, "FangRight", PrimitiveType.Cube, parent,
                        new Vector3(0.2f, 1.08f, 0.48f), Quaternion.Euler(0f, 0f, 18f),
                        new Vector3(0.08f, 0.34f, 0.055f), material);
                    break;
                case PersistentAuraVisual.ShadowWordPain:
                    AddRing(renderers, "PainBrand", parent, 2.08f, 0.4f, 0.042f, 3, material);
                    AddPrimitive(renderers, "PainThornLeft", PrimitiveType.Cube, parent,
                        new Vector3(-0.34f, 2.16f, 0f), Quaternion.Euler(0f, 0f, -42f),
                        new Vector3(0.07f, 0.28f, 0.05f), material);
                    AddPrimitive(renderers, "PainThornRight", PrimitiveType.Cube, parent,
                        new Vector3(0.34f, 2.16f, 0f), Quaternion.Euler(0f, 0f, 42f),
                        new Vector3(0.07f, 0.28f, 0.05f), material);
                    break;
                case PersistentAuraVisual.DevouringPlague:
                    AddRing(renderers, "PlagueOrbit", parent, 0.68f, 0.62f, 0.045f, 6, material);
                    for (int index = 0; index < 3; index++)
                    {
                        float angle = index * 120f;
                        Vector3 position = Quaternion.Euler(0f, angle, 0f) *
                            new Vector3(0f, 0.72f, 0.62f);
                        AddPrimitive(renderers, "PlagueNode_" + index, PrimitiveType.Sphere, parent,
                            position, Quaternion.identity, Vector3.one * 0.13f, material);
                    }
                    break;
                case PersistentAuraVisual.MindFlay:
                    AddRing(renderers, "MindFractureLow", parent, 0.58f, 0.52f, 0.032f, 4, material);
                    AddRing(renderers, "MindFractureMid", parent, 1.08f, 0.64f, 0.038f, 5, material);
                    AddRing(renderers, "MindFractureHigh", parent, 1.58f, 0.48f, 0.032f, 3, material);
                    break;
                case PersistentAuraVisual.Dispersion:
                    AddRing(renderers, "DispersionSeal", parent, 1.04f, 0.78f, 0.045f, 6, material);
                    for (int index = 0; index < 3; index++)
                    {
                        float angle = index * 120f;
                        Vector3 position = Quaternion.Euler(0f, angle, 0f) *
                            new Vector3(0f, 1.02f, 0.72f);
                        AddPrimitive(renderers, "DispersionFacet_" + index, PrimitiveType.Cube, parent,
                            position, Quaternion.Euler(45f, angle, 45f),
                            new Vector3(0.16f, 0.58f, 0.07f), material);
                    }
                    break;
                case PersistentAuraVisual.PsychicHorror:
                    AddRing(renderers, "HorrorCrown", parent, 2.16f, 0.46f, 0.045f, 5, material);
                    for (int index = 0; index < 3; index++)
                    {
                        float x = (index - 1) * 0.3f;
                        AddPrimitive(renderers, "HorrorSpike_" + index, PrimitiveType.Cube, parent,
                            new Vector3(x, 2.43f + (index == 1 ? 0.12f : 0f), 0f),
                            Quaternion.Euler(0f, 0f, index == 0 ? -24f : index == 2 ? 24f : 0f),
                            new Vector3(0.075f, 0.34f, 0.055f), material);
                    }
                    break;
                case PersistentAuraVisual.SerpentSting:
                    AddRing(renderers, "SerpentCoil", parent, 0.72f, 0.58f, 0.04f, 3, material);
                    AddPrimitive(renderers, "SerpentFang", PrimitiveType.Capsule, parent,
                        new Vector3(0f, 0.88f, 0.52f), Quaternion.Euler(24f, 0f, 0f),
                        new Vector3(0.09f, 0.25f, 0.09f), material);
                    break;
                case PersistentAuraVisual.ViperSting:
                    AddRing(renderers, "ViperDrain", parent, 0.86f, 0.54f, 0.04f, 4, material);
                    AddPrimitive(renderers, "ViperDropHigh", PrimitiveType.Sphere, parent,
                        new Vector3(0f, 1.16f, 0.5f), Quaternion.identity,
                        Vector3.one * 0.12f, material);
                    AddPrimitive(renderers, "ViperDropLow", PrimitiveType.Sphere, parent,
                        new Vector3(0f, 0.62f, 0.5f), Quaternion.identity,
                        Vector3.one * 0.08f, material);
                    break;
                case PersistentAuraVisual.ScorpidSting:
                    AddRing(renderers, "ScorpidLock", parent, 0.7f, 0.58f, 0.04f, 5, material);
                    AddPrimitive(renderers, "ScorpidClawLeft", PrimitiveType.Cube, parent,
                        new Vector3(-0.28f, 0.74f, 0.48f), Quaternion.Euler(0f, 0f, -38f),
                        new Vector3(0.1f, 0.28f, 0.065f), material);
                    AddPrimitive(renderers, "ScorpidClawRight", PrimitiveType.Cube, parent,
                        new Vector3(0.28f, 0.74f, 0.48f), Quaternion.Euler(0f, 0f, 38f),
                        new Vector3(0.1f, 0.28f, 0.065f), material);
                    break;
                case PersistentAuraVisual.HuntersMark:
                    AddRing(renderers, "HunterReticleOuter", parent, 0.055f, 0.76f, 0.048f, 40, material);
                    AddRing(renderers, "HunterReticleInner", parent, 0.057f, 0.4f, 0.035f, 4, material);
                    AddPrimitive(renderers, "HunterReticleNeedle", PrimitiveType.Cube, parent,
                        new Vector3(0f, 0.06f, 0.72f), Quaternion.Euler(0f, 45f, 0f),
                        new Vector3(0.12f, 0.025f, 0.12f), material);
                    break;
                case PersistentAuraVisual.TrapRoot:
                    AddRing(renderers, "TrapRootSeal", parent, 0.06f, 0.68f, 0.05f, 8, material);
                    AddPrimitive(renderers, "TrapChainA", PrimitiveType.Cube, parent,
                        new Vector3(0f, 0.075f, 0f), Quaternion.Euler(0f, 45f, 0f),
                        new Vector3(0.07f, 0.025f, 1.02f), material);
                    AddPrimitive(renderers, "TrapChainB", PrimitiveType.Cube, parent,
                        new Vector3(0f, 0.078f, 0f), Quaternion.Euler(0f, -45f, 0f),
                        new Vector3(0.07f, 0.025f, 1.02f), material);
                    AddPrimitive(renderers, "TrapLock", PrimitiveType.Cube, parent,
                        new Vector3(0f, 0.1f, 0f), Quaternion.Euler(0f, 45f, 0f),
                        new Vector3(0.18f, 0.04f, 0.18f), material);
                    break;
                case PersistentAuraVisual.Deterrence:
                    AddRing(renderers, "DeterrenceOrbit", parent, 1.02f, 0.84f, 0.045f, 6, material);
                    for (int index = 0; index < 3; index++)
                    {
                        float angle = index * 120f;
                        Vector3 position = Quaternion.Euler(0f, angle, 0f) *
                            new Vector3(0f, 1.08f, 0.78f);
                        AddPrimitive(renderers, "DeterrenceBlade_" + index, PrimitiveType.Capsule, parent,
                            position, Quaternion.Euler(0f, angle, 0f),
                            new Vector3(0.085f, 0.54f, 0.085f), material);
                    }
                    break;
            }
        }

        private static string GetPersistentMotifName(PersistentAuraVisual motif)
        {
            return motif + "Persistent";
        }

        private static void AddRing(
            List<Renderer> renderers,
            string name,
            Transform parent,
            float height,
            float radius,
            float width,
            int corners,
            Material material)
        {
            renderers.Add(CreateRing(
                name,
                parent,
                new Vector3(0f, height, 0f),
                radius,
                width,
                material,
                corners));
        }

        private static void AddPrimitive(
            List<Renderer> renderers,
            string name,
            PrimitiveType type,
            Transform parent,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            Material material)
        {
            GameObject value = CreatePrimitive(
                name,
                type,
                parent,
                position,
                rotation,
                scale,
                material);
            renderers.Add(value.GetComponent<Renderer>());
        }

        private static GameObject CreateRoot(string name, Transform parent)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            return root;
        }

        private static GameObject CreatePrimitive(
            string name,
            PrimitiveType type,
            Transform parent,
            Vector3 localPosition,
            Quaternion localRotation,
            Vector3 localScale,
            Material material)
        {
            GameObject visual = GameObject.CreatePrimitive(type);
            visual.name = name;
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = localPosition;
            visual.transform.localRotation = localRotation;
            visual.transform.localScale = localScale;
            Collider collider = visual.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            ConfigureRenderer(visual.GetComponent<Renderer>(), material);
            return visual;
        }

        private static LineRenderer CreateRing(
            string name,
            Transform parent,
            Vector3 localPosition,
            float radius,
            float width,
            Material material,
            int corners)
        {
            GameObject root = new GameObject(name, typeof(LineRenderer));
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPosition;
            LineRenderer line = root.GetComponent<LineRenderer>();
            line.alignment = LineAlignment.TransformZ;
            line.loop = true;
            line.useWorldSpace = false;
            line.positionCount = corners <= 4 ? corners : RingPointCount;
            line.widthMultiplier = width;
            line.numCapVertices = 2;
            line.numCornerVertices = 2;
            ConfigureRenderer(line, material);

            int pointCount = line.positionCount;
            for (int index = 0; index < pointCount; index++)
            {
                float angle = (index / (float)pointCount) * Mathf.PI * 2f;
                line.SetPosition(index, new Vector3(
                    Mathf.Sin(angle) * radius,
                    0f,
                    Mathf.Cos(angle) * radius));
            }

            return line;
        }

        private static void ConfigureRenderer(Renderer renderer, Material material)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        }

        private static void SetObject(
            SerializedObject serialized,
            string name,
            UnityEngine.Object value)
        {
            RequireProperty(serialized, name).objectReferenceValue = value;
        }

        private static void SetArray<T>(SerializedObject serialized, string name, T[] values)
            where T : UnityEngine.Object
        {
            SerializedProperty property = RequireProperty(serialized, name);
            property.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            }
        }

        private static void SetIntArray(
            SerializedObject serialized,
            string name,
            int[] values)
        {
            SerializedProperty property = RequireProperty(serialized, name);
            property.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++)
            {
                property.GetArrayElementAtIndex(index).intValue = values[index];
            }
        }

        private static SerializedProperty RequireProperty(
            SerializedObject serialized,
            string name)
        {
            SerializedProperty property = serialized.FindProperty(name);
            if (property == null)
            {
                throw new InvalidOperationException(
                    "Aura visual authoring contract is missing '" + name + "'.");
            }

            return property;
        }
    }
}
