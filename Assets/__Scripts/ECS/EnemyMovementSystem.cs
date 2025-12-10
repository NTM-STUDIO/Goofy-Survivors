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
                float3 direction = movement.ValueRO.TargetPosition - transform.ValueRO.Position;
                float distanceSq = math.lengthsq(direction);

                if (distanceSq > 0.001f)
                {
                    float3 dirNormalized = math.normalize(direction);
                    transform.ValueRW.Position += dirNormalized * stats.ValueRO.MoveSpeed * deltaTime;

                    // Simple facing logic for 2D/2.5D
                    if (dirNormalized.x != 0)
                    {
                        // If moving left (x < 0), rotate 180 degrees on Y. If right, 0 degrees.
                        // Adjust this based on your sprite's default facing direction.
                        bool facingLeft = dirNormalized.x < 0;
                        transform.ValueRW.Rotation = quaternion.RotateY(facingLeft ? math.PI : 0);
                    }
                }
            }
        }
    }
}
