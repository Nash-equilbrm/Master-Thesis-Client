using Thesis.Patterns;
using Thesis.UI.Screens;
using UnityEngine;

namespace Thesis.Managers
{
    public class AppRoleManager : Singleton<AppRoleManager>
    {
        [SerializeField] private GameObject _viewerManagers;
        [SerializeField] private GameObject _cameraManagers;

        public AppRole CurrentRole { get; private set; }
        public bool HasSelectedRole { get; private set; }

        private void Start()
        {
            UIManager.Instance.ShowScreen<RoleSelectScreen>(forceShow: true);
        }

        public void SelectRole(AppRole role)
        {
            if (HasSelectedRole) return;
            HasSelectedRole = true;
            CurrentRole = role;

            if (_viewerManagers != null) _viewerManagers.SetActive(role == AppRole.Viewer);
            if (_cameraManagers != null) _cameraManagers.SetActive(role == AppRole.Camera);
        }
    }
}
