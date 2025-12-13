using UnityEngine;

namespace PlayerAI.States
{
    public class RevivingState : IPlayerState
    {
        public PlayerStateType StateType => PlayerStateType.Reviving;

        public void Enter(PlayerStateMachine ctx)
        {
            if (ctx.Rb != null)
            {
                ctx.Rb.linearVelocity = Vector3.zero;
            }
        }

        public void Tick(PlayerStateMachine ctx) { }

        public void FixedTick(PlayerStateMachine ctx)
        {
            if (ctx.Rb != null)
            {
                ctx.Rb.linearVelocity = Vector3.zero;
            }
        }

        public void Exit(PlayerStateMachine ctx)
        {
            if (ctx.Movement != null)
            {
                ctx.Movement.enabled = true;
            }
        }

        public PlayerStateType? CheckTransitions(PlayerStateMachine ctx)
        {
            if (ctx.Stats != null && !ctx.Stats.IsDowned && ctx.Stats.CurrentHp > 0)
            {
                return PlayerStateType.Idle;
            }

            return null;
        }
    }
}
