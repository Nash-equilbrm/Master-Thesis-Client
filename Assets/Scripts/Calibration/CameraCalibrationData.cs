using System;
using UnityEngine;

namespace Thesis.Calibration {

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
