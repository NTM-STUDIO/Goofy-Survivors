using Unity.Entities;

public struct EnemySpawnerData : IComponentData
{
    public Entity PrefabToSpawn;
    public float SpawnRate;
    public float NextSpawnTime;
    public float SpawnRadius;
}
