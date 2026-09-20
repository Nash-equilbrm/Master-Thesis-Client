using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Thesis.Calibration;
using Thesis.Managers;
using Thesis.Patterns;
using UnityEngine;

namespace Thesis.Dibr
{
    public readonly struct PrepareResult
    {
        public readonly bool Success;
        public readonly bool PermanentlyUnavailable; // true = don't retry this pair (e.g. no calibration)
        public readonly string Reason;
        public readonly ExtrinsicsData ExtrinsicsA;
        public readonly ExtrinsicsData ExtrinsicsB;

        private PrepareResult(bool success, bool permanentlyUnavailable, string reason, ExtrinsicsData a, ExtrinsicsData b)
        {
            Success = success; PermanentlyUnavailable = permanentlyUnavailable; Reason = reason; ExtrinsicsA = a; ExtrinsicsB = b;
        }

        public static PrepareResult Ready(ExtrinsicsData a, ExtrinsicsData b) => new(true, false, null, a, b);
        public static PrepareResult NotCalibrated() => new(false, true, "No calibration for this camera pair.", null, null);
        public static PrepareResult Failed(string reason) => new(false, false, reason, null, null);
    }

    // Owns the OpenDIBR + bridge process lifecycle (launched once per viewer
    // session, per the plan's Phase D), the per-camera "already added" cache
    // on both sides, and the calibration lookup + pairwise depth-pipeline
    // setup needed before a synthetic-view transition can render. Does NOT
    // own the UI/UX sequencing (crossfade-first, when to start/stop) — that's
    // CameraSwitcher's job, calling into this as a plain async service.
    public sealed class OpenDibrSessionManager : Singleton<OpenDibrSessionManager>
    {
        [Header("Bridge (Phase A — path TBD until that component exists)")]
        [SerializeField] private string _bridgeExePathOverride;

        // Must match dibr-bridge's --rtsp-base default (bridge/main.py).
        [SerializeField] private string _rtspBase = "rtsp://localhost:8554";

        [Header("Recently-used camera cache size")]
        [SerializeField] private int _maxCachedCameras = 3;

        // OpenDIBR fixes its render/decode resolution at process launch
        // (confirmed by opendibr-c3, 2026-09-20) — every add_camera's
        // Resolution must match this exactly or the call is rejected. Must
        // match dibr-bridge's --width/--height defaults too (both the
        // viewport AND the bridge's placeholder-camera resolution derive
        // from this on the Unity side and from its own CLI args on the
        // bridge side — keep all three in sync if this default ever
        // changes).
        [Header("Must match OpenDIBR's startup resolution")]
        [SerializeField] private int _outputWidth = 1280;
        [SerializeField] private int _outputHeight = 720;

        // Matches dibr-bridge/bridge/session.py's PLACEHOLDER_NAME exactly —
        // a permanently-running dummy stream required by OpenDIBR's
        // startup-time "at least one decodable camera" check (confirmed by
        // opendibr-c3, 2026-09-20). Never selected into any real pairing.
        private const string PlaceholderName = "dibr_placeholder";

        public OpenDibrFrameReceiver FrameReceiver { get; private set; }
        public bool IsSessionStarted { get; private set; }

        private OpenDibrProcessLauncher _launcher;
        private OpenDibrControlChannel _openDibrControl;
        private DibrBridgeControlChannel _bridgeControl;
        private OpenDibrPoseChannel _poseChannel;

        // Insertion-ordered "recently used" identity list — the OpenDIBR-side
        // cache (per-camera decode, pairing-independent, see
        // DibrBridgeControlChannel's doc comment for why this differs from
        // the bridge's always-fresh-per-pair behavior).
        private readonly List<string> _openDibrAddedOrder = new();
        private readonly Dictionary<string, DibrBridgeControlChannel.CameraUrls> _bridgeAdded = new();

        protected override void Awake()
        {
            base.Awake();
            FrameReceiver = gameObject.GetComponent<OpenDibrFrameReceiver>();
            if (FrameReceiver == null) FrameReceiver = gameObject.AddComponent<OpenDibrFrameReceiver>();

            // This GameObject lives under AppRoleManager's "ViewerManagers"
            // container, which starts inactive and is only SetActive(true)
            // when Viewer role is selected (AppRoleManager.SelectRole) — so
            // Awake() firing here already coincides exactly with "Viewer
            // role was just chosen," no extra event wiring needed. Starts
            // the whole pipeline (mediamtx + bridge + OpenDIBR) immediately
            // rather than waiting for the first camera switch, trading
            // resource cost for the whole Viewer session against a much
            // shorter/likely-zero cold-start delay on that first switch.
            // Fire-and-forget: failures are logged inside
            // DoStartSessionAsync and simply leave IsSessionStarted false,
            // which PreparePairAsync already handles as "fall back to
            // crossfade."
            _ = EnsureSessionStartedAsync();
        }

        private Task _startTask;

        // Bridge MUST be launched (and its always-on placeholder stream
        // given time to come up) before OpenDIBR — OpenDIBR's startup-JSON
        // validation demuxes+decodes every listed camera, including the
        // placeholder, before it finishes booting, and simply fails to
        // launch if that stream isn't already reachable (confirmed by
        // opendibr-c3, 2026-09-20). See OpenDibrStartupJson.cs and
        // dibr-bridge/bridge/session.py's PLACEHOLDER_NAME.
        public async Task EnsureSessionStartedAsync()
        {
            if (IsSessionStarted) return;
            if (_startTask != null) { await _startTask; return; }

            var tcs = new TaskCompletionSource<bool>();
            _startTask = tcs.Task;
            try
            {
                await DoStartSessionAsync();
            }
            finally
            {
                tcs.TrySetResult(true);
            }
        }

        private async Task DoStartSessionAsync()
        {
            if (!DibrCapability.IsAvailableWithReason(out string reason))
            {
                Debug.Log($"[OpenDibrSessionManager] Synthetic view unavailable on this machine: {reason}");
                return;
            }

            _launcher = new OpenDibrProcessLauncher();

            // mediamtx first — the bridge pushes RTSP into it immediately on
            // startup (its placeholder stream), so it needs to already be
            // listening before the bridge process even launches.
            string mediaMtxPath = System.IO.Path.Combine(Application.streamingAssetsPath, "MediaMTX", "mediamtx.exe");
            string mediaMtxConfig = System.IO.Path.Combine(Application.streamingAssetsPath, "MediaMTX", "mediamtx.yml");
            if (!_launcher.StartMediaMtx(mediaMtxPath, mediaMtxConfig))
            {
                Debug.LogWarning("[OpenDibrSessionManager] mediamtx failed to start — synthetic view unavailable this session.");
                return;
            }
            await Task.Delay(1000); // brief startup time before the bridge tries to push into it

            string bridgePath = string.IsNullOrEmpty(_bridgeExePathOverride)
                ? System.IO.Path.Combine(Application.streamingAssetsPath, "DibrBridge", "dibr-bridge.exe")
                : _bridgeExePathOverride;
            string ffmpegDir = System.IO.Path.Combine(Application.streamingAssetsPath, "FFmpeg");
            string bridgeArgs = $"--server-url \"{Thesis.AppConfig.ServerUrl}\" --rtsp-base \"{_rtspBase}\" " +
                                 $"--width {_outputWidth} --height {_outputHeight}";
            if (!_launcher.StartBridge(bridgePath, bridgeArgs, ffmpegDir))
            {
                Debug.LogWarning("[OpenDibrSessionManager] Bridge failed to start — synthetic view unavailable this session.");
                return;
            }

            // Heuristic fixed delay for the bridge's placeholder stream to
            // actually become decodable (ffmpeg encoder init + RTSP
            // registration) — replace with a real readiness signal from the
            // bridge if this proves flaky once actually run.
            await Task.Delay(3000);

            string placeholderColorUrl = $"{_rtspBase}/{PlaceholderName}_color";
            string placeholderDepthUrl = $"{_rtspBase}/{PlaceholderName}_depth";
            string startupJson = OpenDibrStartupJson.Build(_outputWidth, _outputHeight, placeholderColorUrl, placeholderDepthUrl);
            string startupJsonPath = System.IO.Path.Combine(Application.temporaryCachePath, "opendibr_startup.json");
            System.IO.File.WriteAllText(startupJsonPath, startupJson);

            if (!_launcher.StartOpenDibr(startupJsonPath))
            {
                Debug.LogWarning("[OpenDibrSessionManager] OpenDIBR failed to launch — synthetic view unavailable this session.");
                return;
            }

            // A bad placeholder or malformed startup JSON makes OpenDIBR
            // exit immediately rather than hang (per opendibr-c3's
            // findings) — give its own startup validation a moment, then
            // confirm it's actually still running before trusting the
            // control channels below will ever connect.
            await Task.Delay(2000);
            if (!_launcher.IsOpenDibrRunning)
            {
                Debug.LogWarning("[OpenDibrSessionManager] OpenDIBR exited immediately after launch — check its startup JSON/placeholder stream.");
                return;
            }

            _openDibrControl = new OpenDibrControlChannel();
            _bridgeControl = new DibrBridgeControlChannel();
            _poseChannel = new OpenDibrPoseChannel();

            IsSessionStarted = true;
        }

        // Fetches calibration, ensures both cameras are added on both sides
        // (skipping ones already cached), and triggers a fresh pairwise depth
        // pipeline on the bridge — everything needed before OpenDIBR can
        // usefully render this pair. Never throws; failure is communicated
        // via the returned result so the caller (CameraSwitcher) can decide
        // between "retry" and "fall back to crossfade permanently for this
        // pair."
        public async Task<PrepareResult> PreparePairAsync(string camA, string camB, string serverUrl)
        {
            await EnsureSessionStartedAsync();
            if (!IsSessionStarted) return PrepareResult.Failed("DIBR session not available on this machine.");

            SceneCalibrationData calib;
            try
            {
                calib = await FetchCalibrationAsync(camA, camB, serverUrl);
            }
            catch (Exception e)
            {
                return PrepareResult.Failed($"Calibration fetch error: {e.Message}");
            }
            if (calib == null) return PrepareResult.NotCalibrated();

            if (calib.cam1.intrinsics.imageWidth != _outputWidth || calib.cam1.intrinsics.imageHeight != _outputHeight ||
                calib.cam2.intrinsics.imageWidth != _outputWidth || calib.cam2.intrinsics.imageHeight != _outputHeight)
            {
                return PrepareResult.Failed(
                    $"Camera resolution doesn't match OpenDIBR's fixed startup resolution " +
                    $"({_outputWidth}x{_outputHeight}) — cam1: {calib.cam1.intrinsics.imageWidth}x{calib.cam1.intrinsics.imageHeight}, " +
                    $"cam2: {calib.cam2.intrinsics.imageWidth}x{calib.cam2.intrinsics.imageHeight}");
            }

            try
            {
                var urlsA = await EnsureBridgeCameraAsync(camA);
                var urlsB = await EnsureBridgeCameraAsync(camB);

                // Bridge pairing MUST happen before EnsureOpenDibrCameraAsync
                // below — OpenDIBR's add_camera needs a concrete Depth_range
                // up front (confirmed by opendibr-c3: it's a per-camera field
                // on add_camera itself, not on set_active_pair), but depth
                // range is only knowable once the bridge actually computes
                // stereo depth for a specific pairing. Always re-triggered on
                // the bridge even for two already-added cameras — depth
                // output depends on the current partner.
                var depthRange = await _bridgeControl.SetActivePairAsync(camA, camB);

                // NOTE: a camera's Depth_range is captured here only the
                // FIRST time it's added to OpenDIBR (EnsureOpenDibrCameraAsync
                // is a no-op for an already-added identity) and held static
                // for the rest of the session, even though the bridge
                // recomputes a fresh, more accurate range on every
                // set_active_pair call. Accepted simplification — see
                // OpenDibrControlChannel.cs's AddCameraRequest.Depth_range
                // comment. Revisit only if this visibly degrades depth
                // quality for cameras re-paired with very different partners.
                await EnsureOpenDibrCameraAsync(camA, urlsA, calib.cam1.intrinsics, calib.cam1.extrinsics, depthRange.MinA, depthRange.MaxA);
                await EnsureOpenDibrCameraAsync(camB, urlsB, calib.cam2.intrinsics, calib.cam2.extrinsics, depthRange.MinB, depthRange.MaxB);

                await _openDibrControl.SetActivePairAsync(camA, camB);
            }
            catch (Exception e)
            {
                return PrepareResult.Failed($"DIBR pipeline setup failed: {e.Message}");
            }

            if (!FrameReceiver.IsRunning) FrameReceiver.TryStart();

            return PrepareResult.Ready(calib.cam1.extrinsics, calib.cam2.extrinsics);
        }

        public void StreamPose(Vector3 position, Quaternion rotation) => _poseChannel?.SendPose(position, rotation);

        private Task<SceneCalibrationData> FetchCalibrationAsync(string camA, string camB, string serverUrl)
        {
            var tcs = new TaskCompletionSource<SceneCalibrationData>();
            if (!CalibrationPairClient.HasInstance)
            {
                tcs.SetException(new InvalidOperationException("No CalibrationPairClient in scene."));
                return tcs.Task;
            }

            CalibrationPairClient.Instance.FetchPair(serverUrl, camA, camB,
                onFound: data => tcs.TrySetResult(data),
                onNotFound: () => tcs.TrySetResult(null),
                onError: err => tcs.TrySetException(new Exception(err)));
            return tcs.Task;
        }

        private async Task<DibrBridgeControlChannel.CameraUrls> EnsureBridgeCameraAsync(string identity)
        {
            if (_bridgeAdded.TryGetValue(identity, out var cached)) return cached;

            var urls = await _bridgeControl.AddCameraAsync(identity);
            EvictIfFull(_bridgeAdded, identity);
            _bridgeAdded[identity] = urls;
            return urls;
        }

        private async Task EnsureOpenDibrCameraAsync(string identity, DibrBridgeControlChannel.CameraUrls urls,
            IntrinsicsData intrinsics, ExtrinsicsData extrinsics, float depthMin, float depthMax)
        {
            if (_openDibrAddedOrder.Contains(identity)) return;

            if (_openDibrAddedOrder.Count >= _maxCachedCameras)
            {
                string evicted = _openDibrAddedOrder[0];
                _openDibrAddedOrder.RemoveAt(0);
                try { await _openDibrControl.RemoveCameraAsync(evicted); }
                catch (Exception e) { Debug.LogWarning($"[OpenDibrSessionManager] Evict of '{evicted}' failed: {e.Message}"); }
            }

            // This is the INPUT camera's own fixed physical pose (from
            // calibration — used for DIBR reprojection), NOT the live
            // virtual/output camera the pose channel (Item 2) moves during a
            // transition. Two distinct concepts: every input camera has one
            // static pose here; only the output viewpoint is driven live.
            OpenDibrPoseMath.ExtrinsicsToBoardPose(extrinsics, out var pos, out var rot);
            Vector3 rotationRodrigues = OpenDibrPoseMath.QuaternionToRodrigues(rot);

            // Fixed 8-bit convention on both ends — matches
            // dibr-bridge/rtsp_publisher.py's actual encoding (chosen for
            // RTSP/H.264 decode compatibility over HEVC Main10/12, per
            // opendibr-c3's confirmation). Not runtime-negotiated; see
            // AddCameraRequest's doc comment in OpenDibrControlChannel.cs.
            const int bitDepthColor = 8;
            const int bitDepthDepth = 8;

            await _openDibrControl.AddCameraAsync(
                identity, urls.ColorUrl, urls.DepthUrl,
                focal: new[] { intrinsics.fx, intrinsics.fy },
                principlePoint: new[] { intrinsics.cx, intrinsics.cy },
                position: new[] { pos.x, pos.y, pos.z },
                rotationRodrigues: new[] { rotationRodrigues.x, rotationRodrigues.y, rotationRodrigues.z },
                resolution: new[] { _outputWidth, _outputHeight },
                depthRange: new[] { depthMin, depthMax },
                bitDepthColor: bitDepthColor, bitDepthDepth: bitDepthDepth);
            _openDibrAddedOrder.Add(identity);
        }

        private static void EvictIfFull(Dictionary<string, DibrBridgeControlChannel.CameraUrls> map, string incoming)
        {
            // Bridge cache eviction isn't cache-size-critical the way
            // OpenDIBR's is (see DibrBridgeControlChannel doc comment — the
            // bridge doesn't keep N depth pipelines warm anyway, only ever
            // one active pair at a time), so this is a no-op placeholder for
            // now; left as an explicit method in case bridge-side per-camera
            // resource limits turn out to matter once Phase A exists.
        }

        private void OnDestroy()
        {
            _openDibrControl?.Dispose();
            _bridgeControl?.Dispose();
            _poseChannel?.Dispose();
            _launcher?.Dispose();
        }
    }
}
