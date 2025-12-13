using UnityEngine;

namespace EnemyAI.States
{
    public class DyingState : IEnemyState
    {
        public EnemyStateType StateType => EnemyStateType.Dying;
        private const float DEATH_DURATION = 0.3f;

        public void Enter(EnemyStateMachine ctx)
        {
            ctx.Rb.linearVelocity = Vector3.zero;
            ctx.Rb.isKinematic = true;
            
            var collider = ctx.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
        }

        public void Tick(EnemyStateMachine ctx) { }
        public void FixedTick(EnemyStateMachine ctx) { }
        public void Exit(EnemyStateMachine ctx) { }

        public EnemyStateType? CheckTransitions(EnemyStateMachine ctx)
        {
            if (ctx.TimeInCurrentState >= DEATH_DURATION) return EnemyStateType.Dead;
            return null;
        }
    }
}
