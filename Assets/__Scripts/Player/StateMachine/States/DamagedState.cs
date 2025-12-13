using UnityEngine;

namespace PlayerAI.States
{
    public class DamagedState : IPlayerState
    {
        public PlayerStateType StateType => PlayerStateType.Damaged;

        private const float cameraAngleY = 45f;
        private const float horizontalNerfFactor = 0.56f;

        public void Enter(PlayerStateMachine ctx) { }

        public void Tick(PlayerStateMachine ctx) { }

        public void FixedTick(PlayerStateMachine ctx)
        {
            if (ctx.Rb == null || ctx.Stats == null) return;

            Vector3 moveInput = ctx.MoveInput;
            
            if (moveInput.sqrMagnitude < 0.0001f)
            {
                ctx.Rb.linearVelocity = Vector3.zero;
                return;
            }

            Vector3 finalInput = moveInput;
            finalInput.x *= horizontalNerfFactor;

            Quaternion rotation = Quaternion.Euler(0, cameraAngleY, 0);
            Vector3 movementDirection = rotation * finalInput;

            float diagonalCompensation = 1f;
            if (Mathf.Abs(moveInput.x) > 0.1f && Mathf.Abs(moveInput.z) > 0.1f)
                diagonalCompensation = 1f / Mathf.Sqrt(2f);

            ctx.Rb.linearVelocity = movementDirection * ctx.Stats.movementSpeed * diagonalCompensation;
        }

        public void Exit(PlayerStateMachine ctx) { }

        public PlayerStateType? CheckTransitions(PlayerStateMachine ctx)
        {
            if (ctx.Stats != null && ctx.Stats.IsDowned)
                return PlayerStateType.Downed;

            if (ctx.Stats != null && !ctx.Stats.IsInvincible)
            {
                return ctx.HasMoveInput ? PlayerStateType.Moving : PlayerStateType.Idle;
            }

            return null;
        }
    }
}
