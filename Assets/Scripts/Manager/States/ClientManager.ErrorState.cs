using Thesis.Patterns;
using UnityEngine;

namespace Thesis.Managers
{
    public partial class ClientManager
    {
        private class ErrorState : State<ClientManager>
        {
            private readonly string _error;

            public ErrorState(ClientManager ctx, string error) : base(ctx)
            {
                _error = error;
            }

            public override void Enter()
            {
                _context.ErrorMessage = _error;
                Debug.LogError($"[ClientManager] Error: {_error}");
            }
        }
    }
}
