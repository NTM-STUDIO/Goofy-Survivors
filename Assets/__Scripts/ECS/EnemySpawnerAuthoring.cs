using Unity.Entities;
using UnityEngine;

public class EnemySpawnerAuthoring : MonoBehaviour
{
    public GameObject SkeletonPrefab;
    public float SpawnRate = 2f;
    public float SpawnRadius = 10f;

    public class Baker : Baker<EnemySpawnerAuthoring>
    {
        public override void Bake(EnemySpawnerAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            
            AddComponent(entity, new EnemySpawnerData
            {
                PrefabToSpawn = GetEntity(authoring.SkeletonPrefab, TransformUsageFlags.Dynamic),
                SpawnRate = authoring.SpawnRate,
                NextSpawnTime = 0f,
                SpawnRadius = authoring.SpawnRadius
            });
        }
    }
}
