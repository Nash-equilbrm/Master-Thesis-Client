using System;
using Thesis.Patterns;

namespace Thesis.Managers
{
    public class AppRoleManager : Singleton<AppRoleManager>
    {
        [UnityEngine.SerializeField] private UnityEngine.GameObject _viewerManagers;
        [UnityEngine.SerializeField] private UnityEngine.GameObject _cameraManagers;

        public AppRole CurrentRole { get; private set; }
        public bool HasSelectedRole { get; private set; }

        public event Action<AppRole> OnRoleSelected;

        public void SelectRole(AppRole role)
        {
            if (HasSelectedRole) return;
            HasSelectedRole = true;
            CurrentRole = role;

            if (_viewerManagers != null) _viewerManagers.SetActive(role == AppRole.Viewer);
            if (_cameraManagers != null) _cameraManagers.SetActive(role == AppRole.Camera);

            OnRoleSelected?.Invoke(role);
        }
    }
}
