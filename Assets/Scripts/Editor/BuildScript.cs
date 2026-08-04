using UnityEngine;
using UnityEditor;
using System.IO;

namespace MmoPoC.Editor
{
    public static class BuildScript
    {
        private const string ClientProfilePath = "Assets/Settings/Build Profiles/ClientProfile.asset";
        private const string ServerProfilePath = "Assets/Settings/Build Profiles/ServerProfile.asset";

        [MenuItem("Build/Build Dedicated Server (Windows - Local Test)")]
        public static void BuildServerWindows()
        {
            Debug.Log("[BuildScript] Starting Dedicated Server build for Windows...");
            BuildServerInternal(BuildTarget.StandaloneWindows64, "Builds/ServerWindows", "MmoPocServer.exe", "4e3c793746204150860bf175a9a41a05");
        }

        [MenuItem("Build/Build Dedicated Server (Linux - Railway Host)")]
        public static void BuildServerLinux()
        {
            Debug.Log("[BuildScript] Starting Dedicated Server build for Linux...");
            // Platform GUID is empty or blank for Linux since Linux build support doesn't require a specific platform license GUID on Windows
            BuildServerInternal(BuildTarget.StandaloneLinux64, "Builds/ServerLinux", "MmoPocServer", "");
        }

        private static void BuildServerInternal(BuildTarget target, string outputDir, string fileName, string platformId)
        {
            // 1. Ensure build output directory exists
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // 2. Load and set Active Build Profile
            var buildProfileType = System.Type.GetType("UnityEditor.Build.Profile.BuildProfile, UnityEditor");
            if (buildProfileType != null)
            {
                var serverProfile = AssetDatabase.LoadAssetAtPath(ServerProfilePath, buildProfileType);
                if (serverProfile != null)
                {
                    // Reconfigure target platform on the profile on the fly
                    SerializedObject soServer = new SerializedObject(serverProfile);
                    soServer.FindProperty("m_BuildTarget").intValue = (int)target;
                    soServer.FindProperty("m_PlatformId").stringValue = platformId;
                    soServer.ApplyModifiedProperties();
                    EditorUtility.SetDirty(serverProfile);
                    AssetDatabase.SaveAssets();

                    var setActiveMethod = buildProfileType.GetMethod("SetActiveBuildProfile");
                    if (setActiveMethod != null)
                    {
                        setActiveMethod.Invoke(null, new object[] { serverProfile });
                        Debug.Log($"[BuildScript] Reconfigured and set Active Build Profile to {ServerProfilePath}");
                    }
                }
            }

            // 3. Prepare BuildPlayerOptions
            BuildPlayerOptions opt = new BuildPlayerOptions();
            opt.scenes = new string[] { "Assets/Scenes/TestWorld.unity" };
            opt.locationPathName = Path.Combine(outputDir, fileName);
            opt.target = target;
            opt.subtarget = 1; // StandaloneBuildSubtarget.Server
            opt.options = BuildOptions.None;

            // 4. Build Player
            var report = BuildPipeline.BuildPlayer(opt);
            var summary = report.summary;

            if (summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.Log($"[BuildScript] Dedicated Server ({target}) build succeeded! Output location: {opt.locationPathName}");
            }
            else
            {
                Debug.LogError($"[BuildScript] Dedicated Server build failed with result: {summary.result}");
            }
        }

        [MenuItem("Build/Build Client (Windows)")]
        public static void BuildClient()
        {
            Debug.Log("[BuildScript] Starting Client build for Windows (Windowed Mode)...");

            // 1. Ensure build output directory exists
            string outputDir = "Builds/Client";
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // 2. Load and set Active Build Profile
            var buildProfileType = System.Type.GetType("UnityEditor.Build.Profile.BuildProfile, UnityEditor");
            if (buildProfileType != null)
            {
                var clientProfile = AssetDatabase.LoadAssetAtPath(ClientProfilePath, buildProfileType);
                if (clientProfile != null)
                {
                    var setActiveMethod = buildProfileType.GetMethod("SetActiveBuildProfile");
                    if (setActiveMethod != null)
                    {
                        setActiveMethod.Invoke(null, new object[] { clientProfile });
                        Debug.Log($"[BuildScript] Set Active Build Profile to {ClientProfilePath}");
                    }
                }
            }

            // 3. Save original PlayerSettings
            FullScreenMode originalMode = PlayerSettings.fullScreenMode;
            int originalWidth = PlayerSettings.defaultScreenWidth;
            int originalHeight = PlayerSettings.defaultScreenHeight;
            bool originalRunInBackground = PlayerSettings.runInBackground;

            // 4. Force Windowed Mode settings and run in background (crucial for local multi-client tests)
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1280;
            PlayerSettings.defaultScreenHeight = 720;
            PlayerSettings.runInBackground = true; 

            // 5. Prepare BuildPlayerOptions
            BuildPlayerOptions opt = new BuildPlayerOptions();
            opt.scenes = new string[] { "Assets/Scenes/TestWorld.unity" };
            opt.locationPathName = Path.Combine(outputDir, "MmoPocClient.exe");
            opt.target = BuildTarget.StandaloneWindows64;
            opt.subtarget = 2; // StandaloneBuildSubtarget.Player
            opt.options = BuildOptions.None;

            // 6. Build Player
            var report = BuildPipeline.BuildPlayer(opt);
            var summary = report.summary;

            // 7. Restore original PlayerSettings
            PlayerSettings.fullScreenMode = originalMode;
            PlayerSettings.defaultScreenWidth = originalWidth;
            PlayerSettings.defaultScreenHeight = originalHeight;
            PlayerSettings.runInBackground = originalRunInBackground;

            if (summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.Log($"[BuildScript] Client build succeeded in Windowed Mode! Output location: {opt.locationPathName}");
            }
            else
            {
                Debug.LogError($"[BuildScript] Client build failed with result: {summary.result}");
            }
        }
    }
}
