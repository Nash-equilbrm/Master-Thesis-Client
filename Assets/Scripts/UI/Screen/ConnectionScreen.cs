using LiveKit;
using Thesis.Managers;
using Thesis.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Thesis.UI.Screens
{
    public class ConnectionScreen : BaseScreen
    {
        [Header("References")]
        [SerializeField] private TMP_InputField _serverUrlField;
        [SerializeField] private Button _connectButton;
        [SerializeField] private Button _retryButton;
        [SerializeField] private TMP_Text _statusText;

        private static readonly Color _normalColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        private static readonly Color _errorColor  = new Color(1f,    0.40f, 0.40f, 1f);

        public override void Init()
        {
            base.Init();
            if (_connectButton != null)
                _connectButton.onClick.AddListener(OnConnectClicked);

            if (_retryButton != null)
            {
                _retryButton.onClick.AddListener(OnRetryClicked);
                _retryButton.gameObject.SetActive(false);
            }
        }

        public override void Show(object data)
        {
            base.Show(data);

            if (_retryButton != null) _retryButton.gameObject.SetActive(false);
            if (_connectButton != null) _connectButton.interactable = true;

            if (ViewerTokenClient.HasInstance && _serverUrlField != null && string.IsNullOrEmpty(_serverUrlField.text))
                _serverUrlField.text = ViewerTokenClient.Instance.ServerUrl;

            if (ViewerTokenClient.HasInstance)
            {
                ViewerTokenClient.Instance.OnTokenReceived  += HandleTokenReceived;
                ViewerTokenClient.Instance.OnTokenFetchFailed += HandleTokenFetchFailed;
            }

            if (LiveKitManager.HasInstance)
            {
                LiveKitManager.Instance.OnConnected       += HandleConnected;
                LiveKitManager.Instance.OnConnectionError += HandleConnectionError;
            }

            SetStatus(data is string msg ? msg : "Enter the server URL and press Connect.");
        }

        public override void Hide()
        {
            if (ViewerTokenClient.HasInstance)
            {
                ViewerTokenClient.Instance.OnTokenReceived   -= HandleTokenReceived;
                ViewerTokenClient.Instance.OnTokenFetchFailed -= HandleTokenFetchFailed;
            }

            if (LiveKitManager.HasInstance)
            {
                LiveKitManager.Instance.OnConnected       -= HandleConnected;
                LiveKitManager.Instance.OnConnectionError -= HandleConnectionError;
            }

            base.Hide();
        }

        private void OnConnectClicked()
        {
            if (!ViewerTokenClient.HasInstance)
            {
                SetStatus("No ViewerTokenClient in scene.", isError: true);
                return;
            }

            var raw = _serverUrlField != null ? _serverUrlField.text.Trim() : "";
            if (string.IsNullOrEmpty(raw))
            {
                SetStatus("Server URL is required.", isError: true);
                return;
            }

            var url = raw;
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                url = "http://" + url;
            if (!System.Text.RegularExpressions.Regex.IsMatch(url, @":\d+$"))
                url = url.TrimEnd('/') + ":3000";

            SetBusy(true);
            SetStatus("Fetching token…");
            ViewerTokenClient.Instance.FetchToken(url);
        }

        private void OnRetryClicked() => OnConnectClicked();

        private void HandleTokenReceived()
        {
            if (!LiveKitManager.HasInstance)
            {
                SetStatus("No LiveKitManager in scene.", isError: true);
                SetBusy(false);
                return;
            }
            SetStatus("Connecting…");
            LiveKitManager.Instance.ConnectWith(
                ViewerTokenClient.Instance.LiveKitUrl,
                ViewerTokenClient.Instance.Token);
        }

        private void HandleTokenFetchFailed(string error)
        {
            SetBusy(false);
            SetStatus($"Token fetch failed: {error}", isError: true);
        }

        private void HandleConnected(Room room)
        {
            SetStatus("Connected.");
            Hide();
        }

        private void HandleConnectionError(string error)
        {
            SetBusy(false);
            SetStatus($"Connection failed: {error}", isError: true);
        }

        private void SetBusy(bool busy)
        {
            if (_connectButton != null) _connectButton.interactable = !busy;
            if (_retryButton   != null) _retryButton.gameObject.SetActive(!busy);
        }

        private void SetStatus(string message, bool isError = false)
        {
            if (_statusText == null) return;
            _statusText.text  = message;
            _statusText.color = isError ? _errorColor : _normalColor;
        }
    }
}
