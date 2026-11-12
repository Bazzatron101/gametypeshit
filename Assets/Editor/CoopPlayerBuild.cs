using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace HunterSystem.Editor
{
    /// <summary>
    /// Builds the co-op spike as a Windows player in Builds/Coop/, with a steam_appid.txt (480,
    /// Valve's test app) beside the exe so Steam works without a store page. Copy the folder to the
    /// second PC to test over Steam. From the menu or with:
    /// Unity -batchmode -quit -projectPath . -executeMethod HunterSystem.Editor.CoopPlayerBuild.Build
    /// </summary>
    public static class CoopPlayerBuild
    {
        public const string OutputFolder = "Builds/Coop";
        public const string ExeName = "HunterSystem.exe";

        [MenuItem("Hunter System/Build Co-op Player (Windows)")]
        public static void Build()
        {
            var options = new BuildPlayerOptions
            {
                scenes = new[] { GreyboxSceneBuilder.CoopScenePath },
                locationPathName = $"{OutputFolder}/{ExeName}",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            if (report.summary.result != BuildResult.Succeeded)
            {
                Debug.LogError($"Co-op build failed: {report.summary.result}, {report.summary.totalErrors} errors.");
                if (Application.isBatchMode) EditorApplication.Exit(1);
                return;
            }

            File.WriteAllText(Path.Combine(OutputFolder, "steam_appid.txt"), "480");
            Debug.Log($"Co-op build: {Path.GetFullPath(OutputFolder)} ({report.summary.totalSize / (1024 * 1024)} MB)");
        }
    }
}
