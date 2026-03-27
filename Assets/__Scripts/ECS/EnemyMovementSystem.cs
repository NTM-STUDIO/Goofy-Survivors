using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Physics;

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

        // 1. Inimigos SEM Física (Movemos o Transform diretamente)
        foreach (var (transform, movement, stats) in SystemAPI.Query<RefRW<LocalTransform>, RefRW<EnemyMovementData>, RefRO<EnemyStatsData>>()
                 .WithAll<EnemyTag>()
                 .WithNone<PhysicsVelocity>())
        {
            if (hasTarget)
            {
                movement.ValueRW.TargetPosition = targetPos;
                movement.ValueRW.HasTarget = true;
            }

            if (movement.ValueRO.HasTarget)
            {
                float3 direction = movement.ValueRO.TargetPosition - transform.ValueRO.Position;
                direction.y = 0f;

                float distanceSq = math.lengthsq(direction);

                if (distanceSq > 0.001f)
                {
                    // Correção isométrica
                    direction.x *= 0.70710678f;
                    float3 dirNormalized = math.normalize(direction);
                    
                    transform.ValueRW.Position += dirNormalized * stats.ValueRO.MoveSpeed * deltaTime;
                }
            }
        }

        // 2. Inimigos COM Física (Definimos a Velocidade)
        foreach (var (transform, movement, stats, velocity) in SystemAPI.Query<RefRO<LocalTransform>, RefRW<EnemyMovementData>, RefRO<EnemyStatsData>, RefRW<PhysicsVelocity>>()
                 .WithAll<EnemyTag>())
        {
            if (hasTarget)
            {
                movement.ValueRW.TargetPosition = targetPos;
                movement.ValueRW.HasTarget = true;
            }

            if (movement.ValueRO.HasTarget)
            {
                float3 direction = movement.ValueRO.TargetPosition - transform.ValueRO.Position;
                direction.y = 0f;

                float distanceSq = math.lengthsq(direction);

                if (distanceSq > 0.001f)
                {
                    // Correção isométrica
                    direction.x *= 0.70710678f;
                    float3 dirNormalized = math.normalize(direction);
                    
                    // Define a velocidade linear
                    float3 newVel = dirNormalized * stats.ValueRO.MoveSpeed;
                    newVel.y = 0f; 
                    
                    velocity.ValueRW.Linear = newVel;
                }
                else
                {
                    velocity.ValueRW.Linear = float3.zero;
                }
            }
        }
    }
}
