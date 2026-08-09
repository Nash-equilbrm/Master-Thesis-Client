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
        [SerializeField] private TMP_InputField _cameraLabelField;
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
            if (_cameraLabelField != null) _cameraLabelField.gameObject.SetActive(CameraClientManager.HasInstance);

            if (_serverUrlField != null && string.IsNullOrEmpty(_serverUrlField.text))
            {
                if (ViewerTokenClient.HasInstance) _serverUrlField.text = ViewerTokenClient.Instance.ServerUrl;
                else if (RegistrationClient.HasInstance) _serverUrlField.text = RegistrationClient.Instance.ServerUrl;
            }

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

            if (RegistrationClient.HasInstance)
            {
                RegistrationClient.Instance.OnRegistered         += HandleRegistered;
                RegistrationClient.Instance.OnRegistrationFailed += HandleRegistrationFailed;
            }

            if (LiveKitCameraPublisher.HasInstance)
            {
                LiveKitCameraPublisher.Instance.OnPublishingStarted += HandlePublishingStarted;
                LiveKitCameraPublisher.Instance.OnConnectionFailed  += HandleCameraConnectionFailed;
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

            if (RegistrationClient.HasInstance)
            {
                RegistrationClient.Instance.OnRegistered         -= HandleRegistered;
                RegistrationClient.Instance.OnRegistrationFailed -= HandleRegistrationFailed;
            }

            if (LiveKitCameraPublisher.HasInstance)
            {
                LiveKitCameraPublisher.Instance.OnPublishingStarted -= HandlePublishingStarted;
                LiveKitCameraPublisher.Instance.OnConnectionFailed  -= HandleCameraConnectionFailed;
            }

            base.Hide();
        }

        private void OnConnectClicked()
        {
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

            if (CameraClientManager.HasInstance)
            {
                if (!RegistrationClient.HasInstance)
                {
                    SetStatus("No RegistrationClient in scene.", isError: true);
                    return;
                }
                SetBusy(true);
                SetStatus("Registering…");
                RegistrationClient.Instance.ServerUrl = url;
                if (LiveKitCameraPublisher.HasInstance)
                    LiveKitCameraPublisher.Instance.Label = _cameraLabelField != null ? _cameraLabelField.text.Trim() : "";
                CameraClientManager.Instance.StartRegistering();
                return;
            }

            if (!ViewerTokenClient.HasInstance)
            {
                SetStatus("No ViewerTokenClient in scene.", isError: true);
                return;
            }

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

        private void HandleRegistered()
        {
            SetStatus("Connecting…");
        }

        private void HandleRegistrationFailed(string error)
        {
            SetBusy(false);
            SetStatus($"Registration failed: {error}", isError: true);
        }

        private void HandlePublishingStarted()
        {
            SetStatus("Connected.");
            Hide();
        }

        private void HandleCameraConnectionFailed(string error)
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
