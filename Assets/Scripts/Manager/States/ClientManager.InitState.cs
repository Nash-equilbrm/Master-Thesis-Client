using Thesis.Patterns;
using UnityEngine;

namespace Thesis.Managers
{
    public partial class ClientManager
    {
        private class InitState : State<ClientManager>
        {
            public InitState(ClientManager ctx) : base(ctx) { }

            public override void Enter()
            {
                Screen.orientation  = ScreenOrientation.LandscapeLeft;
                Screen.sleepTimeout = SleepTimeout.NeverSleep;
                Application.targetFrameRate = 60;

                _context.ChangeState(new IdleState(_context), ClientState.Idle);
            }
        }
    }
}
