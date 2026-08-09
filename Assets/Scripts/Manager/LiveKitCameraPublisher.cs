using System;
using System.Collections;
using LiveKit;
using LiveKit.Proto;
using Thesis.Patterns;
using UnityEngine;

#if PLATFORM_ANDROID
using UnityEngine.Android;
#endif

namespace Thesis.Managers
{
    public class LiveKitCameraPublisher : Singleton<LiveKitCameraPublisher>
    {
        private const string VideoTrackName = "camera-track";

        [SerializeField] private int _frameRate = 30;
        [SerializeField] private int _maxBitrate = 512000;

        private Room _room;
        private WebCamTexture _webCamTexture;
        private WebCameraSource _cameraSource;
        private LocalVideoTrack _localVideoTrack;

        public bool IsConnected => _room?.IsConnected ?? false;
        public bool IsPublishing { get; private set; }

        public event Action OnConnected;
        public event Action OnDisconnected;
        public event Action OnPublishingStarted;
        public event Action<string> OnConnectionFailed;

        private void OnDestroy() => Cleanup();

        private void OnApplicationPause(bool pause)
        {
            if (_webCamTexture == null) return;
            if (pause) _webCamTexture.Pause();
            else _webCamTexture.Play();
        }

        public void BeginStreaming()
        {
            StartCoroutine(StartStreamingRoutine());
        }

        private IEnumerator StartStreamingRoutine()
        {
            yield return StartCoroutine(OpenCamera());
            if (_webCamTexture == null || !_webCamTexture.isPlaying) yield break;

            yield return StartCoroutine(Connect());
            if (_room == null) yield break; // Connect() nulls _room on failure; IsConnected is unreliable here because ConnectionState updates asynchronously via RoomEvent

            yield return StartCoroutine(PublishCamera());
        }

        #region Camera

        private IEnumerator OpenCamera()
        {
            RequestCameraPermissionIfNeeded();

            yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                Debug.LogError("[LiveKitCameraPublisher] Camera permission denied.");
                OnConnectionFailed?.Invoke("Camera permission denied.");
                yield break;
            }

            yield return WaitForCameraDevices();
            if (WebCamTexture.devices.Length == 0)
            {
                Debug.LogError("[LiveKitCameraPublisher] No camera device found.");
                OnConnectionFailed?.Invoke("No camera device found.");
                yield break;
            }

            var device = WebCamTexture.devices[0];
            var (width, height) = GetCameraResolution();
            _webCamTexture = new WebCamTexture(device.name, width, height, _frameRate)
            {
                wrapMode = TextureWrapMode.Repeat
            };
            _webCamTexture.Play();
            Debug.Log($"[LiveKitCameraPublisher] Camera opened: {device.name}");
        }

        private static IEnumerator WaitForCameraDevices()
        {
            for (int i = 0; i < 300 && WebCamTexture.devices.Length == 0; i++)
                yield return new WaitForEndOfFrame();
        }

        private static (int width, int height) GetCameraResolution()
        {
            return Screen.height > Screen.width
                ? (Screen.height, Screen.width)
                : (Screen.width, Screen.height);
        }

        private static void RequestCameraPermissionIfNeeded()
        {
#if PLATFORM_ANDROID
            if (!Permission.HasUserAuthorizedPermission(Permission.Camera))
                Permission.RequestUserPermission(Permission.Camera);
#endif
        }

        #endregion

        #region Connection

        private IEnumerator Connect()
        {
            var reg = RegistrationClient.Instance;
            _room = new Room();
            _room.Disconnected += HandleDisconnected;

            var options = new LiveKit.RoomOptions();
            var connect = _room.Connect(reg.LiveKitUrl, reg.Token, options);
            yield return connect;

            if (connect.IsError)
            {
                Debug.LogError("[LiveKitCameraPublisher] Failed to connect to LiveKit.");
                OnConnectionFailed?.Invoke("Failed to connect to LiveKit server.");
                _room = null;
                yield break;
            }

            Debug.Log($"[LiveKitCameraPublisher] Connected as {reg.Identity}");
            OnConnected?.Invoke();
        }

        private void HandleDisconnected(Room room)
        {
            Debug.Log("[LiveKitCameraPublisher] Disconnected.");
            IsPublishing = false;
            OnDisconnected?.Invoke();
        }

        #endregion

        #region Publishing

        private IEnumerator PublishCamera()
        {
            _cameraSource = new WebCameraSource(_webCamTexture);
            _localVideoTrack = LocalVideoTrack.CreateVideoTrack(VideoTrackName, _cameraSource, _room);

            // Start feeding frames before PublishTrack — the SDK needs frames during
            // WebRTC SDP negotiation; calling Start() after yield return publish deadlocks.
            _cameraSource.Start();
            StartCoroutine(_cameraSource.Update());

            var options = new TrackPublishOptions
            {
                VideoCodec = VideoCodec.Vp8,
                VideoEncoding = new VideoEncoding
                {
                    MaxBitrate = (ulong)_maxBitrate,
                    MaxFramerate = (uint)_frameRate
                },
                Simulcast = false,
                Source = TrackSource.SourceCamera
            };

            var publish = _room.LocalParticipant.PublishTrack(_localVideoTrack, options);
            yield return publish;

            if (publish.IsError)
            {
                Debug.LogError("[LiveKitCameraPublisher] Failed to publish camera track.");
                OnConnectionFailed?.Invoke("Failed to publish camera track.");
                yield break;
            }

            IsPublishing = true;
            Debug.Log("[LiveKitCameraPublisher] Camera track published.");
            OnPublishingStarted?.Invoke();
        }

        #endregion

        #region Cleanup

        private void Cleanup()
        {
            if (_cameraSource != null)
            {
                _cameraSource.Stop();
                _cameraSource.Dispose();
                _cameraSource = null;
            }

            _webCamTexture?.Stop();
            _webCamTexture = null;

            if (_room != null)
            {
                if (IsPublishing && _localVideoTrack != null)
                    _room.LocalParticipant.UnpublishTrack(_localVideoTrack, false);
                _room.Disconnect();
                _room = null;
            }

            IsPublishing = false;
        }

        #endregion
    }
}
