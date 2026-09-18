#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace TazUO.Editor
{
    public static class BuildIOS
    {
        public static void Perform()
        {
            PlayerSettings.companyName = "TazUO";
            PlayerSettings.productName = "TazUO";
            // Apple bundle versions must contain digits and dots only. The
            // upstream project uses SemVer metadata (for example 0.9.0+1),
            // which iOS rejects during post-processing.
            PlayerSettings.bundleVersion = "0.9.0";
            PlayerSettings.iOS.buildNumber = "1";
            if (Environment.GetEnvironmentVariable("TAZUO_IOS_SIMULATOR") == "1")
            {
                PlayerSettings.iOS.sdkVersion = iOSSdkVersion.SimulatorSDK;
                PlayerSettings.iOS.simulatorSdkArchitecture = AppleMobileArchitectureSimulator.ARM64;
            }
            else
            {
                PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
            }
            PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.iOS, "com.tazuo.mobile");
            // Unity resolves relative build paths from the project directory
            // in batchmode. Keep the Xcode export beside Assets so the shell
            // wrapper can locate it consistently.
            var output = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Xcode");
            Directory.CreateDirectory(output);
            var scenes = new[] { "Assets/Scene.unity" };
            var options = Environment.GetEnvironmentVariable("TAZUO_IOS_SIMULATOR") == "1"
                ? BuildOptions.Development : BuildOptions.None;
            var report = BuildPipeline.BuildPlayer(scenes, output, BuildTarget.iOS, options);
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException($"TazUO iOS build failed: {report.summary.result}");
        }
    }
}
#endif
