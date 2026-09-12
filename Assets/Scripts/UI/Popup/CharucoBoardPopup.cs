using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Thesis.Calibration;
using Thesis.Managers;

namespace Thesis.UI.Popups
{
    // Board parameters are no longer freely editable here — they come from
    // CalibrationConfigClient (server-provided), so this dev tool can never print a
    // board that disagrees with what the live calibration tutorial expects.
    public class CharucoBoardPopup : BasePopup
    {
        [Header("Board Info (read-only, from server)")]
        [SerializeField] private TMP_Text _boardInfoText;

        [Header("Save Location")]
        [SerializeField] private TMP_InputField _saveDirField;

        [Header("Actions")]
        [SerializeField] private Button _generateButton;
        [SerializeField] private Button _closeButton;
        [SerializeField] private TMP_Text _statusText;

        private const string PrefSaveDir = "charuco_saveDir";

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

            if (_saveDirField != null)
                _saveDirField.text = PlayerPrefs.GetString(PrefSaveDir, Application.persistentDataPath);

            if (CalibrationConfigClient.HasInstance && CalibrationConfigClient.Instance.Config != null)
            {
                ShowBoardInfo(CalibrationConfigClient.Instance.Config);
            }
            else
            {
                SetBoardInfoText("Fetching board configuration…");
                if (CalibrationConfigClient.HasInstance)
                {
                    CalibrationConfigClient.Instance.OnConfigReady += ShowBoardInfo;
                    CalibrationConfigClient.Instance.FetchConfig(Thesis.AppConfig.ServerUrl);
                }
                else
                {
                    ShowBoardInfo(BoardConfig.Default);
                }
            }
        }

        public override void Hide(System.Action onComplete = null)
        {
            if (CalibrationConfigClient.HasInstance)
                CalibrationConfigClient.Instance.OnConfigReady -= ShowBoardInfo;
            base.Hide(onComplete);
        }

        private void ShowBoardInfo(BoardConfig config)
        {
            SetBoardInfoText($"{config.squaresX}×{config.squaresY} squares, " +
                              $"{config.squareLengthMm:F1}mm / {config.markerLengthMm:F1}mm, dict {config.dictionaryId}");
        }

        private void SetBoardInfoText(string msg)
        {
            if (_boardInfoText != null) _boardInfoText.text = msg;
        }

        private void OnGenerate()
        {
            var config = CalibrationConfigClient.HasInstance && CalibrationConfigClient.Instance.Config != null
                ? CalibrationConfigClient.Instance.Config
                : BoardConfig.Default;

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

            byte[] png = CharucoBoardGenerator.GeneratePng(config);
            string path = Path.Combine(dir, "CalibrationBoard.png");

            try
            {
                File.WriteAllBytes(path, png);
            }
            catch (System.Exception e)
            {
                SetStatus($"Failed to write image: {e.Message}");
                return;
            }

            PlayerPrefs.SetString(PrefSaveDir, dir);
            PlayerPrefs.Save();
            SetStatus($"Saved:\n{path}");
            Debug.Log($"[CharucoBoardPopup] Board saved to {path}");
        }

        private void LoadPrefs()
        {
            if (_saveDirField != null) _saveDirField.text = PlayerPrefs.GetString(PrefSaveDir, Application.persistentDataPath);
        }

        private void SetStatus(string msg)
        {
            if (_statusText != null) _statusText.text = msg;
        }
    }
}
