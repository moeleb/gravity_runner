#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace GravityHalfDead.Editor
{
    internal static class GravityAndroidBuilder
    {
        [MenuItem("Gravity Half Dead/Build Android APK")]
        public static void BuildAndroidApk()
        {
            const string outputPath = "GravityHalfDead.apk";
            var scenes = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .ToArray();

            if (scenes.Length == 0)
                throw new InvalidOperationException("No enabled scenes are configured for the build.");

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.Android,
                options = BuildOptions.None
            });

            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException("Android build failed: " + report.summary.result);

            Debug.Log("Gravity Half Dead APK built at " + outputPath);
        }
    }
}
#endif
