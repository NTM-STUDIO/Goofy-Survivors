using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;

[BurstCompile]
public partial struct ProjectileSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

        // Usa ProjectileData em vez de ProjectileMovementData
        foreach (var (transform, data, entity) in SystemAPI.Query<RefRW<LocalTransform>, RefRW<ProjectileData>>().WithEntityAccess())
        {
            // 1. Mover na direção definida
            transform.ValueRW.Position += data.ValueRO.Direction * data.ValueRO.Speed * deltaTime;

            // 2. Contar tempo de vida
            data.ValueRW.LifeTime -= deltaTime;
            if (data.ValueRO.LifeTime <= 0)
            {
                ecb.DestroyEntity(entity);
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
