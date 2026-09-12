using Thesis.Managers;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Thesis.UI.Popups
{
    public class DevCommandPopup : BasePopup
    {
        [Header("References")]
        [SerializeField] private Button _logViewerButton;
        [SerializeField] private Button _serverConfigButton;
        [SerializeField] private Button _charucoBoardButton;
        [SerializeField] private Button _toggleCalibrationBypassButton;
        [SerializeField] private Button _closeButton;

        public override void Init()
        {
            base.Init();
            if (_logViewerButton    != null) _logViewerButton.onClick.AddListener(OnLogViewerClicked);
            if (_serverConfigButton != null) _serverConfigButton.onClick.AddListener(OnServerConfigClicked);
            if (_charucoBoardButton != null) _charucoBoardButton.onClick.AddListener(OnCharucoBoardClicked);
            if (_toggleCalibrationBypassButton != null) _toggleCalibrationBypassButton.onClick.AddListener(OnToggleCalibrationBypassClicked);
            if (_closeButton        != null) _closeButton.onClick.AddListener(() => Hide());
        }

        public override void Show(object data)
        {
            base.Show(data);
            RefreshCalibrationBypassLabel();
        }

        private void OnLogViewerClicked()
        {
            Hide();
            DevCommandMenu menu = FindObjectOfType<DevCommandMenu>();
            menu?.OpenReporter();
        }

        private void OnServerConfigClicked()
        {
            Hide();
            UIManager.Instance.ShowPopup<ServerConfigPopup>(forceShow: true);
        }

        private void OnCharucoBoardClicked()
        {
            Hide();
            UIManager.Instance.ShowPopup<CharucoBoardPopup>(forceShow: true);
        }

        private void OnToggleCalibrationBypassClicked()
        {
            Thesis.AppConfig.DevSkipCalibrationUpload = !Thesis.AppConfig.DevSkipCalibrationUpload;
            RefreshCalibrationBypassLabel();
        }

        private void RefreshCalibrationBypassLabel()
        {
            if (_toggleCalibrationBypassButton == null) return;
            var label = _toggleCalibrationBypassButton.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.text = Thesis.AppConfig.DevSkipCalibrationUpload
                    ? "Calibration Upload Bypass: ON"
                    : "Calibration Upload Bypass: OFF";
        }
    }
}
