using UnityEngine;

namespace EnemyAI.States
{
    public class ChasingState : IEnemyState
    {
        public EnemyStateType StateType => EnemyStateType.Chasing;
        
        private string currentAnimState;

        public void Enter(EnemyStateMachine ctx)
        {
            if (ctx.Movement != null && ctx.CurrentTarget != null)
                ctx.Movement.SetTarget(ctx.CurrentTarget);
            
            UpdateDirectionalAnimation(ctx);
        }

        public void Tick(EnemyStateMachine ctx)
        {
            if (ctx.Movement != null && ctx.CurrentTarget != null)
                ctx.Movement.SetTarget(ctx.CurrentTarget);
                
            UpdateDirectionalAnimation(ctx);
        }

        private void UpdateDirectionalAnimation(EnemyStateMachine ctx)
        {
            if (ctx.Animator == null || ctx.CurrentTarget == null) return;

            // 1. Onde está o inimigo em relação ao Player? (Vetor Player -> Inimigo)
            Vector3 posRelativeToPlayer = ctx.transform.position - ctx.CurrentTarget.position;

            // 2. Rotacionar para alinhar com a tela (-45 graus)
            // X será a posição Horizontal na tela (Direita/Esquerda)
            // Z será a posição Vertical na tela (Cima/Baixo)
            Quaternion screenRotation = Quaternion.Euler(0, -45f, 0);
            Vector3 screenPos = screenRotation * posRelativeToPlayer;

            float x = screenPos.x; // >0 direita do player, <0 esquerda
            float z = screenPos.z; // >0 acima do player, <0 abaixo

            string targetAnim = "Moving_BottomRight"; 

            // 3. Escolher animação oposta à posição (se estou em Cima, corro para Baixo)
            if (z > 0) // Inimigo está ACIMA do player -> Corre para BAIXO
            {
                if (x > 0) // Inimigo está à DIREITA -> Corre para ESQUERDA
                    targetAnim = "Moving_BottomLeft";
                else       // Inimigo está à ESQUERDA -> Corre para DIREITA
                    targetAnim = "Moving_BottomRight";
            }
            else // Inimigo está ABAIXO do player -> Corre para CIMA
            {
                if (x > 0) // Inimigo está à DIREITA -> Corre para ESQUERDA
                    targetAnim = "Moving_TopLeft";
                else       // Inimigo está à ESQUERDA -> Corre para DIREITA
                    targetAnim = "Moving_TopRight";
            }

            if (currentAnimState != targetAnim)
            {
                ctx.Animator.Play(targetAnim);
                currentAnimState = targetAnim;
            }
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
