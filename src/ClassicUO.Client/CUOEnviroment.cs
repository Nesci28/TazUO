// SPDX-License-Identifier: BSD-2-Clause

using System;
using System.IO;
using System.Reflection;
using System.Threading;

namespace ClassicUO
{
    internal enum TwoXAssetDisplayMode : byte
    {
        SameSize = 0,
        NativeWorld = 1,
        HiDpi = 2,
        HiDpiBalanced = 3
    }

    internal static class CUOEnviroment
    {
        public static Thread GameThread;
        public static float DPIScaleFactor = 1.0f;
        public static bool NoSound;
        public static string[] Args;
        public static string[] Plugins;
        public static bool Debug;
        public static bool IsHighDPI;
        public static bool Use2XAssets = true;
        public static TwoXAssetDisplayMode AssetDisplayMode;
        public const float BalancedHiDpiDensity = 1.5f;
        public static uint CurrentRefreshRate;
        public static bool SkipLoginScreen;
        public static bool NoServerPing;
        public static Assembly Assembly => Assembly.GetEntryAssembly();

        public static readonly bool IsUnix = Environment.OSVersion.Platform != PlatformID.Win32NT && Environment.OSVersion.Platform != PlatformID.Win32Windows && Environment.OSVersion.Platform != PlatformID.Win32S && Environment.OSVersion.Platform != PlatformID.WinCE;

        public static readonly string Version = Assembly.GetExecutingAssembly()?.GetName()?.Version?.ToString() ?? "0.0.0.0";
        public static readonly string ExecutablePath =
#if NETFRAMEWORK
           AppContext.BaseDirectory; // Path.GetDirectoryName(Assembly.GetEntryAssembly()?.Location);
#else
            Environment.CurrentDirectory;
#endif
    }
}
