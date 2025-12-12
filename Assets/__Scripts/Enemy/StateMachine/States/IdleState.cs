using UnityEngine;

namespace EnemyAI.States
{
    public class IdleState : IEnemyState
    {
        public EnemyStateType StateType => EnemyStateType.Idle;

        public void Enter(EnemyStateMachine ctx) => ctx.Rb.linearVelocity = Vector3.zero;
        public void Tick(EnemyStateMachine ctx) { }
        public void FixedTick(EnemyStateMachine ctx) => ctx.Rb.linearVelocity = Vector3.zero;
        public void Exit(EnemyStateMachine ctx) { }

        public EnemyStateType? CheckTransitions(EnemyStateMachine ctx)
        {
            if (ctx.CurrentTarget != null) return EnemyStateType.Chasing;
            return null;
        }
    }
}
