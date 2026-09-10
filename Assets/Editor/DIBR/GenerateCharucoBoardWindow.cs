#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ArucoModule;
using OpenCVForUnity.ImgcodecsModule;

namespace Thesis.Calibration.Editor {

    /// <summary>
    /// Generates and saves a printable ChArUco board image.
    /// Open via Tools → DIBR → Generate ChArUco Board.
    ///
    /// Print at 100% scale (no fit-to-page).  Measure actual printed square size
    /// and enter it in CameraCalibrator.squareLengthM before calibrating.
    /// </summary>
    public class GenerateCharucoBoardWindow : EditorWindow {

        private int _squaresX = 5;
        private int _squaresY = 7;
        private float _squareLengthMm = 30f;
        private float _markerLengthMm = 15f;
        private int _dictionaryId = Aruco.DICT_5X5_250;
        private int _outputWidthPx = 2100;   // ≈ A4 width at 250 DPI
        private string _saveDir = "Assets/Resources/Sprites";
        private const string FileName = "CalibrationBoard.png";

        [MenuItem("Tools/DIBR/Generate ChArUco Board")]
        static void Open() => GetWindow<GenerateCharucoBoardWindow>("ChArUco Board Generator");

        private void OnGUI() {
            GUILayout.Label("ChArUco Board Generator", EditorStyles.boldLabel);
            EditorGUILayout.Space();

            _squaresX = EditorGUILayout.IntField("Squares X", _squaresX);
            _squaresY = EditorGUILayout.IntField("Squares Y", _squaresY);
            _squareLengthMm = EditorGUILayout.FloatField("Square Length (mm)", _squareLengthMm);
            _markerLengthMm = EditorGUILayout.FloatField("Marker Length (mm)", _markerLengthMm);
            EditorGUILayout.Space();
            _outputWidthPx = EditorGUILayout.IntField("Output Width (px)", _outputWidthPx);

            EditorGUILayout.LabelField("Save Directory");
            EditorGUILayout.BeginHorizontal();
            _saveDir = EditorGUILayout.TextField(_saveDir);
            if (GUILayout.Button("Browse", GUILayout.Width(60))) {
                string picked = EditorUtility.OpenFolderPanel("Choose save folder", _saveDir, "");
                if (!string.IsNullOrEmpty(picked)) _saveDir = picked;
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField("Output file", Path.Combine(_saveDir, FileName), EditorStyles.miniLabel);

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "Print at 100% / actual size (no scaling). " +
                "After printing, measure one square with a ruler and update " +
                "CameraCalibrator.squareLengthM accordingly.",
                MessageType.Info);

            if (GUILayout.Button("Generate & Save")) Generate();
        }

        private void Generate() {
            if (string.IsNullOrWhiteSpace(_saveDir)) {
                EditorUtility.DisplayDialog("Error", "Save directory cannot be empty.", "OK");
                return;
            }

            var dict = Aruco.getPredefinedDictionary(_dictionaryId);
            var board = CharucoBoard.create(
                _squaresX, _squaresY,
                _squareLengthMm / 1000f,
                _markerLengthMm / 1000f,
                dict);

            int h = Mathf.RoundToInt(_outputWidthPx * _squaresY / (float)_squaresX);
            var boardImg = new Mat();
            board.draw(new Size(_outputWidthPx, h), boardImg, 20, 1);

            Directory.CreateDirectory(_saveDir);
            string absPath = Path.GetFullPath(Path.Combine(_saveDir, FileName));
            Imgcodecs.imwrite(absPath, boardImg);

            boardImg.Dispose();
            board.Dispose();
            dict.Dispose();

            AssetDatabase.Refresh();
            Debug.Log($"[GenerateCharucoBoardWindow] Saved to {absPath}");
            EditorUtility.DisplayDialog("Done",
                $"Board image saved to:\n{absPath}\n\nPrint at 100% scale (no fit-to-page).",
                "OK");
        }
    }
}
#endif
