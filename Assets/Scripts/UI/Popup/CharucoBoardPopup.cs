using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.ArucoModule;
using OpenCVForUnity.ImgcodecsModule;

namespace Thesis.UI.Popups
{
    public class CharucoBoardPopup : BasePopup
    {
        [Header("Board Parameters")]
        [SerializeField] private TMP_InputField _squaresXField;
        [SerializeField] private TMP_InputField _squaresYField;
        [SerializeField] private TMP_InputField _squareLengthMmField;
        [SerializeField] private TMP_InputField _markerLengthMmField;

        [Header("Save Location")]
        [SerializeField] private TMP_InputField _saveDirField;

        [Header("Actions")]
        [SerializeField] private Button _generateButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private TMP_Text _statusText;

        private const string PrefSquaresX        = "charuco_squaresX";
        private const string PrefSquaresY        = "charuco_squaresY";
        private const string PrefSquareLengthMm  = "charuco_squareMm";
        private const string PrefMarkerLengthMm  = "charuco_markerMm";
        private const string PrefSaveDir         = "charuco_saveDir";

        public override void Init()
        {
            base.Init();
            if (_generateButton != null) _generateButton.onClick.AddListener(OnGenerate);
            if (_closeButton    != null) _closeButton.onClick.AddListener(() => Hide());
        }

        public override void Show(object data)
        {
            base.Show(data);
            LoadPrefs();
            SetStatus("");
        }

        private void OnGenerate()
        {
            if (!TryParseInputs(out int sqX, out int sqY, out float sqMm, out float mMm)) return;

            string dir = _saveDirField != null ? _saveDirField.text.Trim() : Application.persistentDataPath;
            if (string.IsNullOrEmpty(dir)) dir = Application.persistentDataPath;

            try
            {
                Directory.CreateDirectory(dir);
            }
            catch (System.Exception e)
            {
                SetStatus($"Invalid directory: {e.Message}");
                return;
            }

            var dict  = Aruco.getPredefinedDictionary(Aruco.DICT_5X5_250);
            var board = CharucoBoard.create(sqX, sqY, sqMm / 1000f, mMm / 1000f, dict);

            int outputW = 2100;
            int outputH = Mathf.RoundToInt(outputW * sqY / (float)sqX);
            var img = new Mat();
            board.draw(new Size(outputW, outputH), img, 20, 1);

            string path = Path.Combine(dir, "CalibrationBoard.png");
            bool ok = Imgcodecs.imwrite(path, img);

            img.Dispose();
            board.Dispose();
            dict.Dispose();

            if (ok)
            {
                SavePrefs(sqX, sqY, sqMm, mMm, dir);
                SetStatus($"Saved:\n{path}");
                Debug.Log($"[CharucoBoardPopup] Board saved to {path}");
            }
            else
            {
                SetStatus("Failed to write image — check the directory path.");
            }
        }

        private bool TryParseInputs(out int sqX, out int sqY, out float sqMm, out float mMm)
        {
            sqX = 5; sqY = 7; sqMm = 30f; mMm = 15f;

            if (!int.TryParse(_squaresXField?.text, out sqX) || sqX < 3)
            { SetStatus("Squares X must be ≥ 3."); return false; }

            if (!int.TryParse(_squaresYField?.text, out sqY) || sqY < 3)
            { SetStatus("Squares Y must be ≥ 3."); return false; }

            if (!float.TryParse(_squareLengthMmField?.text, out sqMm) || sqMm <= 0)
            { SetStatus("Square length must be > 0."); return false; }

            if (!float.TryParse(_markerLengthMmField?.text, out mMm) || mMm <= 0 || mMm >= sqMm)
            { SetStatus("Marker length must be > 0 and < square length."); return false; }

            return true;
        }

        private void LoadPrefs()
        {
            if (_squaresXField       != null) _squaresXField.text       = PlayerPrefs.GetInt(PrefSquaresX, 5).ToString();
            if (_squaresYField       != null) _squaresYField.text       = PlayerPrefs.GetInt(PrefSquaresY, 7).ToString();
            if (_squareLengthMmField != null) _squareLengthMmField.text = PlayerPrefs.GetFloat(PrefSquareLengthMm, 30f).ToString("F1");
            if (_markerLengthMmField != null) _markerLengthMmField.text = PlayerPrefs.GetFloat(PrefMarkerLengthMm, 15f).ToString("F1");
            if (_saveDirField        != null) _saveDirField.text        = PlayerPrefs.GetString(PrefSaveDir, Application.persistentDataPath);
        }

        private void SavePrefs(int sqX, int sqY, float sqMm, float mMm, string dir)
        {
            PlayerPrefs.SetInt(PrefSquaresX, sqX);
            PlayerPrefs.SetInt(PrefSquaresY, sqY);
            PlayerPrefs.SetFloat(PrefSquareLengthMm, sqMm);
            PlayerPrefs.SetFloat(PrefMarkerLengthMm, mMm);
            PlayerPrefs.SetString(PrefSaveDir, dir);
            PlayerPrefs.Save();
        }

        private void SetStatus(string msg)
        {
            if (_statusText != null) _statusText.text = msg;
        }
    }
}
