using Thesis.Patterns;
using Thesis.UI.Screens;

namespace Thesis.Managers
{
    public partial class CameraClientManager
    {
        private class RegisteringState : State<CameraClientManager>
        {
            public RegisteringState(CameraClientManager ctx) : base(ctx) { }

            public override void Enter()
            {
                if (UIManager.HasInstance)
                    UIManager.Instance.ShowScreen<ConnectionStatusScreen>(forceShow: true);

                _context.CurrentState = CameraState.Registering;
                _context.OnStateChanged?.Invoke(CameraState.Registering);

                var reg = RegistrationClient.Instance;
                if (reg == null)
                {
                    _context.ChangeState(new ErrorState(_context, "RegistrationClient not found."), CameraState.Error);
                    return;
                }

                reg.OnRegistered += OnRegistered;
                reg.OnRegistrationFailed += OnFailed;
                reg.Register();
            }

            public override void Exit()
            {
                if (!RegistrationClient.HasInstance) return;
                RegistrationClient.Instance.OnRegistered -= OnRegistered;
                RegistrationClient.Instance.OnRegistrationFailed -= OnFailed;
            }

            private void OnRegistered() =>
                _context.ChangeState(new ConnectingState(_context), CameraState.Connecting);

            private void OnFailed(string error) =>
                _context.ChangeState(new ErrorState(_context, error), CameraState.Error);
        }
    }
}
