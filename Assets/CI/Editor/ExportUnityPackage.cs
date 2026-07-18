using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Dev.Yamachu.Kurogo.CI
{
    /// <summary>
    /// CI-only helper for exporting a single package folder (e.g. Assets/dev.yamachu.kurogo.core) as a
    /// .unitypackage. Not part of either distributed package - lives outside them so it never ships to
    /// end users. Invoked from GitHub Actions via -batchmode -executeMethod with -packagePath/-exportPath
    /// command line arguments; the [MenuItem] is kept for manual local runs.
    /// </summary>
    public static class ExportUnityPackage
    {
        private static string GetCommandLineArg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }

            return null;
        }

        private const string ArtifactDirectory = "Artifacts";

        [MenuItem("Tools/Kurogo/Export Package")]
        public static void Export()
        {
            var packagePath = GetCommandLineArg("-packagePath") ?? "Assets/dev.yamachu.kurogo.core";
            var outputPath = GetCommandLineArg("-exportPath") ?? $"{ArtifactDirectory}/{Path.GetFileName(packagePath)}.unitypackage";

            if (!AssetDatabase.IsValidFolder(packagePath))
            {
                Debug.LogError($"Export root not found: {packagePath}");
                return;
            }

            Directory.CreateDirectory(ArtifactDirectory);
            AssetDatabase.Refresh();

            var exportPaths = new List<string>();
            foreach (var guid in AssetDatabase.FindAssets(string.Empty, new[] { packagePath }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path))
                {
                    exportPaths.Add(path);
                }
            }

            if (exportPaths.Count == 0)
            {
                Debug.LogError($"No export assets found under {packagePath}.");
                return;
            }

            AssetDatabase.ExportPackage(
                exportPaths.ToArray(),
                outputPath,
                ExportPackageOptions.Recurse);

            Debug.Log($"Exported unitypackage: {outputPath} (paths={exportPaths.Count})");
        }
    }
}
