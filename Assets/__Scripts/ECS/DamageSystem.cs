using Unity.Burst;
using Unity.Entities;
using Unity.Physics;
using Unity.Physics.Systems;
using Unity.Collections;
using Unity.Transforms;
using Unity.Mathematics;

// Parte 1: Aplica o dano que já está no buffer (Lógica de Vida)
[UpdateInGroup(typeof(SimulationSystemGroup))]
[BurstCompile]
public partial struct ApplyDamageSystem : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var ecb = new EntityCommandBuffer(Unity.Collections.Allocator.Temp);
        
        // Tenta pegar o prefab do XP (se existir)
        Entity xpOrbPrefab = Entity.Null;
        if (SystemAPI.HasSingleton<GameAssetsData>())
        {
            xpOrbPrefab = SystemAPI.GetSingleton<GameAssetsData>().XpOrbPrefab;
        }

        // Prepara buffer de visuais (se existir)
        DynamicBuffer<DamageVisualEvent> visualBuffer = default;
        bool hasVisuals = SystemAPI.HasSingleton<DamageVisualsSingleton>();
        if (hasVisuals)
        {
            visualBuffer = SystemAPI.GetBuffer<DamageVisualEvent>(SystemAPI.GetSingletonEntity<DamageVisualsSingleton>());
        }

        foreach (var (stats, transform, damageBuffer, entity) in SystemAPI.Query<RefRW<EnemyStatsData>, RefRO<LocalTransform>, DynamicBuffer<DamageBufferElement>>().WithEntityAccess())
        {
            if (damageBuffer.IsEmpty) continue;

            foreach (var damage in damageBuffer)
            {
                stats.ValueRW.CurrentHealth -= damage.Value;

                // Adiciona evento visual
                if (hasVisuals)
                {
                    visualBuffer.Add(new DamageVisualEvent
                    {
                        Position = transform.ValueRO.Position + new float3(0, 1.5f, 0), // Um pouco acima da cabeça
                        Amount = (int)damage.Value,
                        IsCritical = false // TODO: Passar info de critico pelo buffer de dano se quiser
                    });
                }
            }

            damageBuffer.Clear();

            if (stats.ValueRO.CurrentHealth <= 0)
            {
                // Spawn XP Orb
                if (xpOrbPrefab != Entity.Null)
                {
                    Entity orb = ecb.Instantiate(xpOrbPrefab);
                    ecb.SetComponent(orb, LocalTransform.FromPosition(transform.ValueRO.Position));
                }

                ecb.DestroyEntity(entity);
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
    }
}

// Parte 2: Detecta colisão física e coloca o dano no buffer
[UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
[BurstCompile]
public partial struct CollisionDamageSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<SimulationSingleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var simulation = SystemAPI.GetSingleton<SimulationSingleton>();
        
        var job = new TriggerDamageJob
        {
            DamageBufferLookup = SystemAPI.GetBufferLookup<DamageBufferElement>(),
            EnemyTagLookup = SystemAPI.GetComponentLookup<EnemyTag>(true),
            ProjectileDataLookup = SystemAPI.GetComponentLookup<ProjectileData>(true),
            ProjectileTagLookup = SystemAPI.GetComponentLookup<ProjectileTag>(true),
            CommandBuffer = SystemAPI.GetSingleton<EndFixedStepSimulationEntityCommandBufferSystem.Singleton>().CreateCommandBuffer(state.WorldUnmanaged)
        };

        state.Dependency = job.Schedule(simulation, state.Dependency);
    }

    [BurstCompile]
    struct TriggerDamageJob : ITriggerEventsJob
    {
        public BufferLookup<DamageBufferElement> DamageBufferLookup;
        [ReadOnly] public ComponentLookup<EnemyTag> EnemyTagLookup;
        [ReadOnly] public ComponentLookup<ProjectileData> ProjectileDataLookup;
        [ReadOnly] public ComponentLookup<ProjectileTag> ProjectileTagLookup;
        public EntityCommandBuffer CommandBuffer;

        public void Execute(TriggerEvent triggerEvent)
        {
            Entity entityA = triggerEvent.EntityA;
            Entity entityB = triggerEvent.EntityB;

            // Caso 1: A é Projétil, B é Inimigo
            if (ProjectileTagLookup.HasComponent(entityA) && EnemyTagLookup.HasComponent(entityB))
            {
                ApplyDamage(entityB, entityA);
            }
            // Caso 2: B é Projétil, A é Inimigo
            else if (ProjectileTagLookup.HasComponent(entityB) && EnemyTagLookup.HasComponent(entityA))
            {
                ApplyDamage(entityA, entityB);
            }
        }

        void ApplyDamage(Entity enemy, Entity projectile)
        {
            // Pega o dano do projétil
            if (ProjectileDataLookup.HasComponent(projectile))
            {
                float damage = ProjectileDataLookup[projectile].Damage;

                // Adiciona ao buffer de dano do inimigo
                if (DamageBufferLookup.HasBuffer(enemy))
                {
                    var buffer = DamageBufferLookup[enemy];
                    buffer.Add(new DamageBufferElement { Value = damage });
                }

                // Destrói o projétil após o impacto
                CommandBuffer.DestroyEntity(projectile);
            }
        }
    }
}
