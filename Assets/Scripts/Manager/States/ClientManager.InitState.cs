using Thesis.Patterns;

namespace Thesis.Managers
{
    public partial class ClientManager
    {
        private class InitState : State<ClientManager>
        {
            public InitState(ClientManager ctx) : base(ctx) { }

            public override void Enter() =>
                _context.ChangeState(new IdleState(_context), ClientState.Idle);
        }
    }
}
