using Thesis.Patterns;
using Thesis.UI.Screens;

namespace Thesis.Managers
{
    public partial class CameraClientManager
    {
        private class StreamingState : State<CameraClientManager>
        {
            public StreamingState(CameraClientManager ctx) : base(ctx) { }

            public override void Enter()
            {
                if (LiveKitCameraPublisher.HasInstance)
                    LiveKitCameraPublisher.Instance.OnDisconnected += OnDisconnected;

                if (UIManager.HasInstance)
                    UIManager.Instance.ShowScreen<ConnectionStatusScreen>(forceShow: true);
            }

            public override void Exit()
            {
                if (LiveKitCameraPublisher.HasInstance)
                    LiveKitCameraPublisher.Instance.OnDisconnected -= OnDisconnected;
            }

            private void OnDisconnected() =>
                _context.ChangeState(new RegisteringState(_context), CameraState.Registering);
        }
    }
}
