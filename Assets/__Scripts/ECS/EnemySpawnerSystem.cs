using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;

[BurstCompile]
public partial struct EnemySpawnerSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        if (!SystemAPI.HasSingleton<PlayerPositionSingleton>()) return;
        float3 playerPos = SystemAPI.GetSingleton<PlayerPositionSingleton>().Position;

        double currentTime = SystemAPI.Time.ElapsedTime;
        
        // Usamos o GlobalSystemVersion como semente simples, mas para aleatoriedade real por frame/entidade
        // é ideal ter um componente Random. Para este exemplo simples, usaremos um Random temporário.
        var random = Unity.Mathematics.Random.CreateFromIndex((uint)(currentTime * 1000));

        foreach (var (spawner, transform) in SystemAPI.Query<RefRW<EnemySpawnerData>, RefRO<LocalTransform>>())
        {
            if (currentTime >= spawner.ValueRO.NextSpawnTime)
            {
                var entity = state.EntityManager.Instantiate(spawner.ValueRO.PrefabToSpawn);
                
                // Calcular posição aleatória em círculo (XZ plane)
                float2 randomCircle = random.NextFloat2Direction() * spawner.ValueRO.SpawnRadius;
                float3 spawnPos = playerPos + new float3(randomCircle.x, 0, randomCircle.y);

                // Define a posição inicial
                state.EntityManager.SetComponentData(entity, LocalTransform.FromPosition(spawnPos));

                spawner.ValueRW.NextSpawnTime = (float)currentTime + spawner.ValueRO.SpawnRate;
            }
        }
    }
}
