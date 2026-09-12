using Thesis.Patterns;
using Thesis.UI.Screens;

namespace Thesis.Managers
{
    public partial class CameraClientManager
    {
        // Camera is open and the room is joined (from ConnectingState) but nothing is
        // published yet. Skips straight through if this device already calibrated;
        // otherwise runs the live tutorial and waits for its upload to complete
        // before publishing.
        private class CalibratingState : State<CameraClientManager>
        {
            private CalibrationTutorialScreen _screen;

            public CalibratingState(CameraClientManager ctx) : base(ctx) { }

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

                if (Thesis.AppConfig.CalibrationAcknowledged)
                {
                    pub.PublishNow();
                    return;
                }

                if (UIManager.HasInstance)
                {
                    UIManager.Instance.ShowScreen<CalibrationTutorialScreen>(forceShow: true);
                    _screen = UIManager.Instance.GetExistScreen<CalibrationTutorialScreen>();
                    if (_screen != null)
                        _screen.OnCalibrationComplete += OnCalibrationComplete;
                }
            }

            public override void Exit()
            {
                if (_screen != null)
                {
                    _screen.OnCalibrationComplete -= OnCalibrationComplete;
                    _screen = null;
                }

                if (!LiveKitCameraPublisher.HasInstance) return;
                LiveKitCameraPublisher.Instance.OnPublishingStarted -= OnPublishingStarted;
                LiveKitCameraPublisher.Instance.OnDisconnected -= OnDisconnected;
                LiveKitCameraPublisher.Instance.OnConnectionFailed -= OnFailed;
            }

            private void OnCalibrationComplete()
            {
                Thesis.AppConfig.CalibrationAcknowledged = true;
                LiveKitCameraPublisher.Instance.PublishNow();
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
