using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Thesis.Calibration;

namespace Thesis.DIBR {

    /// <summary>
    /// Manages the DIBR test scene.
    ///
    /// Scene setup (assign in Inspector):
    ///   - cam1Source / cam2Source: VideoFileSource components with color+depth video paths
    ///   - cam1Mesh  / cam2Mesh  : DepthMeshBuilder components, children of cam1Transform/cam2Transform
    ///   - cam1Transform / cam2Transform: GameObjects placed to match real camera positions
    ///   - virtualCamera: Camera that renders the synthesized view, moved by the slider
    ///
    /// The virtualCamera renders both depth meshes from an interpolated position between
    /// cam1Transform and cam2Transform.  Mesh alpha is cross-faded based on the slider
    /// to blend coverage from each camera.
    ///
    /// Calibration JSON (optional): load to apply correct intrinsics to depth meshes.
    /// Without it, default intrinsics in DepthMeshBuilder are used.
    /// </summary>
    public class DIBRSceneManager : MonoBehaviour {

        [Header("Frame Sources")]
        public VideoFileSource cam1Source;
        public VideoFileSource cam2Source;

        [Header("Depth Meshes")]
        public DepthMeshBuilder cam1Mesh;
        public DepthMeshBuilder cam2Mesh;

        [Header("Virtual Camera")]
        public Camera virtualCamera;
        public Transform cam1Transform;
        public Transform cam2Transform;

        [Header("Calibration")]
        [Tooltip("Absolute path to calibration.json exported from CalibrationScene.")]
        public string calibrationJsonPath;

        [Header("UI")]
        public RawImage outputDisplay;
        public RawImage cam1Preview;
        public RawImage cam2Preview;
        [Range(0f, 1f)] public float virtualCameraT = 0.5f;
        public Slider virtualCameraSlider;
        public Button loadButton;
        public Button playPauseButton;
        public TextMeshProUGUI statusText;

        private RenderTexture _outputRT;
        private bool _isPlaying;
        private int _readySources;

        private void Start() {
            _outputRT = new RenderTexture(1280, 720, 24);
            virtualCamera.targetTexture = _outputRT;
            if (outputDisplay) outputDisplay.texture = _outputRT;

            loadButton?.onClick.AddListener(LoadAll);
            playPauseButton?.onClick.AddListener(TogglePlayPause);
            virtualCameraSlider?.onValueChanged.AddListener(v => virtualCameraT = v);
            if (virtualCameraSlider) virtualCameraSlider.value = virtualCameraT;
        }

        private void LoadAll() {
            LoadCalibration();

            _readySources = 0;
            cam1Source.OnReady += OnSourceReady;
            cam2Source.OnReady += OnSourceReady;
            cam1Source.Load();
            cam2Source.Load();
            SetStatus("Loading videos…");
        }

        private void OnSourceReady() {
            if (++_readySources < 2) return;

            // Wire color previews
            if (cam1Preview) cam1Preview.texture = cam1Source.ColorTexture;
            if (cam2Preview) cam2Preview.texture = cam2Source.ColorTexture;

            SetStatus("Ready — press Play.");
        }

        private void LoadCalibration() {
            if (string.IsNullOrEmpty(calibrationJsonPath) || !File.Exists(calibrationJsonPath)) {
                SetStatus("No calibration file — using default intrinsics.");
                return;
            }
            var data = SceneCalibrationData.FromJson(File.ReadAllText(calibrationJsonPath));
            cam1Mesh.ApplyCalibration(data.cam1.intrinsics, data.depthMin, data.depthMax);
            cam2Mesh.ApplyCalibration(data.cam2.intrinsics, data.depthMin, data.depthMax);
            SetStatus("Calibration loaded.");
        }

        private void TogglePlayPause() {
            if (_isPlaying) {
                cam1Source.Pause();
                cam2Source.Pause();
                SetStatus("Paused.");
            } else {
                cam1Source.Play();
                cam2Source.Play();
                SetStatus("Playing…");
            }
            _isPlaying = !_isPlaying;

            if (playPauseButton)
                playPauseButton.GetComponentInChildren<TextMeshProUGUI>()
                    .text = _isPlaying ? "Pause" : "Play";
        }

        private void Update() {
            if (!_isPlaying) return;
            if (!cam1Source.IsReady || !cam2Source.IsReady) return;

            // Rebuild depth meshes from current depth frames
            cam1Mesh.UpdateMesh(cam1Source.DepthTexture);
            cam2Mesh.UpdateMesh(cam2Source.DepthTexture);

            // Apply color textures
            cam1Mesh.ColorTexture = cam1Source.ColorTexture;
            cam2Mesh.ColorTexture = cam2Source.ColorTexture;

            // Position virtual camera (lerp position, slerp rotation)
            virtualCamera.transform.position = Vector3.Lerp(
                cam1Transform.position, cam2Transform.position, virtualCameraT);
            virtualCamera.transform.rotation = Quaternion.Slerp(
                cam1Transform.rotation, cam2Transform.rotation, virtualCameraT);

            // Cross-fade mesh transparency: cam1 fades out as slider moves toward cam2
            SetMeshAlpha(cam1Mesh, 1f - virtualCameraT);
            SetMeshAlpha(cam2Mesh, virtualCameraT);
        }

        private static void SetMeshAlpha(DepthMeshBuilder mesh, float alpha) {
            var mat = mesh.GetComponent<MeshRenderer>().material;
            if (mat.HasProperty("_BaseColor")) {
                var c = mat.GetColor("_BaseColor");
                c.a = alpha;
                mat.SetColor("_BaseColor", c);
            } else if (mat.HasProperty("_Color")) {
                var c = mat.color;
                c.a = alpha;
                mat.color = c;
            }
        }

        private void SetStatus(string msg) {
            if (statusText) statusText.text = msg;
            Debug.Log($"[DIBRSceneManager] {msg}");
        }

        private void OnDestroy() {
            _outputRT?.Release();
        }
    }
}
