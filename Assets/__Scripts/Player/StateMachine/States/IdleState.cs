using PlayerAI;

namespace PlayerAI.States
{
    public class IdleState : IPlayerState
    {
        public PlayerStateType StateType => PlayerStateType.Idle;

        public void Enter(PlayerStateMachine ctx)
        {
            if (ctx.Animator != null)
            {
                // Dá reset ao Animator para a posição default e desativa-o para não sobrepor o Sprite estático
                ctx.Animator.Rebind();
                ctx.Animator.enabled = false;
            }

            if (ctx.SpriteRenderer != null)
            {
                // Atribui o Sprite escolhido no Inspector
                if (ctx.DefaultIdleSprite != null)
                {
                    ctx.SpriteRenderer.sprite = ctx.DefaultIdleSprite;
                    // Aplica a escala definida no Inspector no estado Idle
                    ctx.SpriteRenderer.transform.localScale = ctx.OriginalSpriteLocalScale * ctx.DefaultIdleSpriteScale;
                }

                // Se andou para a direita (> 0) -> flipX = true
                // Se andou para a esquerda (< 0) -> flipX = false
                ctx.SpriteRenderer.flipX = ctx.LastHorizontalDirection > 0;
            }

            if (ctx.Rb != null)
            {
                ctx.Rb.linearVelocity = UnityEngine.Vector3.zero;
            }
        }

        public void Tick(PlayerStateMachine ctx) 
        {
            if (ctx.SpriteRenderer != null)
            {
                ctx.SpriteRenderer.flipX = ctx.LastHorizontalDirection > 0;
            }
        }

        public void FixedTick(PlayerStateMachine ctx)
        {
            if (ctx.Rb != null)
            {
                ctx.Rb.linearVelocity = UnityEngine.Vector3.zero;
            }
        }

        public void Exit(PlayerStateMachine ctx) { }

        public PlayerStateType? CheckTransitions(PlayerStateMachine ctx)
        {
            if (ctx.Stats != null && ctx.Stats.IsDowned)
                return PlayerStateType.Downed;

            if (ctx.HasMoveInput)
                return PlayerStateType.Moving;

            // Don't transition to Damaged from Idle - let MovingState handle it
            // This prevents animation conflicts when taking damage while idle

            return null;
        }
    }
}
