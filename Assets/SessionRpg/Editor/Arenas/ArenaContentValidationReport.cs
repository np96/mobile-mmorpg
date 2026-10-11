using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace SessionRpg.Editor.Arenas
{
    public enum ArenaContentValidationCode
    {
        NullPresentation,
        PresentationIsNotRoot,
        InvalidContentId,
        InvalidMode,
        InvalidPresentationVersion,
        MissingVisualRoot,
        MissingGameplayProxyRoot,
        MissingGroundRoot,
        RootOutsidePresentation,
        OverlappingVisualAndProxyRoots,
        MissingFlatGroundCapability,
        MissingGroundCollider,
        GroundColliderLayerMismatch,
        TriggerGroundCollider,
        TiltedGroundCollider,
        GroundHeightMismatch,
        RigidbodyOnGameplayProxy,
        VisualUsesAuthoritativeLayer,
        NullCaptureAnchor,
        InvalidCaptureAnchor,
        DuplicateCaptureAnchor,
        CaptureAnchorOutsidePresentation,
        MissingOverviewAnchor,
        MissingGameplayAnchor,
        MissingDenseCombatAnchor,
        NullCatalog,
        EmptyCatalog,
        NullCatalogEntry,
        DuplicateCatalogId,
        AmbiguousCatalogSource,
        MissingCatalogSource,
        MissingPresentationPrefab,
        InvalidPresentationPrefab,
        MissingPresentationScene,
        InvalidPresentationScene,
        PresentationIdentityMismatch,
        PresentationModeMismatch,
        MissingModeDefault,
        DuplicateModeDefault,
    }

    public readonly struct ArenaContentValidationIssue
    {
        public ArenaContentValidationIssue(
            ArenaContentValidationCode code,
            string message,
            UnityEngine.Object context = null)
        {
            Code = code;
            Message = message ?? string.Empty;
            Context = context;
        }

        public ArenaContentValidationCode Code { get; }

        public string Message { get; }

        public UnityEngine.Object Context { get; }
    }

    public sealed class ArenaContentValidationReport
    {
        private readonly List<ArenaContentValidationIssue> _issues =
            new List<ArenaContentValidationIssue>();

        public int ErrorCount => _issues.Count;

        public bool IsValid => _issues.Count == 0;

        public ArenaContentValidationIssue GetIssue(int index)
        {
            if (index < 0 || index >= _issues.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return _issues[index];
        }

        public bool Contains(ArenaContentValidationCode code)
        {
            for (int index = 0; index < _issues.Count; index++)
            {
                if (_issues[index].Code == code)
                {
                    return true;
                }
            }

            return false;
        }

        public override string ToString()
        {
            if (IsValid)
            {
                return "Arena content validation passed.";
            }

            var builder = new StringBuilder();
            builder.Append("Arena content validation failed with ");
            builder.Append(ErrorCount);
            builder.Append(" error(s):");
            for (int index = 0; index < _issues.Count; index++)
            {
                ArenaContentValidationIssue issue = _issues[index];
                builder.AppendLine();
                builder.Append("- [");
                builder.Append(issue.Code);
                builder.Append("] ");
                builder.Append(issue.Message);
            }

            return builder.ToString();
        }

        internal void Add(
            ArenaContentValidationCode code,
            string message,
            UnityEngine.Object context = null)
        {
            _issues.Add(new ArenaContentValidationIssue(code, message, context));
        }

        internal void Merge(ArenaContentValidationReport other)
        {
            if (other == null)
            {
                return;
            }

            for (int index = 0; index < other.ErrorCount; index++)
            {
                _issues.Add(other.GetIssue(index));
            }
        }
    }
}
