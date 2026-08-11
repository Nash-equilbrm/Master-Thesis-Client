using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Thesis.UI.Popups
{
    public class ServerConfigPopup : BasePopup
    {
        [Header("References")]
        [SerializeField] private TMP_InputField _urlField;
        [SerializeField] private TMP_Text _activeUrlText;
        [SerializeField] private Button _saveButton;
        [SerializeField] private Button _resetButton;
        [SerializeField] private Button _closeButton;

        public override void Init()
        {
            base.Init();
            if (_saveButton  != null) _saveButton.onClick.AddListener(OnSaveClicked);
            if (_resetButton != null) _resetButton.onClick.AddListener(OnResetClicked);
            if (_closeButton != null) _closeButton.onClick.AddListener(() => Hide());
        }

        public override void Show(object data)
        {
            base.Show(data);
            if (_urlField != null) _urlField.text = Thesis.AppConfig.ServerUrl;
            RefreshActiveLabel();
        }

        private void OnSaveClicked()
        {
            var url = _urlField != null ? _urlField.text.Trim() : "";
            if (string.IsNullOrEmpty(url)) return;

            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                url = "http://" + url;
            if (!System.Text.RegularExpressions.Regex.IsMatch(url, @":\d+$"))
                url = url.TrimEnd('/') + ":3000";

            Thesis.AppConfig.SetServerUrl(url);
            if (_urlField != null) _urlField.text = url;
            RefreshActiveLabel();
        }

        private void OnResetClicked()
        {
            Thesis.AppConfig.ClearServerUrl();
            if (_urlField != null) _urlField.text = Thesis.AppConfig.ServerUrl;
            RefreshActiveLabel();
        }

        private void RefreshActiveLabel()
        {
            if (_activeUrlText == null) return;
            var label = Thesis.AppConfig.HasOverride
                ? $"Override: {Thesis.AppConfig.ServerUrl}"
                : $"Default: {Thesis.AppConfig.ServerUrl}";
            _activeUrlText.text = label;
        }
    }
}
