using System;
using Thesis.Patterns;
using Thesis.UI.Screens;
using UnityEngine;

namespace Thesis.Managers
{
    public enum CameraState
    {
        Init,
        Idle,
        Registering,
        Connecting,
        Streaming,
        Error
    }

    public partial class CameraClientManager : Singleton<CameraClientManager>
    {
        private StateMachine<CameraClientManager> _stateMachine;

        public CameraState CurrentState { get; private set; } = CameraState.Init;
        public string ErrorMessage { get; private set; }

        public event Action<CameraState> OnStateChanged;

        private void Start()
        {
            _stateMachine = new StateMachine<CameraClientManager>();
            _stateMachine.Initialize(new InitState(this));
        }

        internal void StartRegistering()
        {
            ChangeState(new RegisteringState(this), CameraState.Registering);
        }

        internal void ChangeState(State<CameraClientManager> newState, CameraState stateEnum)
        {
            CurrentState = stateEnum;
            _stateMachine.ChangeState(newState);
            OnStateChanged?.Invoke(stateEnum);
        }
    }
}
