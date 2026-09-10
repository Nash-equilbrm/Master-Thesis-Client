using System;
using UnityEngine;
using UnityEngine.Video;

namespace Thesis.DIBR {

    /// <summary>
    /// Provides color and depth RenderTextures from two synchronized VideoPlayer instances.
    /// Color and depth videos must have the same resolution and frame count.
    /// </summary>
    public class VideoFileSource : MonoBehaviour {

        [Header("Video File Paths (absolute or StreamingAssets-relative)")]
        public string colorVideoPath;
        public string depthVideoPath;

        public RenderTexture ColorTexture { get; private set; }
        public RenderTexture DepthTexture { get; private set; }
        public bool IsReady { get; private set; }
        public int Width => ColorTexture ? ColorTexture.width : 0;
        public int Height => ColorTexture ? ColorTexture.height : 0;

        public event Action OnReady;

        private VideoPlayer _colorPlayer;
        private VideoPlayer _depthPlayer;
        private int _readyCount;

        private void Awake() {
            _colorPlayer = CreatePlayer("ColorPlayer");
            _depthPlayer = CreatePlayer("DepthPlayer");
        }

        private VideoPlayer CreatePlayer(string goName) {
            var go = new GameObject(goName);
            go.transform.SetParent(transform);
            var vp = go.AddComponent<VideoPlayer>();
            vp.renderMode = VideoRenderMode.RenderTexture;
            vp.audioOutputMode = VideoAudioOutputMode.None;
            vp.isLooping = true;
            vp.playOnAwake = false;
            return vp;
        }

        public void Load() {
            if (string.IsNullOrEmpty(colorVideoPath) || string.IsNullOrEmpty(depthVideoPath)) {
                Debug.LogError($"[VideoFileSource:{name}] Video paths not set.");
                return;
            }

            _readyCount = 0;
            IsReady = false;

            _colorPlayer.url = colorVideoPath;
            _depthPlayer.url = depthVideoPath;

            _colorPlayer.prepareCompleted += OnColorReady;
            _depthPlayer.prepareCompleted += OnDepthReady;

            _colorPlayer.Prepare();
            _depthPlayer.Prepare();
        }

        private void OnColorReady(VideoPlayer vp) {
            vp.prepareCompleted -= OnColorReady;
            ColorTexture = new RenderTexture((int)vp.width, (int)vp.height, 0, RenderTextureFormat.ARGB32);
            vp.targetTexture = ColorTexture;
            CheckBothReady();
        }

        private void OnDepthReady(VideoPlayer vp) {
            vp.prepareCompleted -= OnDepthReady;
            DepthTexture = new RenderTexture((int)vp.width, (int)vp.height, 0, RenderTextureFormat.ARGB32);
            vp.targetTexture = DepthTexture;
            CheckBothReady();
        }

        private void CheckBothReady() {
            if (++_readyCount < 2) return;
            IsReady = true;
            Debug.Log($"[VideoFileSource:{name}] Ready {Width}x{Height}");
            OnReady?.Invoke();
        }

        public void Play() {
            _colorPlayer.Play();
            _depthPlayer.Play();
        }

        public void Pause() {
            _colorPlayer.Pause();
            _depthPlayer.Pause();
        }

        public void Stop() {
            _colorPlayer.Stop();
            _depthPlayer.Stop();
        }

        /// <summary>Snaps this source's playback time to match another source.</summary>
        public void SyncTimeTo(VideoFileSource other) {
            double t = other._colorPlayer.time;
            _colorPlayer.time = t;
            _depthPlayer.time = t;
        }

        private void OnDestroy() {
            ColorTexture?.Release();
            DepthTexture?.Release();
        }
    }
}
