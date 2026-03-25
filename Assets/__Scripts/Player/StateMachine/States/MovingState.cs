using UnityEngine;
using PlayerAI;

namespace PlayerAI.States
{
    public class MovingState : IPlayerState
    {
        public PlayerStateType StateType => PlayerStateType.Moving;

        private const float cameraAngleY = 45f;
        private const float horizontalNerfFactor = 0.56f;
        private const float idleTransitionDelay = 0.1f; // Buffer time before transitioning to Idle
        
        // Track current animation to avoid spamming Animator
        private string currentAnimState = "";
        private float timeWithoutInput = 0f;

        public void Enter(PlayerStateMachine ctx) 
        { 
            // Reset and force update on enter
            currentAnimState = "";
            timeWithoutInput = 0f;
            UpdateAnimation(ctx);
        }

        public void Tick(PlayerStateMachine ctx) 
        {
            // Track time without input for smoother transitions
            if (!ctx.HasMoveInput)
            {
                timeWithoutInput += Time.deltaTime;
            }
            else
            {
                timeWithoutInput = 0f;
            }
            
            UpdateAnimation(ctx);
        }

        private void UpdateAnimation(PlayerStateMachine ctx)
        {
            if (ctx.Animator == null) return;

            // Reativa o Animator, já que foi desligado no IdleState
            if (!ctx.Animator.enabled)
            {
                ctx.Animator.enabled = true;
            }

            string targetAnim = (ctx.LastHorizontalDirection >= 0) ? "Moving_Right" : "Moving_Left";

            if (targetAnim != currentAnimState)
            {
                ctx.Animator.Play(targetAnim);
                currentAnimState = targetAnim;
            }

            // Garante que a orientação e escala do sprite acompanham
            if (ctx.SpriteRenderer != null)
            {
                ctx.SpriteRenderer.flipX = ctx.LastHorizontalDirection > 0;
                // Devolve a escala do sprite ao normal ao andar
                ctx.SpriteRenderer.transform.localScale = ctx.OriginalSpriteLocalScale;
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

            // Add buffer time before transitioning to Idle to prevent flickering
            if (timeWithoutInput > idleTransitionDelay)
                return PlayerStateType.Idle;

            if (ctx.Stats != null && ctx.Stats.IsInvincible)
                return PlayerStateType.Damaged;

            return null;
        }
    }
}
