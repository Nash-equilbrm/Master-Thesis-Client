using System.IO;
using UnityEngine;

namespace Thesis.Managers
{
    // Whether the synthetic-view (DIBR) camera-switch feature can run on this
    // machine at all. OpenDIBR is a Windows-desktop, NVIDIA-GPU-only native
    // app (CUDA/NVDEC) — see
    // Master-Thesis-Reports/handoff_spec_Sep_20th_opendibr_live_control_and_export.md.
    // Callers that don't pass this check must always fall back to the plain
    // crossfade/hard-cut switch — this is a permanent platform limitation,
    // not a transient "not ready yet" state.
    public static class DibrCapability
    {
        // Matches the bundled build laid out under Assets/StreamingAssets/OpenDIBR/
        // (see plan: OpenDIBR's compiled output is copied there before each
        // Windows Player build) and the binary name used in prior manual runs
        // (Master-Thesis-Reports/action_log_Sep_9th.md — "bin\Release\RealtimeDIBR.exe").
        private const string RelativeExePath = "OpenDIBR/RealtimeDIBR.exe";

        private static bool? _cached;

        public static bool IsAvailable
        {
            get
            {
                _cached ??= Evaluate(out _);
                return _cached.Value;
            }
        }

        public static bool IsAvailableWithReason(out string reason)
        {
            if (_cached.HasValue)
            {
                reason = _cached.Value ? "available" : _lastReason;
                return _cached.Value;
            }

            bool result = Evaluate(out reason);
            _cached = result;
            _lastReason = reason;
            return result;
        }

        private static string _lastReason = "not checked yet";

        private static bool Evaluate(out string reason)
        {
            if (Application.platform != RuntimePlatform.WindowsPlayer &&
                Application.platform != RuntimePlatform.WindowsEditor)
            {
                reason = $"unsupported platform ({Application.platform}) — DIBR is Windows-desktop-only";
                return false;
            }

            if (!SystemInfo.graphicsDeviceVendor.Contains("NVIDIA", System.StringComparison.OrdinalIgnoreCase) &&
                !SystemInfo.graphicsDeviceName.Contains("NVIDIA", System.StringComparison.OrdinalIgnoreCase))
            {
                reason = $"no NVIDIA GPU detected (vendor: {SystemInfo.graphicsDeviceVendor}, device: {SystemInfo.graphicsDeviceName}) — OpenDIBR requires NVDEC";
                return false;
            }

            string exePath = Path.Combine(Application.streamingAssetsPath, RelativeExePath);
            if (!File.Exists(exePath))
            {
                reason = $"OpenDIBR not bundled at {exePath}";
                return false;
            }

            reason = "available";
            return true;
        }

        public static string ExePath => Path.Combine(Application.streamingAssetsPath, RelativeExePath);
    }
}
