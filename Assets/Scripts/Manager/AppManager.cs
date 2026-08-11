using System;
using Thesis.Patterns;

namespace Thesis.Managers
{
    public enum AppState { Init, RoomEntry, RoleSelect, AppRunning }

    public partial class AppManager : Singleton<AppManager>
    {
        private StateMachine<AppManager> _stateMachine;

        public AppState CurrentState { get; private set; } = AppState.Init;
        public event Action<AppState> OnStateChanged;

        private void Start()
        {
            _stateMachine = new StateMachine<AppManager>();
            _stateMachine.Initialize(new InitState(this));
        }

        internal void ChangeState(State<AppManager> newState, AppState stateEnum)
        {
            CurrentState = stateEnum;
            _stateMachine.ChangeState(newState);
            OnStateChanged?.Invoke(stateEnum);
        }
    }
}
