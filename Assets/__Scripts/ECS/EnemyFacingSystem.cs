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

            // Rotações fixas para Isométrico 2.5D
            // Direita: X=30, Y=45
            // Esquerda: X=30, Y=135 (Espelhado no eixo Y visualmente para trás) ou Y=-45 (Espelhado para frente)
            // Teste qual fica melhor com seu sprite!
            
            quaternion rotationRight = quaternion.Euler(math.radians(30), math.radians(45), 0);
            // Tente 135 se o sprite ficar de costas, ou -45 se ficar de frente
            quaternion rotationLeft = quaternion.Euler(math.radians(30), math.radians(135), 0); 

            visualTransform.Rotation = facingLeft ? rotationLeft : rotationRight;

            state.EntityManager.SetComponentData(visualEntity, visualTransform);
        }
    }
}
