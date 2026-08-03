using System.Collections.Generic;
using Thesis.Managers;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Thesis.Stream
{
    public class CameraSwitcher : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private CameraStreamPlayer _streamPlayer;
        [SerializeField] private Transform _buttonContainer;

        [Header("Camera Identities")]
        [SerializeField] private string[] _cameraIdentities =
        {
            "cam1", "cam2", "cam3", "cam4", "cam5",
            "cam6", "cam7", "cam8", "cam9", "cam10"
        };

        private readonly Dictionary<string, Button> _buttons = new();
        private string _activeCamera;

        void Start()
        {
            if (!LiveKitManager.HasInstance) return;

            LiveKitManager.Instance.OnConnected += OnConnected;
            LiveKitManager.Instance.OnVideoTrackAvailable += OnVideoTrackAvailable;
            LiveKitManager.Instance.OnVideoTrackRemoved += OnVideoTrackRemoved;

            if (LiveKitManager.Instance.IsConnected)
                OnConnected(LiveKitManager.Instance.Room);
        }

        void OnDestroy()
        {
            if (!LiveKitManager.HasInstance) return;
            LiveKitManager.Instance.OnConnected -= OnConnected;
            LiveKitManager.Instance.OnVideoTrackAvailable -= OnVideoTrackAvailable;
            LiveKitManager.Instance.OnVideoTrackRemoved -= OnVideoTrackRemoved;
        }

        private void OnConnected(LiveKit.Room room)
        {
            foreach (var identity in _cameraIdentities)
            {
                if (!_buttons.ContainsKey(identity))
                    CreateButton(identity);
            }

            foreach (var identity in LiveKitManager.Instance.VideoTracks.Keys)
                SetButtonInteractable(identity, true);
        }

        private void OnVideoTrackAvailable(string identity)
        {
            if (!_buttons.ContainsKey(identity))
                CreateButton(identity);
            SetButtonInteractable(identity, true);
        }

        private void OnVideoTrackRemoved(string identity)
        {
            SetButtonInteractable(identity, false);

            if (_activeCamera == identity)
            {
                _streamPlayer?.Unsubscribe();
                _activeCamera = null;
            }
        }

        private void SwitchCamera(string identity)
        {
            if (_activeCamera == identity) return;
            if (!LiveKitManager.Instance.VideoTracks.TryGetValue(identity, out var pub)) return;

            _activeCamera = identity;
            HighlightActiveButton(identity);
            _streamPlayer?.SubscribeTo(pub);
        }

        private void CreateButton(string identity)
        {
            var go = new GameObject(identity, typeof(RectTransform));
            go.transform.SetParent(_buttonContainer, false);

            var rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(0, 50);

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.15f, 0.15f, 0.15f, 0.9f);

            var btn = go.AddComponent<Button>();
            var colors = btn.colors;
            colors.normalColor = new Color(0.15f, 0.15f, 0.15f, 0.9f);
            colors.highlightedColor = new Color(0.3f, 0.3f, 0.3f, 1f);
            colors.pressedColor = new Color(0.05f, 0.05f, 0.05f, 1f);
            colors.disabledColor = new Color(0.1f, 0.1f, 0.1f, 0.4f);
            btn.colors = colors;
            btn.targetGraphic = bg;
            btn.interactable = false;

            var textGo = new GameObject("Label", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var textRt = textGo.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;
            textRt.offsetMin = new Vector2(8, 0);
            textRt.offsetMax = new Vector2(-8, 0);

            var label = textGo.AddComponent<TextMeshProUGUI>();
            label.text = identity.ToUpper();
            label.alignment = TextAlignmentOptions.Center;
            label.fontSize = 16;
            label.fontStyle = FontStyles.Bold;
            label.color = Color.white;

            var captured = identity;
            btn.onClick.AddListener(() => SwitchCamera(captured));
            _buttons[identity] = btn;
        }

        private void SetButtonInteractable(string identity, bool interactable)
        {
            if (_buttons.TryGetValue(identity, out var btn))
                btn.interactable = interactable;
        }

        private void HighlightActiveButton(string activeIdentity)
        {
            foreach (var kv in _buttons)
            {
                var img = kv.Value.GetComponent<Image>();
                if (img == null) continue;
                img.color = kv.Key == activeIdentity
                    ? new Color(0.2f, 0.5f, 0.9f, 1f)
                    : new Color(0.15f, 0.15f, 0.15f, 0.9f);
            }
        }
    }
}
