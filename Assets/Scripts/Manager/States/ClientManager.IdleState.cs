using LiveKit;
using Thesis.Patterns;
using Thesis.UI.Screens;

namespace Thesis.Managers
{
    public partial class ClientManager
    {
        private class IdleState : State<ClientManager>
        {
            private readonly string _message;

            public IdleState(ClientManager ctx, string message = null) : base(ctx)
            {
                _message = message;
            }

            public override void Enter()
            {
                if (LiveKitManager.HasInstance)
                    LiveKitManager.Instance.OnConnected += OnConnected;

                UIManager.Instance.ShowScreen<ConnectionScreen>(_message, forceShow: true);
            }

            public override void Exit()
            {
                if (LiveKitManager.HasInstance)
                    LiveKitManager.Instance.OnConnected -= OnConnected;
            }

            private void OnConnected(Room room) =>
                _context.ChangeState(new StreamingState(_context), ClientState.Streaming);
        }
    }
}
