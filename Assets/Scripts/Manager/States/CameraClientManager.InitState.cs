using UnityEngine;
using Thesis.Patterns;

namespace Thesis.Managers
{
    public partial class CameraClientManager
    {
        private class InitState : State<CameraClientManager>
        {
            public InitState(CameraClientManager ctx) : base(ctx) { }

            public override void Enter()
            {
                Screen.orientation = ScreenOrientation.LandscapeLeft;
                Screen.sleepTimeout = SleepTimeout.NeverSleep;
                Application.targetFrameRate = 60;

                _context.ChangeState(new IdleState(_context), CameraState.Idle);
            }
        }
    }
}
