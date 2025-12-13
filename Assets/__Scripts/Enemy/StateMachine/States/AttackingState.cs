using UnityEngine;

namespace EnemyAI.States
{
    public class AttackingState : IEnemyState
    {
        public EnemyStateType StateType => EnemyStateType.Attacking;
        private const float ATTACK_DURATION = 0.3f;

        public void Enter(EnemyStateMachine ctx)
        {
            ctx.Rb.linearVelocity = Vector3.zero;
            ctx.LastAttackTime = Time.time;
        }

        public void Tick(EnemyStateMachine ctx) { }
        public void FixedTick(EnemyStateMachine ctx) => ctx.Rb.linearVelocity = Vector3.zero;
        public void Exit(EnemyStateMachine ctx) { }

        public EnemyStateType? CheckTransitions(EnemyStateMachine ctx)
        {
            if (ctx.Stats.CurrentHealth <= 0) return EnemyStateType.Dying;
            if (ctx.TimeInCurrentState >= ATTACK_DURATION) return EnemyStateType.Chasing;
            return null;
        }
    }
}
