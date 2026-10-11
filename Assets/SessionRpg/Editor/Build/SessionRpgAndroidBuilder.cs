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
    public static class SessionRpgAndroidBuilder
    {
        public const string DevelopmentMenuPath = "Tools/Session RPG/Build Android Development APK";

        [MenuItem(DevelopmentMenuPath, priority = 500)]
        public static void BuildDevelopmentFromMenu()
        {
            BuildDevelopment();
        }

        public static void BuildDevelopmentForCommandLine()
        {
            BuildDevelopment();
        }

        public static void BuildDevelopment()
        {
            SessionRpgAuthoringGenerator.ValidateTrackedFoundation();
            ValidateAndroidSettings();

            string outputDirectory = Path.GetFullPath(Path.Combine(
                UnityEngine.Application.dataPath,
                "..",
                "..",
                "outputs"));
            Directory.CreateDirectory(outputDirectory);
            string outputPath = Path.Combine(outputDirectory, "SessionRpg-dev.apk");

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes
                    .Where(scene => scene.enabled)
                    .Select(scene => scene.path)
                    .ToArray(),
                locationPathName = outputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.Development | BuildOptions.AllowDebugging,
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                throw new BuildFailedException(
                    "Session RPG Android build failed: " + report.summary.result +
                    " (" + report.summary.totalErrors + " errors).");
            }

            long bytes = new FileInfo(outputPath).Length;
            Debug.Log(
                "Session RPG development APK built successfully: " + outputPath +
                " (" + (bytes / (1024f * 1024f)).ToString("0.0") + " MiB).");
        }

        private static void ValidateAndroidSettings()
        {
            const string expectedIdentifier = "com.sessionrpg.prototype";
            string identifier = PlayerSettings.GetApplicationIdentifier(NamedBuildTarget.Android);
            if (!string.Equals(identifier, expectedIdentifier, StringComparison.Ordinal))
            {
                throw new BuildFailedException(
                    "Android package must be '" + expectedIdentifier + "', but is '" + identifier +
                    "'. Rebuild the authored foundation.");
            }

            if (PlayerSettings.Android.targetArchitectures != AndroidArchitecture.ARM64)
            {
                throw new BuildFailedException("Android build must target ARM64 only for this prototype.");
            }

            if (PlayerSettings.defaultInterfaceOrientation != UIOrientation.AutoRotation ||
                PlayerSettings.allowedAutorotateToPortrait ||
                PlayerSettings.allowedAutorotateToPortraitUpsideDown ||
                !PlayerSettings.allowedAutorotateToLandscapeLeft ||
                !PlayerSettings.allowedAutorotateToLandscapeRight)
            {
                throw new BuildFailedException("Android orientation must be landscape-only autorotation.");
            }
        }
    }
}
