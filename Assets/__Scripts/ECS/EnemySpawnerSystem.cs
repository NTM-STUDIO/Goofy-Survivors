using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;

[BurstCompile]
public partial struct EnemySpawnerSystem : ISystem
{
// [BurstCompile] // Comentado para Debug
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.HasSingleton<PlayerPositionSingleton>()) 
        {
            UnityEngine.Debug.LogWarning("EnemySpawnerSystem: PlayerPositionSingleton não encontrado! Certifique-se de que o script 'PlayerBridge' está no Player.");
            return;
        }
        
        float3 playerPos = SystemAPI.GetSingleton<PlayerPositionSingleton>().Position;

        double currentTime = SystemAPI.Time.ElapsedTime;
        var random = Unity.Mathematics.Random.CreateFromIndex((uint)(currentTime * 1000));

        int spawnerCount = 0;
        // REMOVIDO: RefRO<LocalTransform> da query, pois não estamos usando a posição do spawner, mas sim do player.
        // Isso garante que o spawner seja encontrado mesmo se não tiver Transform.
        foreach (var spawner in SystemAPI.Query<RefRW<EnemySpawnerData>>())
        {
            spawnerCount++;
            if (spawner.ValueRO.PrefabToSpawn == Entity.Null)
            {
                UnityEngine.Debug.LogError("EnemySpawnerSystem: PrefabToSpawn é NULL! Verifique se arrastou o Prefab do Esqueleto no Inspector do Spawner.");
                continue;
            }

            if (currentTime >= spawner.ValueRO.NextSpawnTime)
            {
                UnityEngine.Debug.Log($"EnemySpawnerSystem: Spawnando inimigo em {currentTime}");
                var entity = state.EntityManager.Instantiate(spawner.ValueRO.PrefabToSpawn);
                
                float2 randomCircle = random.NextFloat2Direction() * spawner.ValueRO.SpawnRadius;
                float3 spawnPos = playerPos + new float3(randomCircle.x, 0, randomCircle.y);

                state.EntityManager.SetComponentData(entity, LocalTransform.FromPosition(spawnPos));

                spawner.ValueRW.NextSpawnTime = (float)currentTime + spawner.ValueRO.SpawnRate;
            }
        }

        if (spawnerCount == 0)
        {
            UnityEngine.Debug.LogWarning("EnemySpawnerSystem: Nenhum componente 'EnemySpawnerData' encontrado! Verifique se o GameObject do Spawner está na SubScene e tem o script Authoring.");
        }
    }
}
