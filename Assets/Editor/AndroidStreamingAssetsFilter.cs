using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// Excludes desktop-only StreamingAssets folders (FFmpeg, MediaMTX, DibrBridge, OpenDIBR)
/// from Android builds by moving them out before the build and restoring them after.
public class AndroidStreamingAssetsFilter : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    static readonly string[] Folders = { "FFmpeg", "MediaMTX", "DibrBridge", "OpenDIBR" };

    static string StreamingAssets =>
        Path.Combine(Application.dataPath, "StreamingAssets");

    static string TempRoot =>
        Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "AndroidExcludedStreamingAssets"));

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.Android) return;
        MoveOut();
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        if (report.summary.platform != BuildTarget.Android) return;
        MoveBack();
    }

    static void MoveOut()
    {
        Directory.CreateDirectory(TempRoot);
        bool any = false;
        foreach (var folder in Folders)
        {
            var src = Path.Combine(StreamingAssets, folder);
            if (!Directory.Exists(src)) continue;

            var dst = Path.Combine(TempRoot, folder);
            if (Directory.Exists(dst)) Directory.Delete(dst, recursive: true);
            Directory.Move(src, dst);

            var meta = src + ".meta";
            if (File.Exists(meta)) File.Delete(meta);

            Debug.Log($"[AndroidBuild] Excluded StreamingAssets/{folder}");
            any = true;
        }
        if (any) AssetDatabase.Refresh();
    }

    static void MoveBack()
    {
        bool any = false;
        foreach (var folder in Folders)
        {
            var src = Path.Combine(TempRoot, folder);
            if (!Directory.Exists(src)) continue;

            var dst = Path.Combine(StreamingAssets, folder);
            if (Directory.Exists(dst)) Directory.Delete(dst, recursive: true);
            Directory.Move(src, dst);

            Debug.Log($"[AndroidBuild] Restored StreamingAssets/{folder}");
            any = true;
        }
        if (any) AssetDatabase.Refresh();
    }
}
