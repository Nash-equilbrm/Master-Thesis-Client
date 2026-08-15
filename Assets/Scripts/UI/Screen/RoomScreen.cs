using System;
using Thesis.Managers;
using Thesis.UI;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Thesis.UI.Screens
{
    public class RoomScreen : BaseScreen
    {
        [Header("References")]
        [SerializeField] private TMP_InputField _usernameField;
        [SerializeField] private Button         _createButton;
        [SerializeField] private GameObject     _joinGroup;
        [SerializeField] private TMP_InputField _roomCodeField;
        [SerializeField] private Button         _joinButton;
        [SerializeField] private Button         _continueButton;
        [SerializeField] private TMP_Text       _statusText;

        private static readonly Color _normalColor = new Color(0.85f, 0.85f, 0.85f, 1f);
        private static readonly Color _errorColor  = new Color(1f,    0.40f, 0.40f, 1f);
        private static readonly Color _successColor = new Color(0.40f, 0.90f, 0.50f, 1f);

        private bool _wasCreator;

        /// Fires once this screen's job is done: immediately for a joiner (room
        /// ready), or when the creator taps Continue after seeing the room code.
        /// AppManager waits on this instead of RoomClient.OnRoomReady directly, so
        /// it can't race ahead of the "show code, wait for Continue" flow below.
        public event Action OnEntryComplete;

        public override void Init()
        {
            base.Init();
            if (_createButton   != null) _createButton.onClick.AddListener(OnCreateClicked);
            if (_joinButton     != null) _joinButton.onClick.AddListener(OnJoinClicked);
            if (_continueButton != null)
            {
                _continueButton.onClick.AddListener(() => OnEntryComplete?.Invoke());
                _continueButton.gameObject.SetActive(false);
            }
        }

        public override void Show(object data)
        {
            base.Show(data);

            ResetState();

            if (_usernameField != null)
                _usernameField.text = AppConfig.Username;

            if (RoomClient.HasInstance)
            {
                RoomClient.Instance.OnRoomReady += HandleRoomReady;
                RoomClient.Instance.OnFailed    += HandleFailed;
            }
        }

        public override void Hide(Action onComplete = null)
        {
            if (RoomClient.HasInstance)
            {
                RoomClient.Instance.OnRoomReady -= HandleRoomReady;
                RoomClient.Instance.OnFailed    -= HandleFailed;
            }
            base.Hide(onComplete);
        }

        private void OnCreateClicked()
        {
            if (RoomClient.HasInstance)
            {
                _wasCreator = true;
                RoomClient.Instance.CreateRoom(_usernameField != null ? _usernameField.text : "");
            }
        }

        private void OnJoinClicked()
        {
            if (RoomClient.HasInstance)
            {
                _wasCreator = false;
                RoomClient.Instance.JoinRoom(
                    _roomCodeField != null ? _roomCodeField.text : "",
                    _usernameField != null ? _usernameField.text : "");
            }
        }

        private void HandleRoomReady(string code)
        {
            if (_wasCreator)
            {
                // Show the generated code; user taps Continue when ready to share
                SetInputsInteractable(false);
                if (_joinGroup    != null) _joinGroup.SetActive(false);
                if (_continueButton != null) _continueButton.gameObject.SetActive(true);
                SetStatus($"Your room code:\n{code}\nShare this with participants!", _successColor);
            }
            else
            {
                OnEntryComplete?.Invoke();
            }
        }

        private void HandleFailed(string error) => SetStatus(error, _errorColor);

        private void ResetState()
        {
            _wasCreator = false;
            SetInputsInteractable(true);
            if (_joinGroup      != null) _joinGroup.SetActive(true);
            if (_continueButton != null) _continueButton.gameObject.SetActive(false);
            SetStatus("", _normalColor);
        }

        private void SetInputsInteractable(bool interactable)
        {
            if (_usernameField  != null) _usernameField.interactable  = interactable;
            if (_roomCodeField  != null) _roomCodeField.interactable  = interactable;
            if (_createButton   != null) _createButton.interactable   = interactable;
            if (_joinButton     != null) _joinButton.interactable     = interactable;
        }

        private void SetStatus(string message, Color color)
        {
            if (_statusText == null) return;
            _statusText.text  = message;
            _statusText.color = color;
        }
    }
}
