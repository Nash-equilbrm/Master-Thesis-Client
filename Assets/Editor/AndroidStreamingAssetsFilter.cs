using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// Keeps desktop-only payload (DibrBridge incl. FFS/torch weights, FFmpeg, MediaMTX, OpenDIBR, and
/// anything added later) out of Android and iOS builds. Deny-by-default: every top-level entry under
/// Assets/StreamingAssets is moved out before the build except the AllowList, then moved back.
/// Folders are moved (same-volume rename, instant even for multi-GB) together with their .meta so
/// GUIDs survive. Windows builds are untouched.
[InitializeOnLoad]
public class AndroidStreamingAssetsFilter : IPreprocessBuildWithReport, IPostprocessBuildWithReport
{
    public int callbackOrder => 0;

    // Top-level names (without .meta) that mobile builds keep. build_info is read by the in-app log reporter.
    static readonly string[] AllowList = { "build_info" };

    static string StreamingAssets =>
        Path.Combine(Application.dataPath, "StreamingAssets");

    // Library, not Temp: Unity wipes Temp on editor start, which would destroy the stash after a crash.
    static string StashRoot =>
        Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Library", "MobileExcludedStreamingAssets"));

    // Where earlier versions of this script stashed folders; still recovered from.
    static string LegacyStashRoot =>
        Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Temp", "AndroidExcludedStreamingAssets"));

    static bool IsMobile(BuildTarget t) => t == BuildTarget.Android || t == BuildTarget.iOS;

    static double _nextRecoveryCheck;

    static AndroidStreamingAssetsFilter()
    {
        // Postprocess is not called when a build fails or the editor crashes, so restore from the editor loop.
        EditorApplication.update += RecoverLeftovers;
    }

    public void OnPreprocessBuild(BuildReport report)
    {
        if (!IsMobile(report.summary.platform)) return;
        RestoreAll(); // leftovers from a previous crashed/failed build
        MoveOut(report.summary.platform);
    }

    public void OnPostprocessBuild(BuildReport report)
    {
        if (!IsMobile(report.summary.platform)) return;
        RestoreAll();
    }

    static void RecoverLeftovers()
    {
        if (EditorApplication.timeSinceStartup < _nextRecoveryCheck) return;
        _nextRecoveryCheck = EditorApplication.timeSinceStartup + 3;
        if (BuildPipeline.isBuildingPlayer || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
        if (HasEntries(StashRoot) || HasEntries(LegacyStashRoot)) RestoreAll();
    }

    static bool HasEntries(string dir) =>
        Directory.Exists(dir) && Directory.GetFileSystemEntries(dir).Length > 0;

    static bool IsAllowed(string name)
    {
        if (name.EndsWith(".meta")) name = name.Substring(0, name.Length - ".meta".Length);
        foreach (var a in AllowList) if (a == name) return true;
        return false;
    }

    static void MoveOut(BuildTarget platform)
    {
        if (!Directory.Exists(StreamingAssets)) return;
        Directory.CreateDirectory(StashRoot);
        bool any = false;
        foreach (var path in Directory.GetFileSystemEntries(StreamingAssets))
        {
            var name = Path.GetFileName(path);
            if (name.EndsWith(".meta") || IsAllowed(name)) continue;

            MoveEntry(path, Path.Combine(StashRoot, name));
            var meta = path + ".meta";
            if (File.Exists(meta)) MoveEntry(meta, Path.Combine(StashRoot, name + ".meta"));

            Debug.Log($"[MobileBuild:{platform}] Excluded StreamingAssets/{name}");
            any = true;
        }
        if (any) AssetDatabase.Refresh();
    }

    static void RestoreAll()
    {
        bool any = RestoreFrom(StashRoot);
        any |= RestoreFrom(LegacyStashRoot);
        if (any) AssetDatabase.Refresh();
    }

    static bool RestoreFrom(string root)
    {
        if (!Directory.Exists(root)) return false;
        Directory.CreateDirectory(StreamingAssets);
        bool any = false;
        foreach (var path in Directory.GetFileSystemEntries(root))
        {
            var name = Path.GetFileName(path);
            var dst = Path.Combine(StreamingAssets, name);
            // The stash is authoritative; anything Unity regenerated at the destination in the meantime
            // (empty folder, fresh .meta) is replaced.
            DeleteEntry(dst);
            MoveEntry(path, dst);
            if (!name.EndsWith(".meta")) Debug.Log($"[MobileBuild] Restored StreamingAssets/{name}");
            any = true;
        }
        if (Directory.GetFileSystemEntries(root).Length == 0) Directory.Delete(root);
        return any;
    }

    static void MoveEntry(string src, string dst)
    {
        DeleteEntry(dst);
        if (Directory.Exists(src)) Directory.Move(src, dst);
        else File.Move(src, dst);
    }

    static void DeleteEntry(string path)
    {
        if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        else if (File.Exists(path)) File.Delete(path);
    }
}
