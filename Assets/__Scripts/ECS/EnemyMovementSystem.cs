using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
public partial struct EnemyMovementSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float3 targetPos = float3.zero;
        bool hasTarget = false;

        if (SystemAPI.HasSingleton<PlayerPositionSingleton>())
        {
            targetPos = SystemAPI.GetSingleton<PlayerPositionSingleton>().Position;
            hasTarget = true;
        }

        var deltaTime = SystemAPI.Time.DeltaTime;

        foreach (var (transform, movement, stats) in SystemAPI.Query<RefRW<LocalTransform>, RefRW<EnemyMovementData>, RefRO<EnemyStatsData>>().WithAll<EnemyTag>())
        {
            if (hasTarget)
            {
                movement.ValueRW.TargetPosition = targetPos;
                movement.ValueRW.HasTarget = true;
            }

            if (movement.ValueRO.HasTarget)
            {
                // Lógica replicada do EnemyMovement.cs (Fallback Isometric Logic)
                float3 direction = movement.ValueRO.TargetPosition - transform.ValueRO.Position;
                
                // Zera o Y (XZ plane only)
                direction.y = 0f;

                float distanceSq = math.lengthsq(direction);

                if (distanceSq > 0.001f)
                {
                    // Aplica a correção isométrica no eixo X (0.70710678f)
                    // Isso compensa a distorção visual da câmera isométrica
                    direction.x *= 0.70710678f;

                    float3 dirNormalized = math.normalize(direction);
                    
                    // Aplica a velocidade
                    transform.ValueRW.Position += dirNormalized * stats.ValueRO.MoveSpeed * deltaTime;
                }
            }
        }
    }
}
