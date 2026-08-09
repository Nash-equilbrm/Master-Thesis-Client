using Thesis.Patterns;
using Thesis.UI.Screens;

namespace Thesis.Managers
{
    public partial class CameraClientManager
    {
        private class IdleState : State<CameraClientManager>
        {
            public IdleState(CameraClientManager ctx) : base(ctx) { }

            public override void Enter()
            {
                if (UIManager.HasInstance)
                    UIManager.Instance.ShowScreen<ConnectionScreen>(forceShow: true);
            }
        }
    }
}