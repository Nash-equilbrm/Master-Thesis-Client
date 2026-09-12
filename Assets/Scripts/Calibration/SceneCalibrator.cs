using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Thesis.Managers;

namespace Thesis.Calibration {

    /// <summary>
    /// Coordinates calibration for two cameras and exports calibration.json.
    ///
    /// Workflow:
    /// 0. Fetch the server-provided ChArUco BoardConfig and Configure() both calibrators
    /// 1. Load cam1 intrinsics video → Process → Calibrate Intrinsics
    /// 2. Load cam2 intrinsics video → Process → Calibrate Intrinsics
    /// 3. Pause both videos on a frame where the board is visible → Capture Extrinsics
    /// 4. Export JSON → calibration.json written to Application.persistentDataPath
    /// </summary>
    public class SceneCalibrator : MonoBehaviour {

        [Header("Calibrators")]
        public CameraCalibrator cam1Calibrator;
        public CameraCalibrator cam2Calibrator;

        [Header("Depth Range (set manually based on your scene)")]
        public float depthMin = 0.3f;
        public float depthMax = 5.0f;

        [Header("Output")]
        public string outputFileName = "calibration.json";
        public TextMeshProUGUI outputPathText;
        public TextMeshProUGUI globalStatusText;

        [Header("UI Buttons")]
        public Button processCam1Button;
        public Button stopCam1Button;
        public Button calibrateCam1Button;
        public Button processCam2Button;
        public Button stopCam2Button;
        public Button calibrateCam2Button;
        public Button captureExtrinsicsButton;
        public Button exportButton;

        private SceneCalibrationData _data = new();

        private void Start() {
            processCam1Button?.onClick.AddListener(() => cam1Calibrator.StartProcessingVideo());
            stopCam1Button?.onClick.AddListener(() => cam1Calibrator.StopProcessing());
            calibrateCam1Button?.onClick.AddListener(() => cam1Calibrator.CalibrateIntrinsics());

            processCam2Button?.onClick.AddListener(() => cam2Calibrator.StartProcessingVideo());
            stopCam2Button?.onClick.AddListener(() => cam2Calibrator.StopProcessing());
            calibrateCam2Button?.onClick.AddListener(() => cam2Calibrator.CalibrateIntrinsics());

            captureExtrinsicsButton?.onClick.AddListener(CaptureExtrinsics);
            exportButton?.onClick.AddListener(ExportCalibration);

            cam1Calibrator.OnIntrinsicsCalibrated += i => _data.cam1.intrinsics = i;
            cam2Calibrator.OnIntrinsicsCalibrated += i => _data.cam2.intrinsics = i;
            cam1Calibrator.OnExtrinsicsEstimated += e => _data.cam1.extrinsics = e;
            cam2Calibrator.OnExtrinsicsEstimated += e => _data.cam2.extrinsics = e;

            SetCalibrationButtonsInteractable(false);
            if (globalStatusText) globalStatusText.text = "Fetching board configuration…";

            if (!CalibrationConfigClient.HasInstance) {
                Debug.LogWarning("[SceneCalibrator] No CalibrationConfigClient in scene — using default board config.");
                ApplyBoardConfig(BoardConfig.Default);
                return;
            }

            CalibrationConfigClient.Instance.OnConfigReady += ApplyBoardConfig;
            CalibrationConfigClient.Instance.FetchConfig(Thesis.AppConfig.ServerUrl);
        }

        private void OnDestroy() {
            if (CalibrationConfigClient.HasInstance)
                CalibrationConfigClient.Instance.OnConfigReady -= ApplyBoardConfig;
        }

        private void ApplyBoardConfig(BoardConfig config) {
            cam1Calibrator.Configure(config);
            cam2Calibrator.Configure(config);
            SetCalibrationButtonsInteractable(true);
            if (globalStatusText) globalStatusText.text = "Board config ready — begin calibration.";
        }

        private void SetCalibrationButtonsInteractable(bool value) {
            if (processCam1Button) processCam1Button.interactable = value;
            if (calibrateCam1Button) calibrateCam1Button.interactable = value;
            if (processCam2Button) processCam2Button.interactable = value;
            if (calibrateCam2Button) calibrateCam2Button.interactable = value;
            if (captureExtrinsicsButton) captureExtrinsicsButton.interactable = value;
        }

        private void CaptureExtrinsics() {
            // Both videos should be paused on a frame where the board is clearly visible
            cam1Calibrator.StopProcessing();
            cam2Calibrator.StopProcessing();

            bool ok1 = cam1Calibrator.EstimatePoseFromCurrentFrame();
            bool ok2 = cam2Calibrator.EstimatePoseFromCurrentFrame();

            string msg = $"Extrinsics — cam1: {(ok1 ? "OK" : "FAILED")}  cam2: {(ok2 ? "OK" : "FAILED")}";
            if (globalStatusText) globalStatusText.text = msg;
            Debug.Log($"[SceneCalibrator] {msg}");
        }

        private void ExportCalibration() {
            if (!cam1Calibrator.IsCalibrated || !cam2Calibrator.IsCalibrated) {
                string warn = "Both cameras must be calibrated before exporting.";
                if (globalStatusText) globalStatusText.text = warn;
                Debug.LogWarning($"[SceneCalibrator] {warn}");
                return;
            }

            _data.depthMin = depthMin;
            _data.depthMax = depthMax;

            string path = Path.Combine(Application.persistentDataPath, outputFileName);
            File.WriteAllText(path, _data.ToJson());

            if (outputPathText) outputPathText.text = path;
            if (globalStatusText) globalStatusText.text = $"Saved: {path}";
            Debug.Log($"[SceneCalibrator] Saved to {path}");
        }
    }
}
