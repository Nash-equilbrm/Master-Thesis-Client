using Thesis.Patterns;
using Thesis.UI.Screens;

namespace Thesis.Managers
{
    public partial class AppManager
    {
        private class RoleSelectState : State<AppManager>
        {
            public RoleSelectState(AppManager ctx) : base(ctx) { }

            public override void Enter()
            {
                AppRoleManager.Instance.OnRoleSelected += OnRoleSelected;
                UIManager.Instance.ShowScreen<RoleSelectScreen>(forceShow: true);
            }

            public override void Exit()
            {
                AppRoleManager.Instance.OnRoleSelected -= OnRoleSelected;
            }

            private void OnRoleSelected(AppRole _) =>
                _context.ChangeState(new AppRunningState(_context), AppState.AppRunning);
        }
    }
}
