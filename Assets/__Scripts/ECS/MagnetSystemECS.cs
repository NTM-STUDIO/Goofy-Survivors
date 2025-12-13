using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Collections;

[BurstCompile]
public partial struct MagnetSystemECS : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // 1. Pega a posição e o alcance do Player
        if (!SystemAPI.HasSingleton<PlayerPositionSingleton>()) return;
        
        float3 playerPos = SystemAPI.GetSingleton<PlayerPositionSingleton>().Position;
        
        // Assumindo que existe um PlayerStatsData singleton ou no player
        float pickupRange = 5f; // Valor base
        if (SystemAPI.HasSingleton<PlayerStatsData>())
        {
            pickupRange = SystemAPI.GetSingleton<PlayerStatsData>().PickupRange;
        }

        float deltaTime = SystemAPI.Time.DeltaTime;
        var ecb = new EntityCommandBuffer(Allocator.Temp);

        // 2. Verifica todos os orbes
        foreach (var (transform, orbData, entity) in SystemAPI.Query<RefRW<LocalTransform>, RefRO<ExperienceOrbData>>().WithAll<ExperienceOrbTag>().WithEntityAccess())
        {
            float distSq = math.distancesq(transform.ValueRO.Position, playerPos);

            // Se estiver dentro do alcance, puxa para o player
            if (distSq <= pickupRange * pickupRange)
            {
                float3 dir = math.normalize(playerPos - transform.ValueRO.Position);
                float moveSpeed = 15f; // Velocidade do ímã
                
                transform.ValueRW.Position += dir * moveSpeed * deltaTime;

                // Se estiver muito perto, coleta
                if (distSq < 0.5f)
                {
                    // Adiciona XP ao Buffer Global para ser processado pelo XpBridgeSystem
                    if (SystemAPI.HasSingleton<XpToProcessData>())
                    {
                        RefRW<XpToProcessData> xpData = SystemAPI.GetSingletonRW<XpToProcessData>();
                        xpData.ValueRW.Amount += orbData.ValueRO.XPValue;
                    }

                    ecb.DestroyEntity(entity);
                }
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}
