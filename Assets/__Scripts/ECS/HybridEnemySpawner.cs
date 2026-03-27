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

        // --- DESATIVA COMPONENTES ANTIGOS PARA EVITAR CONFLITOS ---
        // O ECS agora controla o movimento. Se os scripts antigos estiverem ativos,
        // eles vão lutar pelo controle da posição/velocidade.
        
        var oldMovement = visualGO.GetComponent<EnemyMovement>();
        if (oldMovement != null) oldMovement.enabled = false;

        // Tenta desativar o RobustIsometricController pelo nome (para não precisar da referência do tipo)
        var allScripts = visualGO.GetComponents<MonoBehaviour>();
        foreach (var script in allScripts)
        {
            if (script.GetType().Name == "RobustIsometricController")
            {
                script.enabled = false;
                // Debug.Log($"[HybridEnemySpawner] Desativado RobustIsometricController em {visualGO.name}");
            }
        }

        // Tenta desativar o RobustIsometricController se existir (via Reflection ou GetComponent se soubermos o tipo)
        // Como não temos o script aqui, usamos GetComponent(string) ou assumimos que o usuário deve desativar no prefab.
        // Mas vamos tentar desativar componentes genéricos de movimento se possível.
        var rbs = visualGO.GetComponentsInChildren<Rigidbody>();
        foreach (var rb in rbs)
        {
            // Não podemos remover o Rigidbody se precisarmos de colisão física,
            // mas podemos torná-lo Kinematic se o ECS for controlar a posição diretamente.
            // POREM, o EnemyVisualSyncSystem agora usa MovePosition, então Dynamic RB é ok.
            // rb.isKinematic = true; 
        }

        // Desativa NetworkTransform para evitar que o Netcode tente sincronizar a posição
        // (O ECS deve ser a autoridade local, ou sincronizado via ECS Netcode se implementado)
        var netTransform = visualGO.GetComponent<Unity.Netcode.Components.NetworkTransform>();
        if (netTransform != null) netTransform.enabled = false;

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
