using Thesis.Managers;
using Thesis.UI;
using TMPro;
using UnityEngine;

namespace Thesis.UI.Screens
{
    public class ConnectionStatusScreen : BaseScreen
    {
        [SerializeField] private TMP_Text _statusText;

        public override void Init() => base.Init();

        public override void Show(object data = null)
        {
            base.Show(data);
            if (CameraClientManager.HasInstance)
            {
                CameraClientManager.Instance.OnStateChanged += OnStateChanged;
                OnStateChanged(CameraClientManager.Instance.CurrentState);
            }
        }

        public override void Hide()
        {
            if (CameraClientManager.HasInstance)
                CameraClientManager.Instance.OnStateChanged -= OnStateChanged;
            base.Hide();
        }

        private void OnStateChanged(CameraState state)
        {
            var message = state switch
            {
                CameraState.Idle        => "Idle",
                CameraState.Registering => "Registering...",
                CameraState.Connecting  => "Connecting...",
                CameraState.Streaming   => "Streaming",
                CameraState.Error       => $"Error: {CameraClientManager.Instance.ErrorMessage}",
                _                       => state.ToString()
            };
            SetStatus(message);
        }

        private void SetStatus(string message)
        {
            if (_statusText != null) _statusText.text = message;
            Debug.Log($"[ConnectionStatusScreen] {message}");
        }
    }
}
