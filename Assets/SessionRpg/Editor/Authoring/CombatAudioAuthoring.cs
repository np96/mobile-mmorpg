using System;
using System.IO;
using SessionRpg.Unity.Presentation;
using UnityEditor;
using UnityEngine;

namespace SessionRpg.Editor.Authoring
{
    internal static class CombatAudioAuthoring
    {
        public const int WorldSourcePoolSize = 10;
        public const int UiSourcePoolSize = 3;

        private const int SampleRate = 22050;
        private const string AudioFolder = "Assets/SessionRpg/Art/Audio";
        public const string PalettePath = AudioFolder + "/CombatSfxPalette.asset";

        private static readonly CueDefinition[] Definitions =
        {
            new CueDefinition(CombatSfxCue.CastStart, "_castStart", "CastStart", 0.24f),
            new CueDefinition(CombatSfxCue.CastReleaseMagic, "_castReleaseMagic", "CastReleaseMagic", 0.19f),
            new CueDefinition(CombatSfxCue.CastReleasePhysical, "_castReleasePhysical", "CastReleasePhysical", 0.16f),
            new CueDefinition(CombatSfxCue.ImpactMagic, "_impactMagic", "ImpactMagic", 0.17f),
            new CueDefinition(CombatSfxCue.ImpactPhysical, "_impactPhysical", "ImpactPhysical", 0.14f),
            new CueDefinition(CombatSfxCue.Hazard, "_hazard", "Hazard", 0.22f),
            new CueDefinition(CombatSfxCue.Heal, "_heal", "Heal", 0.3f),
            new CueDefinition(CombatSfxCue.Barrier, "_barrier", "Barrier", 0.24f),
            new CueDefinition(CombatSfxCue.Interrupt, "_interrupt", "Interrupt", 0.16f),
            new CueDefinition(CombatSfxCue.Control, "_control", "Control", 0.25f),
            new CueDefinition(CombatSfxCue.Knockback, "_knockback", "Knockback", 0.28f),
            new CueDefinition(CombatSfxCue.Death, "_death", "Death", 0.35f),
            new CueDefinition(CombatSfxCue.ResultVictory, "_resultVictory", "ResultVictory", 0.65f),
            new CueDefinition(CombatSfxCue.ResultDefeat, "_resultDefeat", "ResultDefeat", 0.62f),
            new CueDefinition(CombatSfxCue.ResultDraw, "_resultDraw", "ResultDraw", 0.45f),
            new CueDefinition(CombatSfxCue.UiSelect, "_uiSelect", "UiSelect", 0.065f),
            new CueDefinition(CombatSfxCue.UiConfirm, "_uiConfirm", "UiConfirm", 0.1f),
            new CueDefinition(CombatSfxCue.UiReject, "_uiReject", "UiReject", 0.14f),
            new CueDefinition(CombatSfxCue.TargetSelect, "_targetSelect", "TargetSelect", 0.08f),
            new CueDefinition(CombatSfxCue.Miss, "_miss", "Miss", 0.12f),
            new CueDefinition(CombatSfxCue.CastReleaseRadiant, "_castReleaseRadiant", "CastReleaseRadiant", 0.24f),
            new CueDefinition(CombatSfxCue.ImpactRadiant, "_impactRadiant", "ImpactRadiant", 0.22f),
            new CueDefinition(CombatSfxCue.CastReleaseFire, "_castReleaseFire", "CastReleaseFire", 0.2f),
            new CueDefinition(CombatSfxCue.ImpactFire, "_impactFire", "ImpactFire", 0.22f),
            new CueDefinition(CombatSfxCue.CastReleaseFrost, "_castReleaseFrost", "CastReleaseFrost", 0.21f),
            new CueDefinition(CombatSfxCue.ImpactFrost, "_impactFrost", "ImpactFrost", 0.2f),
            new CueDefinition(CombatSfxCue.CastReleaseShadow, "_castReleaseShadow", "CastReleaseShadow", 0.26f),
            new CueDefinition(CombatSfxCue.ImpactShadow, "_impactShadow", "ImpactShadow", 0.23f),
            new CueDefinition(CombatSfxCue.CastReleaseArcane, "_castReleaseArcane", "CastReleaseArcane", 0.22f),
            new CueDefinition(CombatSfxCue.ImpactArcane, "_impactArcane", "ImpactArcane", 0.2f),
            new CueDefinition(CombatSfxCue.CastReleaseVenom, "_castReleaseVenom", "CastReleaseVenom", 0.2f),
            new CueDefinition(CombatSfxCue.ImpactVenom, "_impactVenom", "ImpactVenom", 0.21f),
        };

        public static CombatSfxPalette BuildPalette()
        {
            Directory.CreateDirectory(AudioFolder);
            for (int index = 0; index < Definitions.Length; index++)
            {
                CueDefinition definition = Definitions[index];
                string assetPath = GetClipPath(definition);
                WriteIfChanged(assetPath, BuildWave(definition));
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            for (int index = 0; index < Definitions.Length; index++)
            {
                ConfigureImporter(GetClipPath(Definitions[index]));
            }

            CombatSfxPalette palette = AssetDatabase.LoadAssetAtPath<CombatSfxPalette>(PalettePath);
            if (palette == null)
            {
                palette = ScriptableObject.CreateInstance<CombatSfxPalette>();
                AssetDatabase.CreateAsset(palette, PalettePath);
            }

            SerializedObject serialized = new SerializedObject(palette);
            serialized.Update();
            for (int index = 0; index < Definitions.Length; index++)
            {
                CueDefinition definition = Definitions[index];
                SerializedProperty property = serialized.FindProperty(definition.PropertyName);
                if (property == null)
                {
                    throw new InvalidOperationException(
                        "Combat SFX palette is missing '" + definition.PropertyName + "'.");
                }

                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(
                    GetClipPath(definition));
                if (clip == null)
                {
                    throw new InvalidOperationException(
                        "Unity did not import authored combat SFX '" + definition.FileName + "'.");
                }

                property.objectReferenceValue = clip;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(palette);
            return palette;
        }

        public static CombatAudioPresenter BuildService(
            Transform parent,
            CombatSfxPalette palette)
        {
            if (palette == null)
            {
                throw new ArgumentException(
                    "An authored CombatSfxPalette is required.",
                    nameof(palette));
            }

            for (int cueValue = 1; cueValue < (int)CombatSfxCue.Count; cueValue++)
            {
                CombatSfxCue cue = (CombatSfxCue)cueValue;
                if (palette.GetClip(cue) == null)
                {
                    throw new ArgumentException(
                        "CombatSfxPalette is missing authored cue '" + cue + "'.",
                        nameof(palette));
                }
            }

            GameObject root = new GameObject("CombatAudio", typeof(CombatAudioPresenter));
            root.transform.SetParent(parent, false);
            Transform worldRoot = new GameObject("WorldSources").transform;
            worldRoot.SetParent(root.transform, false);
            Transform uiRoot = new GameObject("UiSources").transform;
            uiRoot.SetParent(root.transform, false);

            AudioSource[] worldSources = new AudioSource[WorldSourcePoolSize];
            for (int index = 0; index < worldSources.Length; index++)
            {
                worldSources[index] = CreateSource(
                    worldRoot,
                    "WorldSource_" + index,
                    0.18f);
            }

            AudioSource[] uiSources = new AudioSource[UiSourcePoolSize];
            for (int index = 0; index < uiSources.Length; index++)
            {
                uiSources[index] = CreateSource(
                    uiRoot,
                    "UiSource_" + index,
                    0f);
            }

            CombatAudioPresenter presenter = root.GetComponent<CombatAudioPresenter>();
            SerializedObject serialized = new SerializedObject(presenter);
            serialized.FindProperty("_palette").objectReferenceValue = palette;
            SetArray(serialized.FindProperty("_worldSources"), worldSources);
            SetArray(serialized.FindProperty("_uiSources"), uiSources);
            serialized.FindProperty("_masterVolume").floatValue = 0.72f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return presenter;
        }

        private static AudioSource CreateSource(
            Transform parent,
            string name,
            float spatialBlend)
        {
            GameObject root = new GameObject(name, typeof(AudioSource));
            root.transform.SetParent(parent, false);
            AudioSource source = root.GetComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.volume = 1f;
            source.pitch = 1f;
            source.priority = spatialBlend > 0f ? 128 : 96;
            source.spatialBlend = spatialBlend;
            source.dopplerLevel = 0f;
            source.spread = 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 6f;
            source.maxDistance = 32f;
            source.reverbZoneMix = 0f;
            return source;
        }

        private static void SetArray(SerializedProperty property, AudioSource[] values)
        {
            if (property == null)
            {
                throw new InvalidOperationException("Combat audio source pool contract is missing.");
            }

            property.arraySize = values.Length;
            for (int index = 0; index < values.Length; index++)
            {
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
            }
        }

        private static void ConfigureImporter(string assetPath)
        {
            AudioImporter importer = AssetImporter.GetAtPath(assetPath) as AudioImporter;
            if (importer == null)
            {
                throw new InvalidOperationException(
                    "Missing AudioImporter for authored SFX '" + assetPath + "'.");
            }

            importer.forceToMono = true;
            importer.loadInBackground = false;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            settings.loadType = AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = AudioCompressionFormat.PCM;
            settings.sampleRateSetting = AudioSampleRateSetting.OverrideSampleRate;
            settings.sampleRateOverride = SampleRate;
            settings.quality = 1f;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }

        private static string GetClipPath(CueDefinition definition)
        {
            return AudioFolder + "/" + definition.FileName + ".wav";
        }

        private static void WriteIfChanged(string assetPath, byte[] bytes)
        {
            if (File.Exists(assetPath))
            {
                byte[] existing = File.ReadAllBytes(assetPath);
                if (existing.Length == bytes.Length)
                {
                    bool identical = true;
                    for (int index = 0; index < existing.Length; index++)
                    {
                        if (existing[index] == bytes[index])
                        {
                            continue;
                        }

                        identical = false;
                        break;
                    }

                    if (identical)
                    {
                        return;
                    }
                }
            }

            File.WriteAllBytes(assetPath, bytes);
        }

        private static byte[] BuildWave(CueDefinition definition)
        {
            int sampleCount = Mathf.CeilToInt(definition.DurationSeconds * SampleRate);
            int dataByteCount = sampleCount * 2;
            byte[] bytes = new byte[44 + dataByteCount];
            WriteAscii(bytes, 0, "RIFF");
            WriteInt32(bytes, 4, 36 + dataByteCount);
            WriteAscii(bytes, 8, "WAVE");
            WriteAscii(bytes, 12, "fmt ");
            WriteInt32(bytes, 16, 16);
            WriteInt16(bytes, 20, 1);
            WriteInt16(bytes, 22, 1);
            WriteInt32(bytes, 24, SampleRate);
            WriteInt32(bytes, 28, SampleRate * 2);
            WriteInt16(bytes, 32, 2);
            WriteInt16(bytes, 34, 16);
            WriteAscii(bytes, 36, "data");
            WriteInt32(bytes, 40, dataByteCount);

            uint noiseState = 0xA341316Cu ^ ((uint)definition.Cue * 0x9E3779B9u);
            float smoothNoise = 0f;
            for (int index = 0; index < sampleCount; index++)
            {
                float time = index / (float)SampleRate;
                float normalizedTime = time / definition.DurationSeconds;
                float sample = Synthesize(
                    definition.Cue,
                    time,
                    normalizedTime,
                    ref noiseState,
                    ref smoothNoise);
                short pcm = (short)Mathf.RoundToInt(
                    Mathf.Clamp(sample, -0.9f, 0.9f) * short.MaxValue);
                WriteInt16(bytes, 44 + (index * 2), pcm);
            }

            return bytes;
        }

        private static float Synthesize(
            CombatSfxCue cue,
            float time,
            float normalizedTime,
            ref uint noiseState,
            ref float smoothNoise)
        {
            float envelope = Mathf.Clamp01(normalizedTime * 20f) *
                Mathf.Pow(Mathf.Clamp01(1f - normalizedTime), 1.5f);
            float noise = NextNoise(ref noiseState);
            smoothNoise = Mathf.Lerp(smoothNoise, noise, 0.08f);

            switch (cue)
            {
                case CombatSfxCue.CastStart:
                    return envelope * 0.42f *
                        (Chirp(time, 240f, 690f, normalizedTime) +
                         (0.35f * Chirp(time, 520f, 1040f, normalizedTime)));
                case CombatSfxCue.CastReleaseMagic:
                    return envelope * 0.48f *
                        (Chirp(time, 1120f, 420f, normalizedTime) +
                         (noise * 0.16f));
                case CombatSfxCue.CastReleasePhysical:
                    return envelope *
                        ((0.34f * Sine(time, 145f - (normalizedTime * 55f))) +
                         (smoothNoise * 0.42f));
                case CombatSfxCue.ImpactMagic:
                    return envelope *
                        ((0.34f * Sine(time, 155f)) +
                         (0.28f * Chirp(time, 1500f, 380f, normalizedTime)) +
                         (noise * 0.18f));
                case CombatSfxCue.ImpactPhysical:
                    return envelope *
                        ((0.52f * Sine(time, 92f - (normalizedTime * 28f))) +
                         (smoothNoise * 0.34f));
                case CombatSfxCue.Hazard:
                    return envelope *
                        ((0.28f * Sine(time, 58f)) +
                         (0.18f * Sine(time, 117f)) +
                         (smoothNoise * 0.3f));
                case CombatSfxCue.Heal:
                    return envelope * 0.34f *
                        (Sine(time, 523.25f) +
                         (0.75f * Sine(time, 659.25f)) +
                         (0.5f * Sine(time, 783.99f)));
                case CombatSfxCue.Barrier:
                    return envelope * 0.4f *
                        (Sine(time, 987.77f) +
                         (0.62f * Sine(time, 659.25f)) +
                         (0.22f * Sine(time, 1318.51f)));
                case CombatSfxCue.Interrupt:
                    return envelope *
                        ((0.42f * Chirp(time, 1850f, 260f, normalizedTime)) +
                         (noise * 0.32f));
                case CombatSfxCue.Control:
                    return envelope * 0.42f *
                        (Sine(time, 205f + (Mathf.Sin(time * 80f) * 44f)) +
                         (0.45f * Sine(time, 307f)));
                case CombatSfxCue.Knockback:
                    return envelope *
                        ((smoothNoise * 0.48f) +
                         (0.3f * Chirp(time, 260f, 72f, normalizedTime)));
                case CombatSfxCue.Death:
                    return envelope *
                        ((0.48f * Chirp(time, 245f, 48f, normalizedTime)) +
                         (smoothNoise * 0.22f));
                case CombatSfxCue.ResultVictory:
                    return 0.38f *
                        (Note(time, 0f, 0.25f, 523.25f) +
                         Note(time, 0.17f, 0.26f, 659.25f) +
                         Note(time, 0.34f, 0.31f, 783.99f));
                case CombatSfxCue.ResultDefeat:
                    return 0.38f *
                        (Note(time, 0f, 0.26f, 329.63f) +
                         Note(time, 0.18f, 0.27f, 246.94f) +
                         Note(time, 0.36f, 0.26f, 164.81f));
                case CombatSfxCue.ResultDraw:
                    return 0.3f *
                        (Note(time, 0f, 0.24f, 392f) +
                         Note(time, 0.21f, 0.24f, 349.23f));
                case CombatSfxCue.UiSelect:
                    return envelope * 0.36f * Sine(time, 820f);
                case CombatSfxCue.UiConfirm:
                    return 0.32f *
                        (Note(time, 0f, 0.065f, 720f) +
                         Note(time, 0.045f, 0.055f, 960f));
                case CombatSfxCue.UiReject:
                    return envelope * 0.32f *
                        (Sine(time, 175f) + (0.45f * Sine(time, 193f)));
                case CombatSfxCue.TargetSelect:
                    return envelope * 0.3f *
                        (Sine(time, 1040f) + (0.35f * Sine(time, 1560f)));
                case CombatSfxCue.Miss:
                    return envelope *
                        ((smoothNoise * 0.28f) +
                         (0.22f * Chirp(time, 520f, 210f, normalizedTime)));
                case CombatSfxCue.CastReleaseRadiant:
                    return envelope * 0.31f *
                        (Sine(time, 659.25f) +
                         (0.72f * Sine(time, 987.77f)) +
                         (0.34f * Chirp(time, 420f, 1320f, normalizedTime)));
                case CombatSfxCue.ImpactRadiant:
                    return envelope * 0.34f *
                        (Sine(time, 783.99f) +
                         (0.58f * Sine(time, 1174.66f)) +
                         (0.22f * Sine(time, 1567.98f)));
                case CombatSfxCue.CastReleaseFire:
                    return envelope *
                        ((0.28f * Chirp(time, 940f, 210f, normalizedTime)) +
                         (0.27f * smoothNoise) +
                         (0.16f * Sine(time, 78f)));
                case CombatSfxCue.ImpactFire:
                    return envelope *
                        ((0.4f * Sine(time, 72f - (normalizedTime * 24f))) +
                         (0.28f * Chirp(time, 1250f, 150f, normalizedTime)) +
                         (noise * 0.24f));
                case CombatSfxCue.CastReleaseFrost:
                    return envelope * 0.34f *
                        (Sine(time, 1318.51f) +
                         (0.54f * Sine(time, 1760f)) +
                         (0.28f * Chirp(time, 720f, 1640f, normalizedTime)));
                case CombatSfxCue.ImpactFrost:
                    return envelope *
                        ((0.31f * Sine(time, 1567.98f)) +
                         (0.24f * Chirp(time, 2100f, 540f, normalizedTime)) +
                         (noise * 0.2f));
                case CombatSfxCue.CastReleaseShadow:
                    return envelope *
                        ((0.34f * Sine(time, 82f + Mathf.Sin(time * 41f) * 13f)) +
                         (0.24f * Sine(time, 123f)) +
                         (smoothNoise * 0.12f));
                case CombatSfxCue.ImpactShadow:
                    return envelope *
                        ((0.36f * Chirp(time, 430f, 62f, normalizedTime)) +
                         (0.26f * Sine(time, 96f)) +
                         (smoothNoise * 0.18f));
                case CombatSfxCue.CastReleaseArcane:
                    return envelope * 0.3f *
                        (Sine(time, normalizedTime < 0.5f ? 622.25f : 932.33f) +
                         (0.62f * Sine(time, normalizedTime < 0.68f ? 1244.51f : 1396.91f)) +
                         (0.2f * Chirp(time, 360f, 1440f, normalizedTime)));
                case CombatSfxCue.ImpactArcane:
                    return envelope * 0.34f *
                        (Sine(time, 466.16f) +
                         (0.64f * Sine(time, 932.33f)) +
                         (0.3f * Chirp(time, 1680f, 420f, normalizedTime)));
                case CombatSfxCue.CastReleaseVenom:
                    return envelope *
                        ((smoothNoise * 0.28f) +
                         (0.22f * Sine(time, 186f + Mathf.Sin(time * 63f) * 28f)) +
                         (0.14f * Chirp(time, 620f, 290f, normalizedTime)));
                case CombatSfxCue.ImpactVenom:
                    return envelope *
                        ((smoothNoise * 0.34f) +
                         (0.28f * Sine(time, 112f - (normalizedTime * 31f))) +
                         (0.13f * Sine(time, 335f)));
                default:
                    return 0f;
            }
        }

        private static float Note(float time, float start, float duration, float frequency)
        {
            float localTime = time - start;
            if (localTime < 0f || localTime >= duration)
            {
                return 0f;
            }

            float normalized = localTime / duration;
            float envelope = Mathf.Clamp01(normalized * 18f) *
                Mathf.Pow(1f - normalized, 1.2f);
            return Sine(localTime, frequency) * envelope;
        }

        private static float Sine(float time, float frequency)
        {
            return Mathf.Sin(time * frequency * Mathf.PI * 2f);
        }

        private static float Chirp(
            float time,
            float startFrequency,
            float endFrequency,
            float normalizedTime)
        {
            float frequency = Mathf.Lerp(startFrequency, endFrequency, normalizedTime);
            return Sine(time, frequency);
        }

        private static float NextNoise(ref uint state)
        {
            state = (state * 1664525u) + 1013904223u;
            return ((state >> 8) / 8388607.5f) - 1f;
        }

        private static void WriteAscii(byte[] bytes, int offset, string value)
        {
            for (int index = 0; index < value.Length; index++)
            {
                bytes[offset + index] = (byte)value[index];
            }
        }

        private static void WriteInt16(byte[] bytes, int offset, int value)
        {
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
        }

        private static void WriteInt32(byte[] bytes, int offset, int value)
        {
            bytes[offset] = (byte)value;
            bytes[offset + 1] = (byte)(value >> 8);
            bytes[offset + 2] = (byte)(value >> 16);
            bytes[offset + 3] = (byte)(value >> 24);
        }

        private readonly struct CueDefinition
        {
            public CueDefinition(
                CombatSfxCue cue,
                string propertyName,
                string fileName,
                float durationSeconds)
            {
                Cue = cue;
                PropertyName = propertyName;
                FileName = fileName;
                DurationSeconds = durationSeconds;
            }

            public CombatSfxCue Cue { get; }

            public string PropertyName { get; }

            public string FileName { get; }

            public float DurationSeconds { get; }
        }
    }
}
