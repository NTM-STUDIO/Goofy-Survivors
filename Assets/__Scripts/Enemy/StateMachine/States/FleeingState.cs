using UnityEngine;

namespace EnemyAI.States
{
    public class FleeingState : IEnemyState
    {
        public EnemyStateType StateType => EnemyStateType.Fleeing;
        private const float FLEE_SPEED_MULT = 1.2f;
        private const float SAFE_DISTANCE_MULT = 5f;

        public void Enter(EnemyStateMachine ctx) { }
        public void Tick(EnemyStateMachine ctx) { }

        public void FixedTick(EnemyStateMachine ctx)
        {
            if (ctx.CurrentTarget == null)
            {
                ctx.Rb.linearVelocity = Vector3.zero;
                return;
            }

            Vector3 dir = -ctx.GetDirectionToTarget();
            if (dir.sqrMagnitude > 0.001f)
            {
                ctx.Rb.linearVelocity = dir * ctx.Stats.moveSpeed * FLEE_SPEED_MULT;
            }
        }

        public void Exit(EnemyStateMachine ctx) { }

        public EnemyStateType? CheckTransitions(EnemyStateMachine ctx)
        {
            if (ctx.Stats.CurrentHealth <= 0) return EnemyStateType.Dying;
            if (ctx.CurrentTarget == null) return EnemyStateType.Idle;
            if (ctx.GetDistanceToTarget() > ctx.AttackRange * SAFE_DISTANCE_MULT) return EnemyStateType.Chasing;
            return null;
        }
    }
}
