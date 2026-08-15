using Thesis.Patterns;
using Thesis.UI.Screens;

namespace Thesis.Managers
{
    public partial class AppManager
    {
        private class RoomEntryState : State<AppManager>
        {
            public RoomEntryState(AppManager ctx) : base(ctx) { }

            public override void Enter()
            {
                UIManager.Instance.ShowScreen<RoomScreen>(forceShow: true);
                var screen = UIManager.Instance.GetExistScreen<RoomScreen>();
                if (screen != null) screen.OnEntryComplete += OnEntryComplete;
            }

            public override void Exit()
            {
                var screen = UIManager.Instance.GetExistScreen<RoomScreen>();
                if (screen != null) screen.OnEntryComplete -= OnEntryComplete;
            }

            private void OnEntryComplete() =>
                _context.ChangeState(new RoleSelectState(_context), AppState.RoleSelect);
        }
    }
}
