using System;
using System.Threading;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace SessionRpg.Editor.Packages
{
    /// <summary>
    /// Installs the unified Multiplayer Services SDK through UPM so Unity owns
    /// both manifest and lock-file resolution. This intentionally avoids the
    /// deprecated standalone Relay and Lobby packages.
    /// </summary>
    public static class MultiplayerPackageInstaller
    {
        private const string PackageName = "com.unity.services.multiplayer";

        [MenuItem("Session RPG/Packages/Install Multiplayer Services")]
        public static void InstallFromMenu()
        {
            Install();
        }

        public static void InstallForCommandLine()
        {
            try
            {
                Install();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
                return;
            }

            EditorApplication.Exit(0);
        }

        private static void Install()
        {
            AddRequest request = Client.Add(PackageName);
            while (!request.IsCompleted)
            {
                Thread.Sleep(100);
            }

            if (request.Status != StatusCode.Success || request.Result == null)
            {
                string detail = request.Error != null
                    ? request.Error.message
                    : "Unity Package Manager returned no package result.";
                throw new InvalidOperationException(
                    "Could not install " + PackageName + ": " + detail);
            }

            Debug.Log(
                "Installed and locked " + request.Result.name + "@" +
                request.Result.version + ".");
        }
    }
}
