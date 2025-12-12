namespace EnemyAI
{
    public interface IEnemyState
    {
        EnemyStateType StateType { get; }
        void Enter(EnemyStateMachine ctx);
        void Tick(EnemyStateMachine ctx);
        void FixedTick(EnemyStateMachine ctx);
        void Exit(EnemyStateMachine ctx);
        EnemyStateType? CheckTransitions(EnemyStateMachine ctx);
    }
}
