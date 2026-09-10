using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ArucoModule;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.UnityUtils;
using UnityRect = UnityEngine.Rect;

namespace Thesis.Calibration {

    /// <summary>
    /// Processes a recorded video to calibrate one camera using a ChArUco board.
    /// Step 1 — call StartProcessingVideo(): accumulates frames with board detected.
    /// Step 2 — call CalibrateIntrinsics(): computes fx/fy/cx/cy from accumulated frames.
    /// Step 3 — call EstimatePoseFromCurrentFrame(): with board in view, computes rvec/tvec.
    /// </summary>
    public class CameraCalibrator : MonoBehaviour {

        [Header("Board Parameters")]
        public int boardSquaresX = 5;
        public int boardSquaresY = 7;
        public float squareLengthM = 0.030f;
        public float markerLengthM = 0.015f;
        public int dictionaryId = Aruco.DICT_5X5_250;

        [Header("Calibration Settings")]
        public int minFramesForCalibration = 30;
        public int captureEveryNFrames = 5;
        public int charucoMinMarkers = 2;

        [Header("References")]
        public VideoPlayer videoPlayer;
        public TMPro.TextMeshProUGUI statusText;

        public IntrinsicsData Intrinsics { get; private set; }
        public ExtrinsicsData Extrinsics { get; private set; }
        public bool IsCalibrated { get; private set; }
        public bool HasExtrinsics { get; private set; }
        public int AccumulatedFrames => _allCharucoCorners.Count;

        public event Action<IntrinsicsData> OnIntrinsicsCalibrated;
        public event Action<ExtrinsicsData> OnExtrinsicsEstimated;

        private Dictionary _dictionary;
        private CharucoBoard _board;
        private DetectorParameters _detectorParams;

        private readonly List<Mat> _allCharucoCorners = new();
        private readonly List<Mat> _allCharucoIds = new();

        private bool _isProcessing;
        private int _frameCounter;
        private Texture2D _readbackTex;
        private Mat _frameMat;
        private Mat _grayMat;

        private void Awake() {
            _dictionary = Aruco.getPredefinedDictionary(dictionaryId);
            _detectorParams = DetectorParameters.create();
            _detectorParams.set_cornerRefinementMethod(1);
            _board = CharucoBoard.create(boardSquaresX, boardSquaresY, squareLengthM, markerLengthM, _dictionary);
        }

        private void OnDestroy() {
            _dictionary?.Dispose();
            _board?.Dispose();
            _detectorParams?.Dispose();
            _frameMat?.Dispose();
            _grayMat?.Dispose();
            DisposeAccumulatedFrames();
            if (_readbackTex) Destroy(_readbackTex);
        }

        // ── Step 1 ──────────────────────────────────────────────────────────────

        public void StartProcessingVideo() {
            if (_isProcessing) return;
            _isProcessing = true;
            _frameCounter = 0;
            videoPlayer.sendFrameReadyEvents = true;
            videoPlayer.frameReady += OnFrameReady;
            videoPlayer.Play();
        }

        public void StopProcessing() {
            if (!_isProcessing) return;
            videoPlayer.frameReady -= OnFrameReady;
            videoPlayer.Pause();
            _isProcessing = false;
        }

        private void OnFrameReady(VideoPlayer vp, long frameIndex) {
            _frameCounter++;
            if (_frameCounter % captureEveryNFrames != 0) return;

            if (vp.texture is not RenderTexture rt) return;
            ProcessFrameForIntrinsics(rt);
        }

        private void ProcessFrameForIntrinsics(RenderTexture rt) {
            EnsureReadbackTex(rt.width, rt.height);
            ReadFromRT(rt);
            EnsureMats(rt.width, rt.height);

            Utils.texture2DToMat(_readbackTex, _frameMat);
            Imgproc.cvtColor(_frameMat, _grayMat, Imgproc.COLOR_RGB2GRAY);

            var corners = new List<Mat>();
            var ids = new Mat();
            var rejected = new List<Mat>();
            Aruco.detectMarkers(_grayMat, _dictionary, corners, ids, _detectorParams, rejected);

            if (ids.total() > 0) {
                var charucoCorners = new Mat();
                var charucoIds = new Mat();
                Aruco.interpolateCornersCharuco(corners, ids, _grayMat, _board,
                    charucoCorners, charucoIds, new Mat(), new Mat(), charucoMinMarkers);

                if (charucoIds.total() >= 6) {
                    _allCharucoCorners.Add(charucoCorners);
                    _allCharucoIds.Add(charucoIds);
                    Log($"Accumulated {_allCharucoCorners.Count} frames");
                } else {
                    charucoCorners.Dispose();
                    charucoIds.Dispose();
                }
            }

            ids.Dispose();
            foreach (var c in corners) c.Dispose();
            foreach (var r in rejected) r.Dispose();
        }

        // ── Step 2 ──────────────────────────────────────────────────────────────

        public void CalibrateIntrinsics() {
            if (_allCharucoCorners.Count < minFramesForCalibration) {
                Log($"Need {minFramesForCalibration} frames, have {_allCharucoCorners.Count}");
                return;
            }

            var cameraMatrix = new Mat();
            var distCoeffs = new Mat();
            var rvecs = new List<Mat>();
            var tvecs = new List<Mat>();
            var imageSize = new Size(_readbackTex.width, _readbackTex.height);

            double rpe = Aruco.calibrateCameraCharuco(
                _allCharucoCorners, _allCharucoIds, _board, imageSize,
                cameraMatrix, distCoeffs, rvecs, tvecs);

            Intrinsics = new IntrinsicsData {
                fx = (float)cameraMatrix.get(0, 0)[0],
                fy = (float)cameraMatrix.get(1, 1)[0],
                cx = (float)cameraMatrix.get(0, 2)[0],
                cy = (float)cameraMatrix.get(1, 2)[0],
                imageWidth = _readbackTex.width,
                imageHeight = _readbackTex.height,
                reprojectionError = (float)rpe
            };

            double[] dist = new double[distCoeffs.total()];
            distCoeffs.get(0, 0, dist);
            Intrinsics.distCoeffs = Array.ConvertAll(dist, x => (float)x);

            IsCalibrated = true;
            Log($"Calibrated! RPE={rpe:F3}px  fx={Intrinsics.fx:F1} fy={Intrinsics.fy:F1}  cx={Intrinsics.cx:F1} cy={Intrinsics.cy:F1}");
            OnIntrinsicsCalibrated?.Invoke(Intrinsics);

            cameraMatrix.Dispose();
            distCoeffs.Dispose();
            foreach (var m in rvecs) m.Dispose();
            foreach (var m in tvecs) m.Dispose();
        }

        // ── Step 3 ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Reads the current video frame and estimates the board pose.
        /// Both cameras should be paused at a frame where the board is visible.
        /// </summary>
        public bool EstimatePoseFromCurrentFrame() {
            if (!IsCalibrated) { Log("Must calibrate intrinsics first."); return false; }
            if (videoPlayer.texture is not RenderTexture rt) return false;

            EnsureReadbackTex(rt.width, rt.height);
            ReadFromRT(rt);
            EnsureMats(rt.width, rt.height);

            Utils.texture2DToMat(_readbackTex, _frameMat);
            Imgproc.cvtColor(_frameMat, _grayMat, Imgproc.COLOR_RGB2GRAY);

            Mat cameraMatrix = BuildCameraMatrix();
            Mat distCoeffs = BuildDistCoeffs();

            var corners = new List<Mat>();
            var ids = new Mat();
            var rejected = new List<Mat>();
            Aruco.detectMarkers(_grayMat, _dictionary, corners, ids, _detectorParams, rejected);

            bool success = false;
            if (ids.total() > 0) {
                var charucoCorners = new Mat();
                var charucoIds = new Mat();
                Aruco.interpolateCornersCharuco(corners, ids, _grayMat, _board,
                    charucoCorners, charucoIds, cameraMatrix, distCoeffs, charucoMinMarkers);

                if (charucoIds.total() >= 4) {
                    var rvec = new Mat();
                    var tvec = new Mat();
                    success = Aruco.estimatePoseCharucoBoard(
                        charucoCorners, charucoIds, _board,
                        cameraMatrix, distCoeffs, rvec, tvec);

                    if (success) {
                        var rv = new double[3];
                        var tv = new double[3];
                        rvec.get(0, 0, rv);
                        tvec.get(0, 0, tv);
                        Extrinsics = new ExtrinsicsData {
                            rvec = Array.ConvertAll(rv, x => (float)x),
                            tvec = Array.ConvertAll(tv, x => (float)x)
                        };
                        HasExtrinsics = true;
                        Log($"Pose estimated: t=[{tv[0]:F3},{tv[1]:F3},{tv[2]:F3}]m");
                        OnExtrinsicsEstimated?.Invoke(Extrinsics);
                    } else {
                        Log("Pose estimation failed — not enough board visible.");
                    }
                    rvec.Dispose();
                    tvec.Dispose();
                }
                charucoCorners.Dispose();
                charucoIds.Dispose();
            }

            ids.Dispose();
            cameraMatrix.Dispose();
            distCoeffs.Dispose();
            foreach (var c in corners) c.Dispose();
            foreach (var r in rejected) r.Dispose();
            return success;
        }

        // ── Helpers ─────────────────────────────────────────────────────────────

        public void ResetAccumulatedFrames() {
            DisposeAccumulatedFrames();
            IsCalibrated = false;
            Log("Reset.");
        }

        private void DisposeAccumulatedFrames() {
            foreach (var m in _allCharucoCorners) m.Dispose();
            foreach (var m in _allCharucoIds) m.Dispose();
            _allCharucoCorners.Clear();
            _allCharucoIds.Clear();
        }

        private Mat BuildCameraMatrix() {
            Mat m = Mat.eye(3, 3, CvType.CV_64F);
            m.put(0, 0, Intrinsics.fx, 0, Intrinsics.cx,
                       0, Intrinsics.fy, Intrinsics.cy,
                       0, 0, 1);
            return m;
        }

        private Mat BuildDistCoeffs() {
            var d = new Mat(1, Intrinsics.distCoeffs.Length, CvType.CV_64F);
            d.put(0, 0, Array.ConvertAll(Intrinsics.distCoeffs, x => (double)x));
            return d;
        }

        private void EnsureReadbackTex(int w, int h) {
            if (_readbackTex == null || _readbackTex.width != w || _readbackTex.height != h)
                _readbackTex = new Texture2D(w, h, TextureFormat.RGB24, false);
        }

        private void ReadFromRT(RenderTexture rt) {
            RenderTexture.active = rt;
            _readbackTex.ReadPixels(new UnityRect(0, 0, rt.width, rt.height), 0, 0, false);
            _readbackTex.Apply(false);
            RenderTexture.active = null;
        }

        private void EnsureMats(int w, int h) {
            if (_frameMat == null || _frameMat.cols() != w || _frameMat.rows() != h)
                _frameMat = new Mat(h, w, CvType.CV_8UC3);
            if (_grayMat == null || _grayMat.cols() != w || _grayMat.rows() != h)
                _grayMat = new Mat(h, w, CvType.CV_8UC1);
        }

        private void Log(string msg) {
            if (statusText) statusText.text = msg;
            Debug.Log($"[CameraCalibrator:{name}] {msg}");
        }
    }
}
