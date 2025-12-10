using Unity.Burst;
using Unity.Entities;

[BurstCompile]
public partial struct DamageSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);

        foreach (var (stats, damageBuffer, entity) in SystemAPI.Query<RefRW<EnemyStatsData>, DynamicBuffer<DamageBufferElement>>().WithEntityAccess())
        {
            if (damageBuffer.IsEmpty) continue;

            foreach (var damage in damageBuffer)
            {
                stats.ValueRW.CurrentHealth -= damage.Value;
            }

            damageBuffer.Clear();

            if (stats.ValueRO.CurrentHealth <= 0)
            {
                ecb.DestroyEntity(entity);
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
