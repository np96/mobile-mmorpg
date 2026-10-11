using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SessionRpg.Unity.Arenas;
using UnityEngine;

namespace SessionRpg.Editor.Arenas
{
    public sealed class ArenaVariantManifestException : Exception
    {
        public ArenaVariantManifestException(string message)
            : base(message)
        {
        }

        public ArenaVariantManifestException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }

    public sealed class ArenaVariantDefinition
    {
        internal ArenaVariantDefinition(
            ArenaContentId contentId,
            ArenaContentMode mode,
            string gameplayLayoutId,
            string generatorRecipe,
            string outputPrefabPath,
            int presentationVersion,
            bool isDefaultForMode,
            string artStatus,
            string brief)
        {
            ContentId = contentId;
            Mode = mode;
            GameplayLayoutId = gameplayLayoutId;
            GeneratorRecipe = generatorRecipe;
            OutputPrefabPath = outputPrefabPath;
            PresentationVersion = presentationVersion;
            IsDefaultForMode = isDefaultForMode;
            ArtStatus = artStatus;
            Brief = brief;
        }

        public ArenaContentId ContentId { get; }

        public ArenaContentMode Mode { get; }

        public string GameplayLayoutId { get; }

        public string GeneratorRecipe { get; }

        public string OutputPrefabPath { get; }

        public int PresentationVersion { get; }

        public bool IsDefaultForMode { get; }

        public string ArtStatus { get; }

        public string Brief { get; }
    }

    /// <summary>
    /// Strict Editor-only input for deterministic arena presentation generation.
    /// This parser validates identity and file-shape contracts. The generator owns
    /// the supported gameplay-layout and recipe registries.
    /// </summary>
    public sealed class ArenaVariantManifest
    {
        public const int CurrentSchemaVersion = 1;
        public const string DefaultProjectRelativePath =
            "Tools/Agent/arena-variants.json";

        private const string ArenaContentRoot =
            "Assets/SessionRpg/Content/Arenas/";

        private static readonly string[] RootPropertyNames =
        {
            "schemaVersion",
            "entries",
        };

        private static readonly string[] EntryPropertyNames =
        {
            "contentId",
            "mode",
            "gameplayLayoutId",
            "generatorRecipe",
            "outputPrefabPath",
            "presentationVersion",
            "defaultForMode",
            "artStatus",
            "brief",
        };

        private readonly ArenaVariantDefinition[] _entries;

        private ArenaVariantManifest(ArenaVariantDefinition[] entries)
        {
            _entries = entries;
        }

        public int SchemaVersion => CurrentSchemaVersion;

        public int Count => _entries.Length;

        public ArenaVariantDefinition GetEntry(int index)
        {
            if (index < 0 || index >= _entries.Length)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _entries[index];
        }

        public bool TryGet(
            ArenaContentId contentId,
            out ArenaVariantDefinition definition)
        {
            for (int index = 0; index < _entries.Length; index++)
            {
                ArenaVariantDefinition candidate = _entries[index];
                if (candidate.ContentId == contentId)
                {
                    definition = candidate;
                    return true;
                }
            }

            definition = null;
            return false;
        }

        public bool TryGetDefault(
            ArenaContentMode mode,
            out ArenaVariantDefinition definition)
        {
            for (int index = 0; index < _entries.Length; index++)
            {
                ArenaVariantDefinition candidate = _entries[index];
                if (candidate.Mode == mode && candidate.IsDefaultForMode)
                {
                    definition = candidate;
                    return true;
                }
            }

            definition = null;
            return false;
        }

        public static ArenaVariantManifest LoadDefault()
        {
            return LoadProjectRelative(DefaultProjectRelativePath);
        }

        public static ArenaVariantManifest LoadProjectRelative(
            string projectRelativePath)
        {
            string absolutePath = ResolveProjectRelativePath(projectRelativePath);
            string json;
            try
            {
                json = File.ReadAllText(absolutePath, Encoding.UTF8);
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException)
            {
                throw new ArenaVariantManifestException(
                    "Could not read arena variant manifest '" + projectRelativePath +
                    "' at '" + absolutePath + "'. " + exception.Message,
                    exception);
            }

            return Parse(json, projectRelativePath);
        }

        public static string ResolveProjectRelativePath(string projectRelativePath)
        {
            if (string.IsNullOrWhiteSpace(projectRelativePath))
            {
                throw new ArenaVariantManifestException(
                    "Arena variant manifest path is empty. Expected a project-relative " +
                    "JSON path such as '" + DefaultProjectRelativePath + "'.");
            }

            if (!string.Equals(
                    projectRelativePath,
                    projectRelativePath.Trim(),
                    StringComparison.Ordinal) ||
                projectRelativePath.IndexOf('\\') >= 0 ||
                Path.IsPathRooted(projectRelativePath) ||
                !projectRelativePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArenaVariantManifestException(
                    "Arena variant manifest path '" + projectRelativePath +
                    "' must be a normalized project-relative .json path using '/'.");
            }

            ValidatePathSegments(
                projectRelativePath,
                "Arena variant manifest path '" + projectRelativePath + "'");

            string projectRoot = Path.GetFullPath(
                Path.Combine(UnityEngine.Application.dataPath, ".."));
            string absolutePath = Path.GetFullPath(
                Path.Combine(
                    projectRoot,
                    projectRelativePath.Replace('/', Path.DirectorySeparatorChar)));
            string rootPrefix = projectRoot.TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar) +
                Path.DirectorySeparatorChar;
            if (!absolutePath.StartsWith(rootPrefix, PathComparison))
            {
                throw new ArenaVariantManifestException(
                    "Arena variant manifest path '" + projectRelativePath +
                    "' resolves outside the Unity project root '" + projectRoot + "'.");
            }

            return absolutePath;
        }

        public static ArenaVariantManifest Parse(
            string json,
            string sourceName = "<memory>")
        {
            string source = string.IsNullOrWhiteSpace(sourceName)
                ? "<memory>"
                : sourceName;
            if (string.IsNullOrWhiteSpace(json))
            {
                throw Error(source, "file is empty; expected a schema-1 JSON object.");
            }

            JsonShape shape = ValidateJsonShape(json, source);
            ManifestJson document;
            try
            {
                document = JsonUtility.FromJson<ManifestJson>(json);
            }
            catch (Exception exception)
            {
                throw new ArenaVariantManifestException(
                    "Arena variant manifest '" + source +
                    "' is not valid JSON. " + exception.Message,
                    exception);
            }

            if (document == null)
            {
                throw Error(source, "could not be parsed as a JSON object.");
            }

            if (document.schemaVersion != CurrentSchemaVersion)
            {
                throw Error(
                    source,
                    "schemaVersion must be " + CurrentSchemaVersion + ", but was " +
                    document.schemaVersion + ".");
            }

            if (document.entries == null || document.entries.Length == 0)
            {
                throw Error(source, "entries must contain at least one arena variant.");
            }

            if (shape.EntryCount != document.entries.Length)
            {
                throw Error(
                    source,
                    "entries JSON shape contains " + shape.EntryCount +
                    " object(s), but Unity parsed " + document.entries.Length + ".");
            }

            var ids = new HashSet<ArenaContentId>();
            var outputPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var representedModes = new HashSet<ArenaContentMode>();
            var defaultCounts = new Dictionary<ArenaContentMode, int>();
            var definitions = new ArenaVariantDefinition[document.entries.Length];
            for (int index = 0; index < document.entries.Length; index++)
            {
                ManifestEntryJson entry = document.entries[index];
                if (entry == null)
                {
                    throw EntryError(source, index, "entry must be a JSON object.");
                }

                ArenaContentId contentId = new ArenaContentId(entry.contentId);
                if (!contentId.IsValid)
                {
                    throw EntryError(
                        source,
                        index,
                        "contentId '" + (entry.contentId ?? string.Empty) +
                        "' is invalid. Use lowercase ASCII letters, numbers and " +
                        "underscores, beginning with a letter.");
                }

                if (!ids.Add(contentId))
                {
                    throw EntryError(
                        source,
                        index,
                        "contentId '" + contentId + "' is duplicated.");
                }

                ArenaContentMode mode = ParseMode(entry.mode, source, index);
                string gameplayLayoutId = RequireStableToken(
                    entry.gameplayLayoutId,
                    "gameplayLayoutId",
                    source,
                    index);
                string generatorRecipe = RequireStableToken(
                    entry.generatorRecipe,
                    "generatorRecipe",
                    source,
                    index);
                string artStatus = RequireStableToken(
                    entry.artStatus,
                    "artStatus",
                    source,
                    index);
                ValidateOutputPrefabPath(entry.outputPrefabPath, source, index);
                if (!outputPaths.Add(entry.outputPrefabPath))
                {
                    throw EntryError(
                        source,
                        index,
                        "outputPrefabPath '" + entry.outputPrefabPath +
                        "' is already used by another entry. Output paths must be " +
                        "unique, including case-only differences.");
                }

                if (entry.presentationVersion < 1)
                {
                    throw EntryError(
                        source,
                        index,
                        "presentationVersion must be at least 1, but was " +
                        entry.presentationVersion + ".");
                }

                string brief = RequireTrimmedText(
                    entry.brief,
                    "brief",
                    source,
                    index);
                representedModes.Add(mode);
                if (entry.defaultForMode)
                {
                    defaultCounts.TryGetValue(mode, out int count);
                    defaultCounts[mode] = count + 1;
                }

                definitions[index] = new ArenaVariantDefinition(
                    contentId,
                    mode,
                    gameplayLayoutId,
                    generatorRecipe,
                    entry.outputPrefabPath,
                    entry.presentationVersion,
                    entry.defaultForMode,
                    artStatus,
                    brief);
            }

            foreach (ArenaContentMode mode in representedModes)
            {
                defaultCounts.TryGetValue(mode, out int count);
                if (count != 1)
                {
                    throw Error(
                        source,
                        "mode '" + mode + "' must have exactly one defaultForMode " +
                        "entry, but has " + count + ".");
                }
            }

            Array.Sort(definitions, CompareDefinitions);
            return new ArenaVariantManifest(definitions);
        }

        private static StringComparison PathComparison =>
            UnityEngine.Application.platform == RuntimePlatform.WindowsEditor
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;

        private static ArenaContentMode ParseMode(
            string value,
            string source,
            int index)
        {
            if (string.Equals(value, "Arena", StringComparison.Ordinal))
            {
                return ArenaContentMode.Arena;
            }

            if (string.Equals(value, "Boss", StringComparison.Ordinal))
            {
                return ArenaContentMode.Boss;
            }

            if (string.Equals(value, "Siege", StringComparison.Ordinal))
            {
                return ArenaContentMode.Siege;
            }

            throw EntryError(
                source,
                index,
                "mode must be exactly 'Arena', 'Boss', or 'Siege', but was '" +
                (value ?? string.Empty) + "'.");
        }

        private static string RequireStableToken(
            string value,
            string fieldName,
            string source,
            int index)
        {
            if (!ArenaContentId.IsWellFormed(value))
            {
                throw EntryError(
                    source,
                    index,
                    fieldName + " '" + (value ?? string.Empty) +
                    "' must be a stable lowercase ASCII token using letters, " +
                    "numbers and underscores.");
            }

            return value;
        }

        private static string RequireTrimmedText(
            string value,
            string fieldName,
            string source,
            int index)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw EntryError(
                    source,
                    index,
                    fieldName + " must be non-empty text.");
            }

            if (!string.Equals(value, value.Trim(), StringComparison.Ordinal))
            {
                throw EntryError(
                    source,
                    index,
                    fieldName + " cannot begin or end with whitespace.");
            }

            return value;
        }

        private static void ValidateOutputPrefabPath(
            string value,
            string source,
            int index)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                !string.Equals(value, value.Trim(), StringComparison.Ordinal) ||
                value.IndexOf('\\') >= 0 ||
                Path.IsPathRooted(value) ||
                !value.StartsWith(ArenaContentRoot, StringComparison.Ordinal) ||
                !value.EndsWith(".prefab", StringComparison.Ordinal))
            {
                throw EntryError(
                    source,
                    index,
                    "outputPrefabPath '" + (value ?? string.Empty) +
                    "' must be a normalized Assets/SessionRpg/Content/Arenas/**/*.prefab " +
                    "path using '/'.");
            }

            ValidatePathSegments(
                value,
                "Arena variant manifest '" + source + "': entries[" + index +
                "].outputPrefabPath");
            string relative = value.Substring(ArenaContentRoot.Length);
            if (relative.Length <= ".prefab".Length || relative[0] == '/')
            {
                throw EntryError(
                    source,
                    index,
                    "outputPrefabPath must name a prefab below '" +
                    ArenaContentRoot + "'.");
            }
        }

        private static void ValidatePathSegments(string value, string label)
        {
            string[] segments = value.Split('/');
            for (int index = 0; index < segments.Length; index++)
            {
                string segment = segments[index];
                if (string.IsNullOrEmpty(segment) || segment == "." || segment == "..")
                {
                    throw new ArenaVariantManifestException(
                        label + " contains an empty, '.' or '..' path segment.");
                }

                for (int characterIndex = 0;
                    characterIndex < segment.Length;
                    characterIndex++)
                {
                    char character = segment[characterIndex];
                    if (char.IsControl(character) ||
                        character == ':' ||
                        character == '*' ||
                        character == '?' ||
                        character == '"' ||
                        character == '<' ||
                        character == '>' ||
                        character == '|')
                    {
                        throw new ArenaVariantManifestException(
                            label + " contains invalid path character '" +
                            character + "'.");
                    }
                }
            }
        }

        private static int CompareDefinitions(
            ArenaVariantDefinition left,
            ArenaVariantDefinition right)
        {
            int modeComparison = left.Mode.CompareTo(right.Mode);
            return modeComparison != 0
                ? modeComparison
                : string.CompareOrdinal(left.ContentId.Value, right.ContentId.Value);
        }

        private static JsonShape ValidateJsonShape(string json, string source)
        {
            var rootProperties = new HashSet<string>(StringComparer.Ordinal);
            HashSet<string> entryProperties = null;
            int entryCount = 0;
            int objectDepth = 0;
            int arrayDepth = 0;
            bool rootClosed = false;
            for (int index = 0; index < json.Length; index++)
            {
                char character = json[index];
                if (char.IsWhiteSpace(character))
                {
                    continue;
                }

                if (character == '"')
                {
                    string token = ReadJsonString(json, ref index, source);
                    int lookahead = index + 1;
                    while (lookahead < json.Length && char.IsWhiteSpace(json[lookahead]))
                    {
                        lookahead++;
                    }

                    if (lookahead < json.Length && json[lookahead] == ':')
                    {
                        HashSet<string> properties = objectDepth == 1
                            ? rootProperties
                            : objectDepth == 2
                                ? entryProperties
                                : null;
                        if (properties == null)
                        {
                            throw Error(
                                source,
                                "property '" + token +
                                "' appears outside the root or an entry object.");
                        }

                        if (!properties.Add(token))
                        {
                            string location = objectDepth == 1
                                ? "root"
                                : "entries[" + entryCount + "]";
                            throw Error(
                                source,
                                location + " repeats property '" + token + "'.");
                        }
                    }

                    continue;
                }

                if (character == '{')
                {
                    if (rootClosed)
                    {
                        throw Error(source, "contains content after the root object.");
                    }

                    objectDepth++;
                    if (objectDepth == 2)
                    {
                        entryProperties = new HashSet<string>(StringComparer.Ordinal);
                    }
                    else if (objectDepth > 2)
                    {
                        throw Error(
                            source,
                            "contains a nested object not supported by schema 1.");
                    }
                }
                else if (character == '}')
                {
                    if (objectDepth == 2)
                    {
                        ValidatePropertySet(
                            entryProperties,
                            EntryPropertyNames,
                            "entries[" + entryCount + "]",
                            source);
                        entryCount++;
                        entryProperties = null;
                    }

                    objectDepth--;
                    if (objectDepth < 0)
                    {
                        throw Error(source, "contains an unmatched '}'.");
                    }

                    if (objectDepth == 0)
                    {
                        ValidatePropertySet(
                            rootProperties,
                            RootPropertyNames,
                            "root",
                            source);
                        rootClosed = true;
                    }
                }
                else if (character == '[')
                {
                    arrayDepth++;
                }
                else if (character == ']')
                {
                    arrayDepth--;
                    if (arrayDepth < 0)
                    {
                        throw Error(source, "contains an unmatched ']'.");
                    }
                }
                else if (rootClosed)
                {
                    throw Error(source, "contains content after the root object.");
                }
            }

            if (!rootClosed || objectDepth != 0 || arrayDepth != 0)
            {
                throw Error(source, "has unbalanced JSON object or array delimiters.");
            }

            return new JsonShape(entryCount);
        }

        private static string ReadJsonString(
            string json,
            ref int index,
            string source)
        {
            var builder = new StringBuilder();
            for (index++; index < json.Length; index++)
            {
                char character = json[index];
                if (character == '"')
                {
                    return builder.ToString();
                }

                if (character != '\\')
                {
                    if (char.IsControl(character))
                    {
                        throw Error(source, "contains an unescaped control character.");
                    }

                    builder.Append(character);
                    continue;
                }

                index++;
                if (index >= json.Length)
                {
                    break;
                }

                char escaped = json[index];
                switch (escaped)
                {
                    case '"':
                    case '\\':
                    case '/':
                        builder.Append(escaped);
                        break;
                    case 'b':
                        builder.Append('\b');
                        break;
                    case 'f':
                        builder.Append('\f');
                        break;
                    case 'n':
                        builder.Append('\n');
                        break;
                    case 'r':
                        builder.Append('\r');
                        break;
                    case 't':
                        builder.Append('\t');
                        break;
                    case 'u':
                        builder.Append(ReadUnicodeEscape(json, ref index, source));
                        break;
                    default:
                        throw Error(
                            source,
                            "contains invalid JSON escape '\\" + escaped + "'.");
                }
            }

            throw Error(source, "contains an unterminated JSON string.");
        }

        private static char ReadUnicodeEscape(
            string json,
            ref int index,
            string source)
        {
            if (index + 4 >= json.Length)
            {
                throw Error(source, "contains an incomplete Unicode escape.");
            }

            int value = 0;
            for (int offset = 1; offset <= 4; offset++)
            {
                char character = json[index + offset];
                int digit = character >= '0' && character <= '9'
                    ? character - '0'
                    : character >= 'a' && character <= 'f'
                        ? character - 'a' + 10
                        : character >= 'A' && character <= 'F'
                            ? character - 'A' + 10
                            : -1;
                if (digit < 0)
                {
                    throw Error(source, "contains an invalid Unicode escape.");
                }

                value = (value << 4) | digit;
            }

            index += 4;
            return (char)value;
        }

        private static void ValidatePropertySet(
            HashSet<string> actual,
            string[] expected,
            string location,
            string source)
        {
            if (actual == null)
            {
                throw Error(source, location + " must be a JSON object.");
            }

            for (int index = 0; index < expected.Length; index++)
            {
                if (!actual.Contains(expected[index]))
                {
                    throw Error(
                        source,
                        location + " is missing required property '" +
                        expected[index] + "'.");
                }
            }

            foreach (string property in actual)
            {
                bool recognized = false;
                for (int index = 0; index < expected.Length; index++)
                {
                    if (string.Equals(property, expected[index], StringComparison.Ordinal))
                    {
                        recognized = true;
                        break;
                    }
                }

                if (!recognized)
                {
                    throw Error(
                        source,
                        location + " contains unknown property '" + property + "'.");
                }
            }
        }

        private static ArenaVariantManifestException EntryError(
            string source,
            int index,
            string message)
        {
            return Error(source, "entries[" + index + "]." + message);
        }

        private static ArenaVariantManifestException Error(
            string source,
            string message)
        {
            return new ArenaVariantManifestException(
                "Arena variant manifest '" + source + "': " + message);
        }

        private readonly struct JsonShape
        {
            public JsonShape(int entryCount)
            {
                EntryCount = entryCount;
            }

            public int EntryCount { get; }
        }

        [Serializable]
        private sealed class ManifestJson
        {
            public int schemaVersion;
            public ManifestEntryJson[] entries;
        }

        [Serializable]
        private sealed class ManifestEntryJson
        {
            public string contentId;
            public string mode;
            public string gameplayLayoutId;
            public string generatorRecipe;
            public string outputPrefabPath;
            public int presentationVersion;
            public bool defaultForMode;
            public string artStatus;
            public string brief;
        }
    }
}
