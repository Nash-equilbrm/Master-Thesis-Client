using Thesis.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace Thesis.UI.Screens
{
    public class RoleSelectScreen : BaseScreen
    {
        [Header("References")]
        [SerializeField] private Button _cameraButton;
        [SerializeField] private Button _viewerButton;

        public override void Init()
        {
            base.Init();
            if (_cameraButton != null) _cameraButton.onClick.AddListener(OnCameraClicked);
            if (_viewerButton != null) _viewerButton.onClick.AddListener(OnViewerClicked);
        }

        private void OnCameraClicked() => AppRoleManager.Instance.SelectRole(AppRole.Camera);
        private void OnViewerClicked() => AppRoleManager.Instance.SelectRole(AppRole.Viewer);
    }
}
