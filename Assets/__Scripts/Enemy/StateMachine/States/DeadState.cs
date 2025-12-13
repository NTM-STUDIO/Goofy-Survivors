namespace EnemyAI.States
{
    public class DeadState : IEnemyState
    {
        public EnemyStateType StateType => EnemyStateType.Dead;

        public void Enter(EnemyStateMachine ctx) => ctx.Stats.Die();
        public void Tick(EnemyStateMachine ctx) { }
        public void FixedTick(EnemyStateMachine ctx) { }
        public void Exit(EnemyStateMachine ctx) { }
        public EnemyStateType? CheckTransitions(EnemyStateMachine ctx) => null;
    }
}
