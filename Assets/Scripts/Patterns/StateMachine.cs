namespace Thesis.Patterns
{
    public class StateMachine<T>
    {
        public State<T> CurrentState { get; private set; }
        public State<T> PreviousState { get; private set; }

        public void Initialize(State<T> startingState)
        {
            CurrentState = startingState;
            startingState.Enter();
        }

        public void ChangeState(State<T> newState)
        {
            CurrentState.Exit();
            PreviousState = CurrentState;
            CurrentState = newState;
            newState.Enter();
        }
    }

    public abstract class State<T>
    {
        protected T _context;

        public State(T context)
        {
            _context = context;
        }

        public virtual void Enter() { }
        public virtual void HandleInput() { }
        public virtual void LogicUpdate() { }
        public virtual void PhysicsUpdate() { }
        public virtual void Exit() { }
    }
}
