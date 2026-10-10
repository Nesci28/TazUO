#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace TazUO.Editor
{
    internal static class TazUOIOSPostBuild
    {
        [PostProcessBuild(900)]
        private static void Configure(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS)
                return;

            var projectPath = PBXProject.GetPBXProjectPath(path);
            var project = new PBXProject();
            project.ReadFromFile(projectPath);
            var frameworkTarget = project.GetUnityFrameworkTargetGuid();
            var mainTarget = project.GetUnityMainTargetGuid();
            project.AddFrameworkToProject(frameworkTarget, "WebKit.framework", false);
            // Unity's iOS export can mark the targets as device-only. Keep
            // the generated project usable with an Apple-Silicon simulator
            // as well as a signed device build.
            project.SetBuildProperty(mainTarget, "SUPPORTED_PLATFORMS", "iphoneos iphonesimulator");
            project.SetBuildProperty(frameworkTarget, "SUPPORTED_PLATFORMS", "iphoneos iphonesimulator");
            var gameAssemblyTarget = project.TargetGuidByName("GameAssembly");
            if (!string.IsNullOrEmpty(gameAssemblyTarget))
                project.SetBuildProperty(gameAssemblyTarget, "SUPPORTED_PLATFORMS", "iphoneos iphonesimulator");
            project.WriteToFile(projectPath);

            var plistPath = Path.Combine(path, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            var root = plist.root;
            root.SetString("CFBundleDisplayName", "TazUO");
            root.SetBoolean("UIRequiresFullScreen", true);
            var orientations = root.CreateArray("UISupportedInterfaceOrientations");
            orientations.AddString("UIInterfaceOrientationLandscapeLeft");
            orientations.AddString("UIInterfaceOrientationLandscapeRight");
            File.WriteAllText(plistPath, plist.WriteToString());
        }
    }
}
#endif
