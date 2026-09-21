using System;
using System.Collections;
using System.IO;
using LiveKit;
using Thesis.Calibration;
using Thesis.Managers;
using Thesis.Stream;
using UnityEngine;

namespace Thesis.Dibr
{
    // One-off diagnostic tool, NOT part of the live DIBR pipeline: on every
    // camera switch, independently grabs one raw frame from each of the two
    // cameras plus their pair calibration JSON, and dumps all three to disk
    // for offline testing of dibr-bridge's StereoDepthComputer (see
    // Master-Thesis-Reports' Sep 21st depth-debugging log). Runs its own
    // VideoStream per camera rather than reusing CameraSwitcher/
    // CameraStreamPlayer's — deliberately decoupled so a failing/timing-out
    // DIBR prepare pipeline never blocks or skews this capture.
    public class DibrDepthTestCapture : MonoBehaviour
    {
        [SerializeField] private CameraSwitcher _cameraSwitcher;
        [SerializeField] private float _frameTimeoutSeconds = 10f;
        [SerializeField] private float _calibrationTimeoutSeconds = 10f;

        private bool _capturing;

        private void Awake()
        {
            if (_cameraSwitcher == null)
#if UNITY_2023_1_OR_NEWER
                _cameraSwitcher = FindFirstObjectByType<CameraSwitcher>();
#else
                _cameraSwitcher = FindObjectOfType<CameraSwitcher>();
#endif
        }

        private void OnEnable()
        {
            if (_cameraSwitcher != null) _cameraSwitcher.OnSwitchStarted += OnSwitchStarted;
        }

        private void OnDisable()
        {
            if (_cameraSwitcher != null) _cameraSwitcher.OnSwitchStarted -= OnSwitchStarted;
        }

        private void OnSwitchStarted(string camA, string camB)
        {
            if (_capturing)
            {
                Debug.LogWarning($"[DibrDepthTestCapture] Already capturing — ignoring {camA}→{camB}.");
                return;
            }
            StartCoroutine(CapturePairRoutine(camA, camB));
        }

        private IEnumerator CapturePairRoutine(string camA, string camB)
        {
            _capturing = true;
            Debug.Log($"[DibrDepthTestCapture] Capturing {camA} + {camB}...");

            if (!LiveKitManager.HasInstance
                || !LiveKitManager.Instance.VideoTracks.TryGetValue(camA, out var pubA)
                || !LiveKitManager.Instance.VideoTracks.TryGetValue(camB, out var pubB))
            {
                Debug.LogWarning("[DibrDepthTestCapture] Missing video track publication for one or both cameras — aborting.");
                _capturing = false;
                yield break;
            }

            Texture2D texA = null, texB = null;
            yield return StartCoroutine(SubscribeAndCapture(camA, pubA, t => texA = t));
            yield return StartCoroutine(SubscribeAndCapture(camB, pubB, t => texB = t));

            if (texA == null || texB == null)
            {
                Debug.LogWarning($"[DibrDepthTestCapture] Capture failed — no frame for " +
                                  $"{(texA == null ? camA : camB)}. Nothing saved.");
                _capturing = false;
                yield break;
            }

            SceneCalibrationData calib = null;
            string calibError = null;
            bool calibDone = false;
            if (!CalibrationPairClient.HasInstance)
            {
                calibError = "No CalibrationPairClient in scene.";
                calibDone = true;
            }
            else
            {
                CalibrationPairClient.Instance.FetchPair(Thesis.AppConfig.ServerUrl, camA, camB,
                    onFound: data => { calib = data; calibDone = true; },
                    onNotFound: () => { calibError = "No calibration found for this pair."; calibDone = true; },
                    onError: err => { calibError = err; calibDone = true; });
            }

            float waited = 0f;
            while (!calibDone && waited < _calibrationTimeoutSeconds)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            string dir = BuildOutputDir(camA, camB);
            Directory.CreateDirectory(dir);
            File.WriteAllBytes(Path.Combine(dir, $"{camA}.png"), texA.EncodeToPNG());
            File.WriteAllBytes(Path.Combine(dir, $"{camB}.png"), texB.EncodeToPNG());
            Destroy(texA);
            Destroy(texB);

            if (calib != null)
            {
                File.WriteAllText(Path.Combine(dir, "calibration.json"), calib.ToJson());
                Debug.Log($"[DibrDepthTestCapture] Saved capture (frames + calibration) to {dir}");
            }
            else
            {
                Debug.LogWarning($"[DibrDepthTestCapture] Calibration unavailable ({calibError ?? "timed out"}) " +
                                  $"— frames saved without calibration.json at {dir}");
            }

            _capturing = false;
        }

        // Independently subscribes to `identity`'s video track (reusing an
        // existing subscription if the track is already flowing, e.g. the
        // outgoing camera being displayed) and waits for exactly one decoded
        // frame, without touching the display RawImage or unsubscribing
        // afterwards — this is additive-only so it can never interrupt the
        // real crossfade/DIBR path running concurrently.
        private IEnumerator SubscribeAndCapture(string identity, RemoteTrackPublication pub, Action<Texture2D> onCaptured)
        {
            IRemoteTrack track = pub.Track;
            if (track == null)
            {
                pub.SetSubscribed(true);
                bool subscribed = false;
                void Handler(IRemoteTrack t, RemoteTrackPublication p, RemoteParticipant participant)
                {
                    if (p != pub) return;
                    track = t;
                    subscribed = true;
                }

                LiveKitManager.Instance.OnTrackSubscribed += Handler;
                float waited = 0f;
                while (!subscribed && waited < _frameTimeoutSeconds)
                {
                    waited += Time.deltaTime;
                    yield return null;
                }
                LiveKitManager.Instance.OnTrackSubscribed -= Handler;

                if (!subscribed)
                {
                    Debug.LogWarning($"[DibrDepthTestCapture] Timed out waiting for '{identity}' track subscription.");
                    yield break;
                }
            }

            if (track is not RemoteVideoTrack videoTrack)
            {
                Debug.LogWarning($"[DibrDepthTestCapture] '{identity}' track is not a video track.");
                yield break;
            }

            var videoStream = new VideoStream(videoTrack);
            Texture receivedTex = null;
            videoStream.TextureReceived += tex => receivedTex = tex;
            videoStream.Start();
            var updateRoutine = StartCoroutine(videoStream.Update());

            float frameWait = 0f;
            while (receivedTex == null && frameWait < _frameTimeoutSeconds)
            {
                frameWait += Time.deltaTime;
                yield return null;
            }

            Texture2D snapshot = null;
            if (receivedTex is RenderTexture rt)
            {
                // One extra frame so the just-created RenderTexture has a
                // fully uploaded frame in it before we read it back.
                yield return null;
                snapshot = ReadRenderTexture(rt);
            }
            else if (receivedTex == null)
            {
                Debug.LogWarning($"[DibrDepthTestCapture] No frame received for '{identity}' within {_frameTimeoutSeconds}s.");
            }

            StopCoroutine(updateRoutine);
            videoStream.Stop();
            videoStream.Dispose();

            onCaptured(snapshot);
        }

        private static Texture2D ReadRenderTexture(RenderTexture rt)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex2D = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex2D.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex2D.Apply();
            RenderTexture.active = previous;
            return tex2D;
        }

        private static string BuildOutputDir(string camA, string camB)
        {
            // Workspace root (sibling of this repo, dibr-bridge, OpenDIBR,
            // etc. — see the workspace CLAUDE.md), not inside Assets, so
            // Unity never imports these as assets and it's a neutral
            // hand-off location for the bridge repo.
            string workspaceRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            return Path.Combine(workspaceRoot, "DibrDepthTestCaptures", $"{camA}_{camB}_{timestamp}");
        }
    }
}
