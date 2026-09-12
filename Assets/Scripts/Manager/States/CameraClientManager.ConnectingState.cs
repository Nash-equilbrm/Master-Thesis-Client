using Thesis.Patterns;

namespace Thesis.Managers
{
    public partial class CameraClientManager
    {
        private class ConnectingState : State<CameraClientManager>
        {
            public ConnectingState(CameraClientManager ctx) : base(ctx) { }

            public override void Enter()
            {
                var pub = LiveKitCameraPublisher.Instance;
                if (pub == null)
                {
                    _context.ChangeState(new ErrorState(_context, "LiveKitCameraPublisher not found."), CameraState.Error);
                    return;
                }

                pub.OnReadyToPublish += OnReadyToPublish;
                pub.OnDisconnected += OnDisconnected;
                pub.OnConnectionFailed += OnFailed;
                pub.BeginStreaming();
            }

            public override void Exit()
            {
                if (!LiveKitCameraPublisher.HasInstance) return;
                LiveKitCameraPublisher.Instance.OnReadyToPublish -= OnReadyToPublish;
                LiveKitCameraPublisher.Instance.OnDisconnected -= OnDisconnected;
                LiveKitCameraPublisher.Instance.OnConnectionFailed -= OnFailed;
            }

            private void OnReadyToPublish() =>
                _context.ChangeState(new CalibratingState(_context), CameraState.Calibrating);

            private void OnDisconnected() =>
                _context.ChangeState(new RegisteringState(_context), CameraState.Registering);

            private void OnFailed(string error) =>
                _context.ChangeState(new ErrorState(_context, error), CameraState.Error);
        }
    }
}
