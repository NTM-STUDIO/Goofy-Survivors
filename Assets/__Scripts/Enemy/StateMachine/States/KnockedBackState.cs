using UnityEngine;

namespace EnemyAI.States
{
    public class KnockedBackState : IEnemyState
    {
        public EnemyStateType StateType => EnemyStateType.KnockedBack;

        public void Enter(EnemyStateMachine ctx)
        {
            ctx.Stats.SetKnockedBack(true);
            ctx.Rb.linearVelocity = Vector3.zero;
            
            Vector3 force = ctx.KnockbackDirection.normalized * ctx.KnockbackForce;
            force.y = 0;
            ctx.Rb.AddForce(force, ForceMode.Impulse);
        }

        public void Tick(EnemyStateMachine ctx) { }
        public void FixedTick(EnemyStateMachine ctx) { }

        public void Exit(EnemyStateMachine ctx)
        {
            ctx.Stats.SetKnockedBack(false);
            ctx.Rb.linearVelocity = Vector3.zero;
        }

        public EnemyStateType? CheckTransitions(EnemyStateMachine ctx)
        {
            if (ctx.Stats.CurrentHealth <= 0) return EnemyStateType.Dying;
            if (Time.time >= ctx.KnockbackEndTime)
            {
                return ctx.CurrentTarget != null ? EnemyStateType.Chasing : EnemyStateType.Idle;
            }
            return null;
        }
    }
}
