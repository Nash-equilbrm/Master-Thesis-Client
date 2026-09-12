using System;
using UnityEngine;
using OpenCVForUnity.ArucoModule;

namespace Thesis.Calibration {

    // Board identity (dictionary + grid + physical size) is server-provided so every
    // camera calibrates against the same physical ChArUco board — see CalibrationConfigClient.
    [Serializable]
    public class BoardConfig {
        public int dictionaryId = Aruco.DICT_5X5_250;
        public int squaresX = 5;
        public int squaresY = 7;
        public float squareLengthMm = 30f;
        public float markerLengthMm = 15f;

        public float SquareLengthM => squareLengthMm / 1000f;
        public float MarkerLengthM => markerLengthMm / 1000f;

        public static BoardConfig Default => new BoardConfig();
    }

    [Serializable]
    public class IntrinsicsData {
        public float fx = 600f, fy = 600f;
        public float cx = 320f, cy = 240f;
        public float[] distCoeffs = new float[5];
        public int imageWidth = 640, imageHeight = 480;
        public float reprojectionError = -1f;
    }

    [Serializable]
    public class ExtrinsicsData {
        // Output of estimatePoseCharucoBoard: transforms board frame → camera frame
        // P_cam = R(rvec) * P_board + tvec
        public float[] rvec = new float[3];
        public float[] tvec = new float[3];
    }

    [Serializable]
    public class CameraCalibrationData {
        public string cameraName = "cam";
        public IntrinsicsData intrinsics = new IntrinsicsData();
        public ExtrinsicsData extrinsics;
    }

    [Serializable]
    public class SceneCalibrationData {
        public CameraCalibrationData cam1 = new CameraCalibrationData { cameraName = "cam1" };
        public CameraCalibrationData cam2 = new CameraCalibrationData { cameraName = "cam2" };
        public float depthMin = 0.3f;
        public float depthMax = 5.0f;

        public string ToJson() => JsonUtility.ToJson(this, prettyPrint: true);

        public static SceneCalibrationData FromJson(string json) =>
            JsonUtility.FromJson<SceneCalibrationData>(json);
    }
}
