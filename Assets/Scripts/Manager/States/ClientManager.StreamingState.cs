using Thesis.Patterns;
using Thesis.UI.Screens;

namespace Thesis.Managers
{
    public partial class ClientManager
    {
        private class StreamingState : State<ClientManager>
        {
            public StreamingState(ClientManager ctx) : base(ctx) { }

            public override void Enter()
            {
                if (LiveKitManager.HasInstance)
                    LiveKitManager.Instance.OnDisconnected += OnDisconnected;

                UIManager.Instance.ShowScreen<StreamScreen>(forceShow: true);
            }

            public override void Exit()
            {
                if (LiveKitManager.HasInstance)
                    LiveKitManager.Instance.OnDisconnected -= OnDisconnected;
            }

            private void OnDisconnected() =>
                _context.ChangeState(
                    new IdleState(_context, "Disconnected. Enter server URL to reconnect."),
                    ClientState.Idle);
        }
    }
}
