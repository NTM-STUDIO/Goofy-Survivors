using UnityEngine;
using PlayerAI;

namespace PlayerAI.States
{
    public class DamagedState : IPlayerState
    {
        public PlayerStateType StateType => PlayerStateType.Damaged;

        private const float cameraAngleY = 45f;
        private const float horizontalNerfFactor = 0.56f;

        public void Enter(PlayerStateMachine ctx) 
        { 
            // DON'T change animation - keep running animation active
            // This allows the player to see movement continue even when taking damage
            // if (ctx.Animator != null)
            // {
            //     ctx.Animator.Play("Damaged");
            // }
        }

        public void Tick(PlayerStateMachine ctx) 
        {
             // Update animation to reflect current movement direction, 
             // so the player doesn't look "stuck" in the wrong direction while taking damage.
             UpdateAnimation(ctx);
        }

        private void UpdateAnimation(PlayerStateMachine ctx)
        {
            if (ctx.Animator == null) return;

            if (ctx.HasMoveInput)
            {
                // If moving, play the directional moving animation
                string targetAnim = (ctx.LastHorizontalDirection >= 0) ? "Moving_Right" : "Moving_Left";
                ctx.Animator.Play(targetAnim);
            }
            else
            {
                // If stopped, play Idle (or Damaged if you have one)
                ctx.Animator.Play("Idle");
            }
        }

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
