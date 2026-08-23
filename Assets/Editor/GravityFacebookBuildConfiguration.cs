#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using Facebook.Unity.Editor;
using Facebook.Unity.Settings;
using UnityEditor;
using UnityEngine;

namespace GravityHalfDead.Editor
{
    internal static class GravityFacebookBuildConfiguration
    {
        private const string AppId = "1029206673440587";
        private const string ClientToken = "dcf690e7e2f9a39318a7e2b822aac529";
        private const string SettingsAssetPath =
            "Assets/FacebookSDK/SDK/Resources/FacebookSettings.asset";
        [InitializeOnLoadMethod]
        private static void ScheduleConfiguration()
        {
            if (File.Exists(SettingsAssetPath))
                return;

            EditorApplication.delayCall -= Configure;
            EditorApplication.delayCall += Configure;
        }

        [MenuItem("Gravity Half Dead/Configure Facebook SDK")]
        private static void Configure()
        {
            var settings = FacebookSettings.Instance;
            if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(settings)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsAssetPath));
                AssetDatabase.CreateAsset(settings, SettingsAssetPath);
            }

            FacebookSettings.AppLabels = new List<string> { "gravity-half-dead" };
            FacebookSettings.AppIds = new List<string> { AppId };
            FacebookSettings.ClientTokens = new List<string> { ClientToken };
            FacebookSettings.AppLinkSchemes = new List<FacebookSettings.UrlSchemes>
            {
                new FacebookSettings.UrlSchemes()
            };
            FacebookSettings.SelectedAppIndex = 0;
            FacebookSettings.EditorBuildTarget = FacebookSettings.BuildTarget.Android;

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            ManifestMod.GenerateManifest();
            AssetDatabase.Refresh();
            Debug.Log("Gravity Half Dead: Facebook SDK settings and Android manifest configured.");
        }
    }
}
#endif
