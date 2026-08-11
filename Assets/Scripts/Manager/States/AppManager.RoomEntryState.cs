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
                RoomClient.Instance.OnRoomReady += OnRoomReady;
                UIManager.Instance.ShowScreen<RoomScreen>(forceShow: true);
            }

            public override void Exit()
            {
                RoomClient.Instance.OnRoomReady -= OnRoomReady;
            }

            private void OnRoomReady(string _) =>
                _context.ChangeState(new RoleSelectState(_context), AppState.RoleSelect);
        }
    }
}
