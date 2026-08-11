using DG.Tweening;
using Thesis.Patterns;
using UnityEngine;

namespace Thesis.Managers
{
    public partial class AppManager
    {
        private class InitState : State<AppManager>
        {
            public InitState(AppManager ctx) : base(ctx) { }

            public override void Enter()
            {
                Screen.orientation      = ScreenOrientation.LandscapeLeft;
                Screen.sleepTimeout     = SleepTimeout.NeverSleep;
                Application.targetFrameRate = 60;

                DOTween.Init();

                _context.ChangeState(new RoleSelectState(_context), AppState.RoleSelect);
            }
        }
    }
}
