using UnityEngine;

namespace EnemyAI.States
{
    public class StunnedState : IEnemyState
    {
        public EnemyStateType StateType => EnemyStateType.Stunned;
        private float stunEndTime;

        public void Enter(EnemyStateMachine ctx)
        {
            ctx.Rb.linearVelocity = Vector3.zero;
            stunEndTime = Time.time + 0.5f;
        }

        public void Tick(EnemyStateMachine ctx) { }
        public void FixedTick(EnemyStateMachine ctx) => ctx.Rb.linearVelocity = Vector3.zero;
        public void Exit(EnemyStateMachine ctx) { }

        public EnemyStateType? CheckTransitions(EnemyStateMachine ctx)
        {
            if (ctx.Stats.CurrentHealth <= 0) return EnemyStateType.Dying;
            if (Time.time >= stunEndTime)
            {
                return ctx.CurrentTarget != null ? EnemyStateType.Chasing : EnemyStateType.Idle;
            }
            return null;
        }
    }
}
