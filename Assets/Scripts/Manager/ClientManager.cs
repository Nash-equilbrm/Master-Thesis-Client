using System;
using Thesis.Patterns;
using UnityEngine;

namespace Thesis.Managers
{
    public enum ClientState
    {
        Init,
        Idle,
        Streaming,
        Error
    }

    public partial class ClientManager : Singleton<ClientManager>
    {
        private StateMachine<ClientManager> _stateMachine;

        public ClientState CurrentState { get; private set; } = ClientState.Init;
        public string ErrorMessage { get; private set; }

        public event Action<ClientState> OnStateChanged;

        protected override void Awake()
        {
            base.Awake();
        }

        void Start()
        {
            _stateMachine = new StateMachine<ClientManager>();
            _stateMachine.Initialize(new InitState(this));
        }

        internal void ChangeState(State<ClientManager> newState, ClientState stateEnum)
        {
            CurrentState = stateEnum;
            _stateMachine.ChangeState(newState);
            OnStateChanged?.Invoke(stateEnum);
        }
    }
}
