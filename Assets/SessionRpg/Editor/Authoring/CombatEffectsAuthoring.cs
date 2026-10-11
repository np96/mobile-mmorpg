using System;
using SessionRpg.Unity.Presentation;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SessionRpg.Editor.Authoring
{
    internal static class CombatEffectsAuthoring
    {
        public const int EffectPoolSize = 24;
        public const int GroundEffectPoolSize = 8;
        public const int GroundEffectRenderersPerView = 8;
        private const int TextPoolSize = 16;
        private const int MotifCount = 12;
        private const int HaloCount = 3;
        private const int TrailCount = 2;
        private const int HaloPointCount = 33;
        private const int TrailPointCount = 8;
        private const string VfxMeshFolder = AuthoringAssets.ArtFolder + "/Vfx";

        public static CombatEffectsPresenter Build(
            Material effectMaterial,
            Material fragmentMaterial,
            Material ribbonMaterial,
            Material particleMaterial,
            Font font,
            CombatSfxPalette audioPalette)
        {
            if (effectMaterial == null)
            {
                throw new ArgumentNullException(nameof(effectMaterial));
            }

            if (font == null)
            {
                throw new ArgumentNullException(nameof(font));
            }

            if (fragmentMaterial == null)
            {
                throw new ArgumentNullException(nameof(fragmentMaterial));
            }

            if (ribbonMaterial == null)
            {
                throw new ArgumentNullException(nameof(ribbonMaterial));
            }

            if (particleMaterial == null)
            {
                throw new ArgumentNullException(nameof(particleMaterial));
            }

            GameObject root = new GameObject("CombatEffects", typeof(CombatEffectsPresenter));
            CombatAudioPresenter audio = CombatAudioAuthoring.BuildService(
                root.transform,
                audioPalette);
            GameObject effectsRoot = new GameObject("EffectPool");
            effectsRoot.transform.SetParent(root.transform, false);
            GameObject textRoot = new GameObject("CombatTextPool");
            textRoot.transform.SetParent(root.transform, false);
            GameObject groundEffectRoot = new GameObject("GroundEffectPool");
            groundEffectRoot.transform.SetParent(root.transform, false);

            Mesh cometMesh = EnsureCometMesh();
            Mesh crystalMesh = EnsureCrystalMesh();

            CombatEffectView[] effects = new CombatEffectView[EffectPoolSize];
            for (int index = 0; index < effects.Length; index++)
            {
                effects[index] = CreateEffectView(
                    effectsRoot.transform,
                    effectMaterial,
                    fragmentMaterial,
                    ribbonMaterial,
                    particleMaterial,
                    cometMesh,
                    crystalMesh,
                    index);
            }

            CombatTextView[] texts = new CombatTextView[TextPoolSize];
            for (int index = 0; index < texts.Length; index++)
            {
                texts[index] = CreateTextView(textRoot.transform, font, index);
            }

            BuildGroundEffectPool(
                groundEffectRoot.transform,
                ribbonMaterial,
                effectMaterial,
                out GameObject[] groundEffectRoots,
                out GameObject[] freezingTrapRoots,
                out GameObject[] frostTrapRoots,
                out Renderer[] groundEffectRenderers);

            CombatEffectsPresenter presenter = root.GetComponent<CombatEffectsPresenter>();
            SerializedObject serialized = new SerializedObject(presenter);
            serialized.Update();
            SetObjectReference(serialized, "_gameCamera", Camera.main);
            SetObjectReference(serialized, "_audio", audio);
            SetArray(serialized, "_effectPool", effects);
            SetArray(serialized, "_textPool", texts);
            SetArray(serialized, "_groundEffectRoots", groundEffectRoots);
            SetArray(serialized, "_freezingTrapRoots", freezingTrapRoots);
            SetArray(serialized, "_frostTrapRoots", frostTrapRoots);
            SetArray(serialized, "_groundEffectRenderers", groundEffectRenderers);
            RequireProperty(serialized, "_groundEffectRenderersPerView").intValue =
                GroundEffectRenderersPerView;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return presenter;
        }

        private static void BuildGroundEffectPool(
            Transform parent,
            Material ribbonMaterial,
            Material effectMaterial,
            out GameObject[] roots,
            out GameObject[] freezingRoots,
            out GameObject[] frostRoots,
            out Renderer[] renderers)
        {
            roots = new GameObject[GroundEffectPoolSize];
            freezingRoots = new GameObject[GroundEffectPoolSize];
            frostRoots = new GameObject[GroundEffectPoolSize];
            renderers = new Renderer[GroundEffectPoolSize * GroundEffectRenderersPerView];

            for (int index = 0; index < GroundEffectPoolSize; index++)
            {
                GameObject root = new GameObject("GroundState_" + index);
                root.transform.SetParent(parent, false);
                roots[index] = root;
                int rendererIndex = index * GroundEffectRenderersPerView;
                renderers[rendererIndex++] = CreateGroundRing(
                    "Boundary",
                    root.transform,
                    ribbonMaterial,
                    1f,
                    0.035f,
                    32);

                GameObject freezing = new GameObject("FreezingTrapJaws");
                freezing.transform.SetParent(root.transform, false);
                freezingRoots[index] = freezing;
                renderers[rendererIndex++] = CreateGroundBar(
                    "JawLeft",
                    freezing.transform,
                    effectMaterial,
                    new Vector3(-0.2f, 0.045f, 0f),
                    Quaternion.Euler(0f, -34f, 0f),
                    new Vector3(0.12f, 0.045f, 0.66f));
                renderers[rendererIndex++] = CreateGroundBar(
                    "JawRight",
                    freezing.transform,
                    effectMaterial,
                    new Vector3(0.2f, 0.048f, 0f),
                    Quaternion.Euler(0f, 34f, 0f),
                    new Vector3(0.12f, 0.045f, 0.66f));
                renderers[rendererIndex++] = CreateGroundBar(
                    "TrapLock",
                    freezing.transform,
                    effectMaterial,
                    new Vector3(0f, 0.058f, 0f),
                    Quaternion.Euler(0f, 45f, 0f),
                    new Vector3(0.19f, 0.055f, 0.19f));

                GameObject frost = new GameObject("FrostTrapRunes");
                frost.transform.SetParent(root.transform, false);
                frostRoots[index] = frost;
                renderers[rendererIndex++] = CreateGroundRing(
                    "InnerRune",
                    frost.transform,
                    ribbonMaterial,
                    0.56f,
                    0.026f,
                    12);
                for (int spoke = 0; spoke < 3; spoke++)
                {
                    renderers[rendererIndex++] = CreateGroundBar(
                        "FrostSpoke_" + spoke,
                        frost.transform,
                        effectMaterial,
                        new Vector3(0f, 0.04f, 0f),
                        Quaternion.Euler(0f, spoke * 60f, 0f),
                        new Vector3(0.055f, 0.035f, 1.3f));
                }

                freezing.SetActive(false);
                frost.SetActive(false);
                root.SetActive(false);
            }
        }

        private static LineRenderer CreateGroundRing(
            string name,
            Transform parent,
            Material material,
            float radius,
            float width,
            int pointCount)
        {
            GameObject root = new GameObject(name, typeof(LineRenderer));
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0f, 0.035f, 0f);
            LineRenderer line = ConfigureLineRenderer(root, material, pointCount, false);
            line.alignment = LineAlignment.TransformZ;
            line.loop = true;
            line.widthMultiplier = width;
            for (int pointIndex = 0; pointIndex < pointCount; pointIndex++)
            {
                float angle = pointIndex / (float)pointCount * Mathf.PI * 2f;
                line.SetPosition(
                    pointIndex,
                    new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }

            return line;
        }

        private static Renderer CreateGroundBar(
            string name,
            Transform parent,
            Material material,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale)
        {
            Transform value = CreatePrimitiveVisual(
                name,
                PrimitiveType.Cube,
                parent,
                material,
                position);
            value.localRotation = rotation;
            value.localScale = scale;
            return value.GetComponent<Renderer>();
        }

        private static CombatEffectView CreateEffectView(
            Transform parent,
            Material material,
            Material fragmentMaterial,
            Material ribbonMaterial,
            Material particleMaterial,
            Mesh cometMesh,
            Mesh crystalMesh,
            int index)
        {
            GameObject root = new GameObject("Effect_" + index, typeof(CombatEffectView));
            root.transform.SetParent(parent, false);

            Transform core = CreatePrimitiveVisual(
                "LuminousCore",
                PrimitiveType.Sphere,
                root.transform,
                material,
                Vector3.zero);
            Transform shell = CreatePrimitiveVisual(
                "EnergyShell",
                PrimitiveType.Sphere,
                root.transform,
                material,
                Vector3.zero);
            Transform disc = CreatePrimitiveVisual(
                "GroundSigil",
                PrimitiveType.Cylinder,
                root.transform,
                material,
                Vector3.zero);

            Transform[] motifs = new Transform[MotifCount];
            Renderer[] motifRenderers = new Renderer[MotifCount];
            MeshFilter[] motifMeshFilters = new MeshFilter[MotifCount];
            Mesh[] motifBaseMeshes = new Mesh[MotifCount];
            for (int motifIndex = 0; motifIndex < motifs.Length; motifIndex++)
            {
                PrimitiveType shape = motifIndex % 3 == 0
                    ? PrimitiveType.Capsule
                    : PrimitiveType.Cube;
                motifs[motifIndex] = CreatePrimitiveVisual(
                    "Motif_" + motifIndex,
                    shape,
                    root.transform,
                    fragmentMaterial,
                    Vector3.zero);
                motifRenderers[motifIndex] = motifs[motifIndex].GetComponent<Renderer>();
                motifMeshFilters[motifIndex] = motifs[motifIndex].GetComponent<MeshFilter>();
                motifBaseMeshes[motifIndex] = motifMeshFilters[motifIndex].sharedMesh;
            }

            LineRenderer[] halos = new LineRenderer[HaloCount];
            for (int haloIndex = 0; haloIndex < halos.Length; haloIndex++)
            {
                halos[haloIndex] = CreateHalo(
                    root.transform,
                    ribbonMaterial,
                    haloIndex);
            }

            LineRenderer[] trails = new LineRenderer[TrailCount];
            for (int trailIndex = 0; trailIndex < trails.Length; trailIndex++)
            {
                trails[trailIndex] = CreateTrail(
                    root.transform,
                    ribbonMaterial,
                    trailIndex);
            }


            ParticleSystem particles = CreateParticleEmitter(
                root.transform,
                particleMaterial);
            ParticleSystem groundParticles = CreateGroundParticleEmitter(
                root.transform,
                particleMaterial);
            Light pulseLight = CreatePulseLight(root.transform);

            CombatEffectView view = root.GetComponent<CombatEffectView>();
            SerializedObject serialized = new SerializedObject(view);
            serialized.Update();
            SetObjectReference(serialized, "_core", core);
            SetObjectReference(serialized, "_shell", shell);
            SetObjectReference(serialized, "_disc", disc);
            SetArray(serialized, "_motifs", motifs);
            SetArray(serialized, "_halos", halos);
            SetArray(serialized, "_trails", trails);
            SetObjectReference(serialized, "_particles", particles);
            SetObjectReference(
                serialized,
                "_particleRenderer",
                particles.GetComponent<ParticleSystemRenderer>());
            SetObjectReference(serialized, "_groundParticles", groundParticles);
            SetObjectReference(
                serialized,
                "_groundParticleRenderer",
                groundParticles.GetComponent<ParticleSystemRenderer>());
            SetObjectReference(serialized, "_pulseLight", pulseLight);
            SetObjectReference(serialized, "_coreRenderer", core.GetComponent<Renderer>());
            SetObjectReference(serialized, "_shellRenderer", shell.GetComponent<Renderer>());
            SetObjectReference(serialized, "_discRenderer", disc.GetComponent<Renderer>());
            SetArray(serialized, "_motifRenderers", motifRenderers);
            MeshFilter coreMeshFilter = core.GetComponent<MeshFilter>();
            MeshFilter shellMeshFilter = shell.GetComponent<MeshFilter>();
            SetObjectReference(serialized, "_coreMeshFilter", coreMeshFilter);
            SetObjectReference(serialized, "_shellMeshFilter", shellMeshFilter);
            SetArray(serialized, "_motifMeshFilters", motifMeshFilters);
            SetArray(serialized, "_motifBaseMeshes", motifBaseMeshes);
            SetObjectReference(serialized, "_roundedMesh", coreMeshFilter.sharedMesh);
            SetObjectReference(serialized, "_cometMesh", cometMesh);
            SetObjectReference(serialized, "_crystalMesh", crystalMesh);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            root.SetActive(false);
            return view;
        }

        private static Mesh EnsureCometMesh()
        {
            EnsureVfxMeshFolder();
            const string path = VfxMeshFolder + "/SpellComet.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            Mesh authored = CreateCometMesh();
            authored.name = "SpellComet";
            if (mesh == null)
            {
                mesh = authored;
                AssetDatabase.CreateAsset(mesh, path);
            }
            else
            {
                EditorUtility.CopySerialized(authored, mesh);
                UnityEngine.Object.DestroyImmediate(authored);
                EditorUtility.SetDirty(mesh);
            }

            return mesh;
        }

        private static Mesh EnsureCrystalMesh()
        {
            EnsureVfxMeshFolder();
            const string path = VfxMeshFolder + "/SpellCrystal.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            Mesh authored = CreateCrystalMesh();
            authored.name = "SpellCrystal";
            if (mesh == null)
            {
                mesh = authored;
                AssetDatabase.CreateAsset(mesh, path);
            }
            else
            {
                EditorUtility.CopySerialized(authored, mesh);
                UnityEngine.Object.DestroyImmediate(authored);
                EditorUtility.SetDirty(mesh);
            }

            return mesh;
        }

        private static void EnsureVfxMeshFolder()
        {
            if (!AssetDatabase.IsValidFolder(VfxMeshFolder))
            {
                AssetDatabase.CreateFolder(AuthoringAssets.ArtFolder, "Vfx");
            }
        }

        private static Mesh CreateCometMesh()
        {
            const int sideCount = 12;
            float[] ringDepths = { 0.48f, 0f, -0.56f };
            float[] ringRadii = { 0.28f, 0.46f, 0.22f };
            int ringVertexCount = sideCount * ringDepths.Length;
            Vector3[] vertices = new Vector3[ringVertexCount + 2];
            vertices[0] = new Vector3(0f, 0f, 1f);
            for (int ringIndex = 0; ringIndex < ringDepths.Length; ringIndex++)
            {
                int start = 1 + (ringIndex * sideCount);
                for (int sideIndex = 0; sideIndex < sideCount; sideIndex++)
                {
                    float angle = sideIndex / (float)sideCount * Mathf.PI * 2f;
                    vertices[start + sideIndex] = new Vector3(
                        Mathf.Cos(angle) * ringRadii[ringIndex],
                        Mathf.Sin(angle) * ringRadii[ringIndex],
                        ringDepths[ringIndex]);
                }
            }

            int rearTip = vertices.Length - 1;
            vertices[rearTip] = new Vector3(0f, 0f, -1.08f);
            int triangleCount = (sideCount * 2) +
                (sideCount * (ringDepths.Length - 1) * 2);
            int[] triangles = new int[triangleCount * 3];
            int triangle = 0;
            for (int sideIndex = 0; sideIndex < sideCount; sideIndex++)
            {
                int next = (sideIndex + 1) % sideCount;
                triangles[triangle++] = 0;
                triangles[triangle++] = 1 + sideIndex;
                triangles[triangle++] = 1 + next;
            }

            for (int ringIndex = 0; ringIndex < ringDepths.Length - 1; ringIndex++)
            {
                int current = 1 + (ringIndex * sideCount);
                int following = current + sideCount;
                for (int sideIndex = 0; sideIndex < sideCount; sideIndex++)
                {
                    int next = (sideIndex + 1) % sideCount;
                    triangles[triangle++] = current + sideIndex;
                    triangles[triangle++] = following + sideIndex;
                    triangles[triangle++] = current + next;
                    triangles[triangle++] = current + next;
                    triangles[triangle++] = following + sideIndex;
                    triangles[triangle++] = following + next;
                }
            }

            int lastRing = 1 + ((ringDepths.Length - 1) * sideCount);
            for (int sideIndex = 0; sideIndex < sideCount; sideIndex++)
            {
                int next = (sideIndex + 1) % sideCount;
                triangles[triangle++] = lastRing + sideIndex;
                triangles[triangle++] = rearTip;
                triangles[triangle++] = lastRing + next;
            }

            Mesh mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static Mesh CreateCrystalMesh()
        {
            Vector3[] vertices =
            {
                new Vector3(0f, 0f, 1.25f),
                new Vector3(0.42f, 0f, 0f),
                new Vector3(0f, 0.42f, 0f),
                new Vector3(-0.42f, 0f, 0f),
                new Vector3(0f, -0.42f, 0f),
                new Vector3(0f, 0f, -0.72f),
            };
            int[] triangles =
            {
                0, 1, 2,
                0, 2, 3,
                0, 3, 4,
                0, 4, 1,
                5, 2, 1,
                5, 3, 2,
                5, 4, 3,
                5, 1, 4,
            };
            Mesh mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static ParticleSystem CreateParticleEmitter(
            Transform parent,
            Material material)
        {
            GameObject root = new GameObject(
                "SpellParticles",
                typeof(ParticleSystem));
            root.transform.SetParent(parent, false);
            ParticleSystem particles = root.GetComponent<ParticleSystem>();

            ParticleSystem.MainModule main = particles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.55f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.35f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.34f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.maxParticles = 72;
            main.stopAction = ParticleSystemStopAction.None;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = true;
            emission.rateOverTime = 28f;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.07f;

            ParticleSystem.ColorOverLifetimeModule colorOverLifetime =
                particles.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient fade = new Gradient();
            fade.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(Color.white, 1f),
                },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(1f, 0.05f),
                    new GradientAlphaKey(0.8f, 0.35f),
                    new GradientAlphaKey(0.35f, 0.75f),
                    new GradientAlphaKey(0f, 1f),
                });
            colorOverLifetime.color = fade;

            ParticleSystem.SizeOverLifetimeModule sizeOverLifetime =
                particles.sizeOverLifetime;
            sizeOverLifetime.enabled = true;
            sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(
                1f,
                new AnimationCurve(
                    new Keyframe(0f, 0.35f),
                    new Keyframe(0.08f, 1f),
                    new Keyframe(0.55f, 0.8f),
                    new Keyframe(1f, 0f)));

            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.quality = ParticleSystemNoiseQuality.Low;
            noise.strength = 0.1f;
            noise.frequency = 0.72f;
            noise.scrollSpeed = 0.24f;

            ParticleSystemRenderer renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.velocityScale = 0.16f;
            renderer.lengthScale = 2.2f;
            renderer.cameraVelocityScale = 0f;
            renderer.maxParticleSize = 0.16f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return particles;
        }

        private static ParticleSystem CreateGroundParticleEmitter(
            Transform parent,
            Material material)
        {
            GameObject root = new GameObject(
                "GroundDustParticles",
                typeof(ParticleSystem));
            root.transform.SetParent(parent, false);
            ParticleSystem particles = root.GetComponent<ParticleSystem>();

            ParticleSystem.MainModule main = particles.main;
            main.playOnAwake = false;
            main.loop = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Local;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.28f, 0.62f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.08f, 0.38f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.12f);
            main.gravityModifier = 0.16f;
            main.maxParticles = 28;
            main.stopAction = ParticleSystemStopAction.None;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.5f;
            shape.radiusThickness = 0.72f;

            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.quality = ParticleSystemNoiseQuality.Low;
            noise.strength = 0.045f;
            noise.frequency = 0.55f;

            ParticleSystemRenderer renderer = root.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.1f;
            renderer.lengthScale = 1.35f;
            renderer.cameraVelocityScale = 0f;
            renderer.maxParticleSize = 0.08f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return particles;
        }

        private static Light CreatePulseLight(Transform parent)
        {
            GameObject root = new GameObject("SpellPulseLight", typeof(Light));
            root.transform.SetParent(parent, false);
            Light light = root.GetComponent<Light>();
            light.type = LightType.Point;
            light.range = 2f;
            light.intensity = 0f;
            light.shadows = LightShadows.None;
            light.renderMode = LightRenderMode.Auto;
            light.enabled = false;
            return light;
        }

        private static LineRenderer CreateHalo(
            Transform parent,
            Material material,
            int index)
        {
            GameObject root = new GameObject("RuneHalo_" + index, typeof(LineRenderer));
            root.transform.SetParent(parent, false);
            LineRenderer line = ConfigureLineRenderer(root, material, HaloPointCount, false);
            line.loop = true;
            line.widthMultiplier = 0.026f;
            for (int pointIndex = 0; pointIndex < HaloPointCount; pointIndex++)
            {
                float angle = pointIndex / (float)HaloPointCount * Mathf.PI * 2f;
                float irregularity =
                    Mathf.Sin(angle * (3 + index)) * 0.018f +
                    Mathf.Sin(angle * (7 + index * 2)) * 0.009f;
                float radius = 0.92f + irregularity;
                line.SetPosition(
                    pointIndex,
                    new Vector3(
                        Mathf.Cos(angle) * radius,
                        Mathf.Sin(angle * (2 + index)) * 0.018f,
                        Mathf.Sin(angle) * radius));
            }

            return line;
        }

        private static LineRenderer CreateTrail(
            Transform parent,
            Material material,
            int index)
        {
            GameObject root = new GameObject("SpellRibbon_" + index, typeof(LineRenderer));
            root.transform.SetParent(parent, false);
            LineRenderer line = ConfigureLineRenderer(
                root,
                material,
                TrailPointCount,
                true);
            line.widthCurve = new AnimationCurve(
                new Keyframe(0f, 1f),
                new Keyframe(0.55f, 0.72f),
                new Keyframe(1f, 0f));
            line.widthMultiplier = 0.028f;
            return line;
        }

        private static LineRenderer ConfigureLineRenderer(
            GameObject root,
            Material material,
            int pointCount,
            bool useWorldSpace)
        {
            LineRenderer line = root.GetComponent<LineRenderer>();
            line.positionCount = pointCount;
            line.useWorldSpace = useWorldSpace;
            line.widthMultiplier = 0.1f;
            line.numCapVertices = 5;
            line.numCornerVertices = 3;
            line.sharedMaterial = material;
            line.textureMode = LineTextureMode.Stretch;
            line.alignment = LineAlignment.View;
            line.shadowCastingMode = ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.lightProbeUsage = LightProbeUsage.Off;
            line.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return line;
        }

        private static CombatTextView CreateTextView(Transform parent, Font font, int index)
        {
            GameObject root = new GameObject("CombatText_" + index, typeof(CombatTextView));
            root.transform.SetParent(parent, false);
            TextMesh textMesh = root.AddComponent<TextMesh>();
            textMesh.font = font;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.alignment = TextAlignment.Center;
            textMesh.characterSize = 0.09f;
            textMesh.fontSize = 44;
            textMesh.color = Color.white;
            MeshRenderer renderer = root.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;

            CombatTextView view = root.GetComponent<CombatTextView>();
            SerializedObject serialized = new SerializedObject(view);
            serialized.Update();
            SetObjectReference(serialized, "_textMesh", textMesh);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            root.SetActive(false);
            return view;
        }

        private static Transform CreatePrimitiveVisual(
            string name,
            PrimitiveType primitiveType,
            Transform parent,
            Material material,
            Vector3 localPosition)
        {
            GameObject visual = GameObject.CreatePrimitive(primitiveType);
            visual.name = name;
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = localPosition;
            Collider collider = visual.GetComponent<Collider>();
            if (collider != null)
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            Renderer renderer = visual.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return visual.transform;
        }

        private static void SetArray<T>(
            SerializedObject serialized,
            string propertyName,
            T[] values)
            where T : UnityEngine.Object
        {
            SerializedProperty property = RequireProperty(serialized, propertyName);
            property.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            }
        }

        private static void SetObjectReference(
            SerializedObject serialized,
            string propertyName,
            UnityEngine.Object value)
        {
            RequireProperty(serialized, propertyName).objectReferenceValue = value;
        }

        private static SerializedProperty RequireProperty(
            SerializedObject serialized,
            string propertyName)
        {
            SerializedProperty property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                throw new InvalidOperationException(
                    "Combat effects authoring contract is missing '" + propertyName + "'.");
            }

            return property;
        }
    }
}
