using Thesis.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace Thesis.UI.Popups
{
    public class DevCommandPopup : BasePopup
    {
        [Header("References")]
        [SerializeField] private Button _logViewerButton;
        [SerializeField] private Button _serverConfigButton;
        [SerializeField] private Button _closeButton;

        public override void Init()
        {
            base.Init();
            if (_logViewerButton  != null) _logViewerButton.onClick.AddListener(OnLogViewerClicked);
            if (_serverConfigButton != null) _serverConfigButton.onClick.AddListener(OnServerConfigClicked);
            if (_closeButton != null) _closeButton.onClick.AddListener(() => Hide());
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
    }
}
