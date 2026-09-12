using System;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Thesis.Calibration;
using Thesis.Managers;

namespace Thesis.UI.Screens
{
    // Guided, auto-advancing live calibration: print the marker, then intrinsics
    // (move the board around), then extrinsics (hold it steady) — each step reads
    // frames straight from LiveKitCameraPublisher's live WebCamTexture, the same
    // one already open for the self-preview, rather than opening a second camera
    // handle. Completing uploads this device's result to the server.
    public class CalibrationTutorialScreen : BaseScreen
    {
        private enum Step { Marker, Intrinsics, Extrinsics }

        [Header("Panels")]
        [SerializeField] private GameObject _markerPanel;
        [SerializeField] private GameObject _livePanel;

        [Header("Marker Step")]
        [SerializeField] private RawImage _markerPreview;
        [SerializeField] private Button _downloadButton;
        [SerializeField] private Button _continueButton;
        [SerializeField] private TMP_Text _markerStatusText;

        [Header("Live Step (Intrinsics / Extrinsics)")]
        [SerializeField] private RawImage _cameraPreview;
        [SerializeField] private AspectRatioFitter _aspectFitter;
        [SerializeField] private TMP_Text _instructionText;
        [SerializeField] private TMP_Text _progressText;

        [Header("Calibrator")]
        [SerializeField] private CameraCalibrator _calibrator;

        private const int CaptureEveryNFrames = 5;

        public event Action OnCalibrationComplete;

        private Step _step;
        private Texture2D _markerTexture;
        private RenderTexture _scratchRT;
        private int _frameCounter;
        private bool _uploadInFlight;

        public override void Init()
        {
            base.Init();
            if (_downloadButton != null) _downloadButton.onClick.AddListener(OnDownloadClicked);
            if (_continueButton != null) _continueButton.onClick.AddListener(OnContinueClicked);
        }

        public override void Show(object data)
        {
            base.Show(data);
            _uploadInFlight = false;
            SetStep(Step.Marker);

            if (CalibrationConfigClient.HasInstance)
            {
                if (CalibrationConfigClient.Instance.Config != null)
                    OnConfigReady(CalibrationConfigClient.Instance.Config);
                else
                {
                    CalibrationConfigClient.Instance.OnConfigReady += OnConfigReady;
                    CalibrationConfigClient.Instance.FetchConfig(Thesis.AppConfig.ServerUrl);
                }
            }
            else
            {
                SetMarkerStatus("No CalibrationConfigClient in scene.");
            }
        }

        public override void Hide(Action onComplete = null)
        {
            if (CalibrationConfigClient.HasInstance)
                CalibrationConfigClient.Instance.OnConfigReady -= OnConfigReady;
            UnsubscribeUpload();

            if (_cameraPreview != null) _cameraPreview.texture = null;
            ReleaseScratchRT();
            base.Hide(onComplete);
        }

        private void Update()
        {
            if (_step != Step.Intrinsics && _step != Step.Extrinsics) return;
            if (_calibrator == null || !_calibrator.IsConfigured) return;

            var webcam = LiveKitCameraPublisher.HasInstance ? LiveKitCameraPublisher.Instance.Texture : null;
            if (webcam == null || !webcam.isPlaying || !webcam.didUpdateThisFrame) return;

            EnsureScratchRT(webcam.width, webcam.height);
            Graphics.Blit(webcam, _scratchRT);

            if (_step == Step.Intrinsics)
                TickIntrinsics();
            else
                TickExtrinsics();
        }

        // ── Step 0 — marker ──────────────────────────────────────────────────────

        private void OnConfigReady(BoardConfig config)
        {
            if (_calibrator != null) _calibrator.Configure(config);
            ShowMarkerTexture(config);
        }

        private void ShowMarkerTexture(BoardConfig config)
        {
            byte[] png = CharucoBoardGenerator.GeneratePng(config);
            if (_markerTexture != null) Destroy(_markerTexture);
            _markerTexture = new Texture2D(2, 2);
            _markerTexture.LoadImage(png);
            if (_markerPreview != null) _markerPreview.texture = _markerTexture;
            SetMarkerStatus("");
        }

        private void OnDownloadClicked()
        {
            if (_markerTexture == null) return;

            try
            {
                byte[] png = _markerTexture.EncodeToPNG();
                string path = Path.Combine(Application.persistentDataPath, "CalibrationBoard.png");
                File.WriteAllBytes(path, png);
                SetMarkerStatus($"Saved: {path}");
            }
            catch (Exception e)
            {
                SetMarkerStatus($"Failed to save: {e.Message}");
            }
        }

        private void OnContinueClicked()
        {
            if (_calibrator == null || !_calibrator.IsConfigured)
            {
                SetMarkerStatus("Still fetching board configuration…");
                return;
            }
            SetStep(Step.Intrinsics);
        }

        private void SetMarkerStatus(string msg)
        {
            if (_markerStatusText != null) _markerStatusText.text = msg;
        }

        // ── Steps 1/2 — live intrinsics / extrinsics ────────────────────────────

        private void SetStep(Step step)
        {
            _step = step;
            if (_markerPanel != null) _markerPanel.SetActive(step == Step.Marker);
            if (_livePanel != null) _livePanel.SetActive(step != Step.Marker);

            if (step == Step.Intrinsics)
            {
                _frameCounter = 0;
                _calibrator?.ResetAccumulatedFrames();
                SetupLivePreview();
                if (_instructionText != null)
                    _instructionText.text = "Move the board around your camera's view — tilt and rotate it.";
                UpdateProgressText();
            }
            else if (step == Step.Extrinsics)
            {
                if (_instructionText != null)
                    _instructionText.text = "Hold the board steady, fully visible, and stay still.";
                if (_progressText != null)
                    _progressText.text = "";
            }
        }

        private void TickIntrinsics()
        {
            _frameCounter++;
            if (_frameCounter % CaptureEveryNFrames != 0) return;

            _calibrator.ProcessLiveFrame(_scratchRT);
            UpdateProgressText();

            if (_calibrator.AccumulatedFrames >= _calibrator.minFramesForCalibration)
            {
                _calibrator.CalibrateIntrinsics();
                if (_calibrator.IsCalibrated)
                    SetStep(Step.Extrinsics);
            }
        }

        private void TickExtrinsics()
        {
            if (_calibrator.EstimatePoseFromTexture(_scratchRT))
                CompleteCalibration();
        }

        private void UpdateProgressText()
        {
            if (_progressText == null || _calibrator == null) return;
            _progressText.text = $"{_calibrator.AccumulatedFrames}/{_calibrator.minFramesForCalibration} frames";
        }

        private void SetupLivePreview()
        {
            var texture = LiveKitCameraPublisher.HasInstance ? LiveKitCameraPublisher.Instance.Texture : null;
            if (_cameraPreview == null || texture == null) return;

            _cameraPreview.texture = texture;

            var rt = _cameraPreview.rectTransform;
            rt.localEulerAngles = new Vector3(0f, 0f, -texture.videoRotationAngle);
            rt.localScale = new Vector3(rt.localScale.x, texture.videoVerticallyMirrored ? -1f : 1f, 1f);

            if (_aspectFitter != null && texture.height > 0)
            {
                bool rotated = texture.videoRotationAngle == 90 || texture.videoRotationAngle == 270;
                float width  = rotated ? texture.height : texture.width;
                float height = rotated ? texture.width  : texture.height;
                _aspectFitter.aspectRatio = width / height;
            }
        }

        private void EnsureScratchRT(int w, int h)
        {
            if (_scratchRT != null && _scratchRT.width == w && _scratchRT.height == h) return;
            ReleaseScratchRT();
            _scratchRT = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32);
        }

        private void ReleaseScratchRT()
        {
            if (_scratchRT == null) return;
            _scratchRT.Release();
            Destroy(_scratchRT);
            _scratchRT = null;
        }

        // ── Completion / upload ─────────────────────────────────────────────────

        private void CompleteCalibration()
        {
            if (_uploadInFlight) return;
            _uploadInFlight = true;

            if (_instructionText != null) _instructionText.text = "Uploading calibration…";

            if (!CalibrationUploadClient.HasInstance)
            {
                Debug.LogWarning("[CalibrationTutorialScreen] No CalibrationUploadClient in scene — skipping upload.");
                OnCalibrationComplete?.Invoke();
                return;
            }

            string identity = RegistrationClient.HasInstance ? RegistrationClient.Instance.Identity : null;
            string cameraName = LiveKitCameraPublisher.HasInstance && !string.IsNullOrEmpty(LiveKitCameraPublisher.Instance.Label)
                ? LiveKitCameraPublisher.Instance.Label
                : identity;

            var upload = CalibrationUploadClient.Instance;
            upload.ServerUrl = Thesis.AppConfig.ServerUrl;
            upload.OnUploaded += HandleUploaded;
            upload.OnUploadFailed += HandleUploadFailed;
            upload.Upload(identity, cameraName, _calibrator.Intrinsics, _calibrator.Extrinsics);
        }

        private void HandleUploaded()
        {
            UnsubscribeUpload();
            OnCalibrationComplete?.Invoke();
        }

        private void HandleUploadFailed(string error)
        {
            UnsubscribeUpload();

            if (Thesis.AppConfig.DevSkipCalibrationUpload)
            {
                Debug.LogWarning($"[CalibrationTutorialScreen] Upload failed ({error}) — " +
                                  "DevSkipCalibrationUpload is on, proceeding without a confirmed upload.");
                OnCalibrationComplete?.Invoke();
                return;
            }

            _uploadInFlight = false; // next successful pose in Update() retries the upload
            if (_instructionText != null) _instructionText.text = $"Upload failed: {error}. Retrying…";
        }

        private void UnsubscribeUpload()
        {
            if (!CalibrationUploadClient.HasInstance) return;
            CalibrationUploadClient.Instance.OnUploaded -= HandleUploaded;
            CalibrationUploadClient.Instance.OnUploadFailed -= HandleUploadFailed;
        }
    }
}
