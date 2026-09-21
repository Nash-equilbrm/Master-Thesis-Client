using System;
using System.Diagnostics;
using System.IO;
using Thesis.Managers;
using Debug = UnityEngine.Debug;

namespace Thesis.Dibr
{
    // Starts/stops the OpenDIBR, bridge, and mediamtx child processes.
    // Launched once per viewer session (see OpenDibrSessionManager), not per
    // switch.
    public sealed class OpenDibrProcessLauncher : IDisposable
    {
        private Process _openDibrProcess;
        private Process _bridgeProcess;
        private Process _mediaMtxProcess;

        public bool IsOpenDibrRunning => _openDibrProcess is { HasExited: false };
        public bool IsBridgeRunning => _bridgeProcess is { HasExited: false };
        public bool IsMediaMtxRunning => _mediaMtxProcess is { HasExited: false };

        // configPath may be null — mediamtx runs with sane defaults (RTSP on
        // :8554) if no config file is passed.
        public bool StartMediaMtx(string exePath, string configPath = null)
        {
            if (IsMediaMtxRunning) return true;
            if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath))
            {
                Debug.LogWarning($"[OpenDibrProcessLauncher] mediamtx executable not found at '{exePath}'.");
                return false;
            }

            string args = string.IsNullOrEmpty(configPath) ? null : $"\"{configPath}\"";
            return TryStart(ref _mediaMtxProcess, exePath, args);
        }

        // startupJsonPath: required — OpenDIBR refuses to launch with zero
        // cameras (confirmed by opendibr-c3, 2026-09-20), see
        // OpenDibrStartupJson.cs. `-i` is required by its argument parser
        // but functionally unused now that every camera path is a URL — any
        // existing directory works; this passes OpenDIBR's own folder.
        public bool StartOpenDibr(string startupJsonPath)
        {
            if (IsOpenDibrRunning) return true;
            if (!DibrCapability.IsAvailableWithReason(out string reason))
            {
                Debug.LogWarning($"[OpenDibrProcessLauncher] Not starting OpenDIBR — {reason}");
                return false;
            }

            string exeDir = Path.GetDirectoryName(DibrCapability.ExePath);
            string args = $"-j \"{startupJsonPath}\" -i \"{exeDir}\"";
            Debug.Log($"[OpenDibrProcessLauncher] Launching OpenDIBR: {DibrCapability.ExePath} {args}");
            return TryStart(ref _openDibrProcess, DibrCapability.ExePath, args, captureOutput: true, logTag: "OpenDIBR");
        }

        // bridgeExePath: the PyInstaller-frozen dibr-bridge.exe (bundled
        // under StreamingAssets, see OpenDibrSessionManager). ffmpegDir: the
        // bundled FFmpeg folder, prepended to the bridge's own PATH so its
        // "ffmpeg" subprocess calls resolve without needing ffmpeg installed
        // system-wide — no bridge-side code change needed for this, it still
        // just calls "ffmpeg" and relies on PATH resolution.
        public bool StartBridge(string bridgeExePath, string arguments = null, string ffmpegDir = null, bool captureOutput = false)
        {
            if (IsBridgeRunning) return true;
            if (string.IsNullOrEmpty(bridgeExePath) || !File.Exists(bridgeExePath))
            {
                Debug.LogWarning($"[OpenDibrProcessLauncher] Bridge executable not found at '{bridgeExePath}'.");
                return false;
            }

            return TryStart(ref _bridgeProcess, bridgeExePath, arguments, ffmpegDir, captureOutput, logTag: "Bridge");
        }

        private static bool TryStart(ref Process slot, string exePath, string arguments,
            string extraPathDir = null, bool captureOutput = false, string logTag = null)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = arguments,
                    WorkingDirectory = Path.GetDirectoryName(exePath),
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = captureOutput,
                    RedirectStandardError = captureOutput,
                };
                if (!string.IsNullOrEmpty(extraPathDir))
                {
                    // .NET pre-populates EnvironmentVariables with a copy of
                    // this process's own environment when UseShellExecute is
                    // false — safe to read/modify directly.
                    psi.EnvironmentVariables["PATH"] = extraPathDir + Path.PathSeparator + psi.EnvironmentVariables["PATH"];
                }

                var p = new Process { StartInfo = psi };
                if (captureOutput)
                {
                    string tag = logTag ?? Path.GetFileNameWithoutExtension(exePath);
                    p.OutputDataReceived += (_, e) => { if (e.Data != null) Debug.Log($"[{tag} stdout] {e.Data}"); };
                    p.ErrorDataReceived  += (_, e) => { if (e.Data != null) Debug.Log($"[{tag} stderr] {e.Data}"); };
                }

                if (!p.Start()) return false;

                if (captureOutput)
                {
                    p.BeginOutputReadLine();
                    p.BeginErrorReadLine();
                }

                slot = p;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[OpenDibrProcessLauncher] Failed to start '{exePath}': {e.Message}");
                return false;
            }
        }

        public void StopAll()
        {
            TryKill(ref _openDibrProcess);
            TryKill(ref _bridgeProcess);
            TryKill(ref _mediaMtxProcess);
        }

        private static void TryKill(ref Process p)
        {
            if (p == null) return;
            try { if (!p.HasExited) p.Kill(); }
            catch (Exception e) { Debug.LogWarning($"[OpenDibrProcessLauncher] Kill failed: {e.Message}"); }
            p.Dispose();
            p = null;
        }

        public void Dispose() => StopAll();
    }
}
