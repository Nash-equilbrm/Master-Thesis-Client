using Thesis.Patterns;

namespace Thesis.Managers
{
    public partial class AppManager
    {
        private class AppRunningState : State<AppManager>
        {
            public AppRunningState(AppManager ctx) : base(ctx) { }

            // Role-specific managers own the flow from here via their own Start().
        }
    }
}
