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

                pub.OnPublishingStarted += OnPublishingStarted;
                pub.OnDisconnected += OnDisconnected;
                pub.OnConnectionFailed += OnFailed;
                pub.BeginStreaming();
            }

            public override void Exit()
            {
                if (!LiveKitCameraPublisher.HasInstance) return;
                LiveKitCameraPublisher.Instance.OnPublishingStarted -= OnPublishingStarted;
                LiveKitCameraPublisher.Instance.OnDisconnected -= OnDisconnected;
                LiveKitCameraPublisher.Instance.OnConnectionFailed -= OnFailed;
            }

            private void OnPublishingStarted() =>
                _context.ChangeState(new StreamingState(_context), CameraState.Streaming);

            private void OnDisconnected() =>
                _context.ChangeState(new RegisteringState(_context), CameraState.Registering);

            private void OnFailed(string error) =>
                _context.ChangeState(new ErrorState(_context, error), CameraState.Error);
        }
    }
}
