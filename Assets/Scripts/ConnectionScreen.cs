using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Startup overlay that lets the user enter the LiveKit server URL and access
/// token, then connect. Hides itself once connected and reappears on
/// disconnect or connection error.
/// </summary>
public class ConnectionScreen : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private GameObject panelRoot;       // toggled on/off (usually this GameObject)
    [SerializeField] private TMP_InputField urlField;
    [SerializeField] private TMP_InputField tokenField;
    [SerializeField] private Button connectButton;
    [SerializeField] private TMP_Text statusText;

    void Start()
    {
        var mgr = LiveKitManager.Instance;
        if (mgr != null)
        {
            // Prefill from the manager's serialized defaults if the fields are empty.
            if (urlField != null && string.IsNullOrEmpty(urlField.text))
                urlField.text = mgr.ServerUrl;
            if (tokenField != null && string.IsNullOrEmpty(tokenField.text))
                tokenField.text = mgr.Token;

            mgr.OnConnected += HandleConnected;
            mgr.OnDisconnected += HandleDisconnected;
            mgr.OnConnectionError += HandleError;
        }

        if (connectButton != null)
            connectButton.onClick.AddListener(OnConnectClicked);

        Show("Enter server URL and token, then press Connect.");
    }

    void OnDestroy()
    {
        var mgr = LiveKitManager.Instance;
        if (mgr != null)
        {
            mgr.OnConnected -= HandleConnected;
            mgr.OnDisconnected -= HandleDisconnected;
            mgr.OnConnectionError -= HandleError;
        }
    }

    private void OnConnectClicked()
    {
        if (LiveKitManager.Instance == null)
        {
            SetStatus("No LiveKitManager in scene.");
            return;
        }

        var url = urlField != null ? urlField.text.Trim() : "";
        var token = tokenField != null ? tokenField.text.Trim() : "";

        if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(token))
        {
            SetStatus("Server URL and token are both required.");
            return;
        }

        if (connectButton != null) connectButton.interactable = false;
        SetStatus("Connecting…");
        LiveKitManager.Instance.ConnectWith(url, token);
    }

    private void HandleConnected(LiveKit.Room room)
    {
        SetStatus("Connected.");
        if (panelRoot != null) panelRoot.SetActive(false);
    }

    private void HandleDisconnected()
    {
        if (connectButton != null) connectButton.interactable = true;
        Show("Disconnected. Check the details and reconnect.");
    }

    private void HandleError(string message)
    {
        if (connectButton != null) connectButton.interactable = true;
        Show($"Connection failed: {message}");
    }

    private void Show(string message)
    {
        if (panelRoot != null) panelRoot.SetActive(true);
        SetStatus(message);
    }

    private void SetStatus(string message)
    {
        if (statusText != null) statusText.text = message;
    }
}
