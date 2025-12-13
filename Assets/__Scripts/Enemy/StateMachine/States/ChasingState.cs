using UnityEngine;

namespace EnemyAI.States
{
    public class ChasingState : IEnemyState
    {
        public EnemyStateType StateType => EnemyStateType.Chasing;

        public void Enter(EnemyStateMachine ctx)
        {
            if (ctx.Movement != null && ctx.CurrentTarget != null)
                ctx.Movement.SetTarget(ctx.CurrentTarget);
        }

        public void Tick(EnemyStateMachine ctx)
        {
            if (ctx.Movement != null && ctx.CurrentTarget != null)
                ctx.Movement.SetTarget(ctx.CurrentTarget);
        }

        public void FixedTick(EnemyStateMachine ctx)
        {
            // Movement is handled by EnemyMovement component
            // Fallback for enemies without EnemyMovement
            if (ctx.Movement == null && ctx.CurrentTarget != null)
            {
                Vector3 dir = ctx.GetDirectionToTarget();
                if (dir.sqrMagnitude > 0.001f)
                {
                    ctx.Rb.linearVelocity = dir * ctx.Stats.moveSpeed;
                }
            }
        }

        public void Exit(EnemyStateMachine ctx) { }

        public EnemyStateType? CheckTransitions(EnemyStateMachine ctx)
        {
            if (ctx.Stats.CurrentHealth <= 0) return EnemyStateType.Dying;
            if (ctx.CurrentTarget == null) return EnemyStateType.Idle;
            if (ctx.IsTargetInRange() && ctx.CanAttack()) return EnemyStateType.Attacking;
            return null;
        }
    }
}
