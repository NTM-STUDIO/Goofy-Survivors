using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

// Spawner Híbrido: Cria Entidade ECS + GameObject Visual separadamente
// Isso permite manter o RobustIsometricController funcionando!
public class HybridEnemySpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    public GameObject enemyPrefab; // Prefab ORIGINAL com RobustIsometricController
    public float spawnRate = 1f;
    public float spawnRadius = 15f;
    public int maxEnemies = 200;

    private EntityManager entityManager;
    private float nextSpawnTime;
    private EntityQuery playerQuery;
    private bool initialized = false;

    void Start()
    {
        TryInitialize();
    }

    void TryInitialize()
    {
        if (initialized) return;
        
        // Espera pelo World ECS estar pronto
        if (World.DefaultGameObjectInjectionWorld == null) return;
        
        entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        playerQuery = entityManager.CreateEntityQuery(typeof(PlayerPositionSingleton));
        initialized = true;
    }

    void Update()
    {
        // Tenta inicializar se ainda não conseguiu
        if (!initialized)
        {
            TryInitialize();
            return;
        }
        
        // Espera pelo player
        if (playerQuery.IsEmpty) return;

        if (Time.time < nextSpawnTime) return;
        nextSpawnTime = Time.time + spawnRate;

        // Conta inimigos atuais (simplificado)
        int currentCount = GameObject.FindGameObjectsWithTag("Enemy").Length;
        if (currentCount >= maxEnemies) return;

        // Pega posição do player
        var playerSingleton = playerQuery.GetSingleton<PlayerPositionSingleton>();
        float3 playerPos = playerSingleton.Position;

        // Calcula posição de spawn
        Vector2 randomCircle = UnityEngine.Random.insideUnitCircle.normalized * spawnRadius;
        Vector3 spawnPos = new Vector3(playerPos.x + randomCircle.x, 0, playerPos.z + randomCircle.y);

        // Spawn do GameObject visual (com RobustIsometricController!)
        GameObject visualGO = Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
        visualGO.tag = "Enemy"; // Garante que tem a tag para colisão

        // Cria a Entidade ECS para a lógica
        Entity entity = entityManager.CreateEntity(
            typeof(LocalTransform),
            typeof(EnemyTag),
            typeof(EnemyStatsData),
            typeof(EnemyMovementData)
        );

        // Configura a entidade
        var authoring = enemyPrefab.GetComponent<SkeletonAuthoring>();
        float health = authoring != null ? authoring.Health : 100f;
        float damage = authoring != null ? authoring.Damage : 10f;
        float speed = authoring != null ? authoring.Speed : 3f;

        entityManager.SetComponentData(entity, LocalTransform.FromPosition(spawnPos));
        entityManager.SetComponentData(entity, new EnemyStatsData
        {
            CurrentHealth = health,
            MaxHealth = health,
            Damage = damage,
            MoveSpeed = speed
        });
        entityManager.SetComponentData(entity, new EnemyMovementData
        {
            TargetPosition = float3.zero,
            HasTarget = false
        });

        // Liga o visual à entidade
        entityManager.AddComponentObject(entity, new EnemyVisualGameObject { Visual = visualGO });

        // Adiciona buffer de dano
        entityManager.AddBuffer<DamageBufferElement>(entity);

        // Guarda referência da entidade no GameObject para cleanup E para receber dano
        var bridge = visualGO.GetComponent<EnemyEntityBridge>();
        if (bridge == null)
        {
            bridge = visualGO.AddComponent<EnemyEntityBridge>();
        }
        bridge.LinkedEntity = entity;
        bridge.EntityManager = entityManager;
    }
}

// Componente para ligar GameObject à Entity (para destruição sincronizada E receber dano)
public class EnemyEntityBridge : MonoBehaviour
{
    public Entity LinkedEntity;
    public EntityManager EntityManager;

    // Chamado pelo sistema de armas (via trigger do GameObject)
    public void TakeDamage(float damage)
    {
        if (EntityManager == default || !EntityManager.Exists(LinkedEntity)) return;

        // Adiciona dano ao buffer ECS
        if (EntityManager.HasBuffer<DamageBufferElement>(LinkedEntity))
        {
            var buffer = EntityManager.GetBuffer<DamageBufferElement>(LinkedEntity);
            buffer.Add(new DamageBufferElement { Value = damage });
        }
    }

    void OnDestroy()
    {
        // Quando o GameObject é destruído, destrói a entidade também
        if (World.DefaultGameObjectInjectionWorld != null && 
            World.DefaultGameObjectInjectionWorld.EntityManager.Exists(LinkedEntity))
        {
            World.DefaultGameObjectInjectionWorld.EntityManager.DestroyEntity(LinkedEntity);
        }
    }
}
