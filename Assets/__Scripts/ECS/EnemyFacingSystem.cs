using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
public partial struct EnemyFacingSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // Roda para todos os inimigos que têm referência visual e dados de movimento
        foreach (var (transform, movement, visualsRef) in SystemAPI.Query<RefRO<LocalTransform>, RefRO<EnemyMovementData>, RefRO<EnemyVisualsReference>>())
        {
            if (!movement.ValueRO.HasTarget) continue;

            Entity visualEntity = visualsRef.ValueRO.VisualsEntity;
            if (!state.EntityManager.Exists(visualEntity)) continue;

            LocalTransform visualTransform = state.EntityManager.GetComponentData<LocalTransform>(visualEntity);

            float3 direction = movement.ValueRO.TargetPosition - transform.ValueRO.Position;
            
            // Se estiver muito perto, não gira para evitar "flicker"
            if (math.lengthsq(direction) < 0.1f) continue;

            bool facingLeft = direction.x < 0;

            // Lógica de Flip Simples (Escala X)
            // Isso preserva a rotação isométrica original (30, 45, 0) e apenas espelha o sprite
            float currentScaleX = math.abs(visualTransform.Scale); // Assume escala uniforme base
            
            // Se o sprite original olha para a direita:
            // Esquerda = Scale -1
            // Direita = Scale 1
            
            // Nota: LocalTransform tem apenas 'Scale' (uniforme). 
            // Para escala não-uniforme (X negativo), precisamos usar PostTransformMatrix ou mudar a rotação.
            // Como LocalTransform não suporta escala negativa em um eixo só facilmente sem Matrix,
            // Vamos usar a Rotação Y para flipar, mas mantendo a inclinação X.

            // Rotação Base (Direita): X=30, Y=45, Z=0
            // Rotação Flip (Esquerda): X=30, Y=225 (45+180), Z=0  <-- Isso gira "por trás"
            // OU Espelhar via Y axis rotation relativa.

            if (facingLeft)
            {
                // Vira para a esquerda (Flip Horizontal visual)
                // Se o sprite é 2D billboarded, Y=180 resolve.
                // Se é isométrico 3D, precisamos ajustar.
                // Vamos tentar a rotação que você usava no editor:
                // Direita: (30, 45, 0)
                // Esquerda: (30, 135, 0) ?? Teste visual
                visualTransform.Rotation = quaternion.Euler(math.radians(30), math.radians(135), 0);
            }
            else
            {
                // Vira para a direita (Normal)
                visualTransform.Rotation = quaternion.Euler(math.radians(30), math.radians(45), 0);
            }

            state.EntityManager.SetComponentData(visualEntity, visualTransform);
        }
    }
}
