using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;

// Sistema ECS puro para spawn de inimigos
// Requer: 
// 1. PlayerBridge no Player (cria PlayerPositionSingleton)
// 2. EnemySpawnerAuthoring na SubScene com prefab configurado
[BurstCompile]
public partial struct EnemySpawnerSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        // Espera pelo player existir
        if (!SystemAPI.HasSingleton<PlayerPositionSingleton>()) return;
        
        float3 playerPos = SystemAPI.GetSingleton<PlayerPositionSingleton>().Position;

        double currentTime = SystemAPI.Time.ElapsedTime;
        var random = Unity.Mathematics.Random.CreateFromIndex((uint)(currentTime * 1000));

        foreach (var spawner in SystemAPI.Query<RefRW<EnemySpawnerData>>())
        {
            if (spawner.ValueRO.PrefabToSpawn == Entity.Null) continue;

            if (currentTime >= spawner.ValueRO.NextSpawnTime)
            {
                var entity = state.EntityManager.Instantiate(spawner.ValueRO.PrefabToSpawn);
                
                float2 randomCircle = random.NextFloat2Direction() * spawner.ValueRO.SpawnRadius;
                float3 spawnPos = playerPos + new float3(randomCircle.x, 0, randomCircle.y);

                state.EntityManager.SetComponentData(entity, LocalTransform.FromPosition(spawnPos));

                spawner.ValueRW.NextSpawnTime = (float)currentTime + spawner.ValueRO.SpawnRate;
            }
        }
    }
}
