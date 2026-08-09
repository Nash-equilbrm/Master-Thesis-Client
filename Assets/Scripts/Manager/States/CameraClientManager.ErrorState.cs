using Thesis.Patterns;
using UnityEngine;

namespace Thesis.Managers
{
    public partial class CameraClientManager
    {
        private class ErrorState : State<CameraClientManager>
        {
            private readonly string _error;

            public ErrorState(CameraClientManager ctx, string error) : base(ctx)
            {
                _error = error;
            }

            public override void Enter()
            {
                _context.ErrorMessage = _error;
                Debug.LogError($"[CameraClientManager] Error: {_error}");
            }
        }
    }
}
