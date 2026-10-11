using System;
using System.IO;
using System.Linq;
using SessionRpg.Editor.Authoring;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SessionRpg.Editor.Build
{
    public static class SessionRpgMacCaptureBuilder
    {
        public const string DevelopmentMenuPath = "Tools/Session RPG/Build macOS Capture Player";

        [MenuItem(DevelopmentMenuPath, priority = 501)]
        public static void BuildDevelopmentCaptureFromMenu()
        {
            BuildDevelopmentCapture();
        }

        public static void BuildDevelopmentCaptureForCommandLine()
        {
            BuildDevelopmentCapture();
        }

        public static void BuildDevelopmentCapture()
        {
            SessionRpgAuthoringGenerator.ValidateTrackedFoundation();

            string outputDirectory = Path.GetFullPath(Path.Combine(
                UnityEngine.Application.dataPath,
                "..",
                "..",
                "outputs"));
            Directory.CreateDirectory(outputDirectory);
            string outputPath = Path.Combine(outputDirectory, "SessionRpg-mac-capture.app");

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes
                    .Where(scene => scene.enabled)
                    .Select(scene => scene.path)
                    .ToArray(),
                locationPathName = outputPath,
                target = BuildTarget.StandaloneOSX,
                targetGroup = BuildTargetGroup.Standalone,
                // This player is an internal visual-review artifact. Mark it as
                // development so evaluation-only character trials can never be
                // confused with a release build.
                options = BuildOptions.Development,
                extraScriptingDefines = new[] { "SESSION_RPG_CAPTURE" },
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException(
                    "Session RPG macOS capture-player build failed: " + report.summary.result +
                    " (" + report.summary.totalErrors + " errors).");
            }

            Debug.Log("Session RPG macOS capture player built successfully: " + outputPath);
        }
    }
}
