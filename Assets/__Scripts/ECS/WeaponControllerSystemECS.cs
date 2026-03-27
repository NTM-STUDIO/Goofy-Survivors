using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Collections;

[BurstCompile]
public partial struct WeaponControllerSystemECS : ISystem
{
    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        float deltaTime = SystemAPI.Time.DeltaTime;
        
        // 1. Tenta pegar os stats do player (assumindo que existe um singleton ou entidade com isso)
        // Se não existir, usa valores padrão (1.0f)
        PlayerStatsECS stats = new PlayerStatsECS 
        { 
            DamageMultiplier = 1, CooldownReduction = 0, AttackSpeedMultiplier = 1, 
            DurationMultiplier = 1, KnockbackMultiplier = 1, ProjectileSizeMultiplier = 1, ExtraProjectiles = 0 
        };

        if (SystemAPI.HasSingleton<PlayerStatsECS>())
        {
            stats = SystemAPI.GetSingleton<PlayerStatsECS>();
        }

        // 2. Prepara lista de inimigos para buscar alvo (Otimização: Spatial Query seria melhor, mas array serve por enquanto)
        var enemyQuery = SystemAPI.QueryBuilder().WithAll<EnemyTag, LocalTransform>().Build();
        var enemyEntities = enemyQuery.ToEntityArray(Allocator.Temp);
        var enemyTransforms = enemyQuery.ToComponentDataArray<LocalTransform>(Allocator.Temp);

        var ecb = new EntityCommandBuffer(Allocator.Temp);

        // 3. Atualiza cada arma
        foreach (var (weapon, visualState) in SystemAPI.Query<RefRW<WeaponControllerData>, RefRW<WeaponVisualState>>())
        {
            // Pega posição do Player
            float3 playerPos = float3.zero;
            Entity playerEntity = Entity.Null;
            if (SystemAPI.HasSingleton<PlayerPositionSingleton>())
            {
                playerPos = SystemAPI.GetSingleton<PlayerPositionSingleton>().Position + new float3(0, 1.0f, 0);
                playerEntity = SystemAPI.GetSingletonEntity<PlayerPositionSingleton>();
            }
            else
            {
                continue;
            }

            // Calcular Stats Finais
            float finalDamage = weapon.ValueRO.BaseDamage * stats.DamageMultiplier;
            float finalSpeed = weapon.ValueRO.BaseSpeed * stats.AttackSpeedMultiplier;
            float finalDuration = weapon.ValueRO.BaseDuration * stats.DurationMultiplier;
            float finalKnockback = weapon.ValueRO.BaseKnockback * stats.KnockbackMultiplier;
            float finalSize = weapon.ValueRO.BaseArea * stats.ProjectileSizeMultiplier;
            int totalProjectiles = weapon.ValueRO.ProjectileAmount + stats.ExtraProjectiles;

            // --- LÓGICA POR ARQUÉTIPO ---
            int archetype = weapon.ValueRO.Archetype;

            // === AURA (2) ===
            if (archetype == 2)
            {
                // 1. Visual Permanente
                if (!visualState.ValueRO.IsSpawned)
                {
                    Entity visual = ecb.Instantiate(weapon.ValueRO.WeaponPrefab);
                    ecb.AddComponent(visual, new Parent { Value = playerEntity }); // Cola no Player
                    ecb.AddComponent(visual, new LocalTransform { Position = float3.zero, Rotation = quaternion.identity, Scale = finalSize * 5.0f });
                    
                    visualState.ValueRW.VisualInstance = visual;
                    visualState.ValueRW.IsSpawned = true;
                }
                else
                {
                    // Atualiza tamanho da Aura se stats mudarem
                    if (state.EntityManager.Exists(visualState.ValueRO.VisualInstance))
                    {
                        var transform = state.EntityManager.GetComponentData<LocalTransform>(visualState.ValueRO.VisualInstance);
                        transform.Scale = finalSize * 5.0f;
                        ecb.SetComponent(visualState.ValueRO.VisualInstance, transform);
                    }
                }

                // 2. Dano Pulsante (Tick)
                weapon.ValueRW.CurrentCooldown -= deltaTime;
                if (weapon.ValueRW.CurrentCooldown <= 0f)
                {
                    float rangeSq = (finalSize * 5.0f) * (finalSize * 5.0f); // Raio ao quadrado

                    // Aplica dano em todos os inimigos na área
                    for (int k = 0; k < enemyTransforms.Length; k++)
                    {
                        if (math.distancesq(playerPos, enemyTransforms[k].Position) <= rangeSq)
                        {
                            // Aplica Dano (Adiciona ao buffer de dano do inimigo)
                            // Precisamos da Entity do inimigo, não só transform
                            Entity enemyEntity = enemyEntities[k];
                            ecb.AppendToBuffer(enemyEntity, new DamageBufferElement { Value = finalDamage });
                        }
                    }

                    // Reseta Cooldown
                    weapon.ValueRW.CurrentCooldown = weapon.ValueRO.BaseCooldown * (1.0f - stats.CooldownReduction);
                }
                return; // Aura tratada, sai do loop para esta arma
            }

            // === OUTRAS ARMAS (Disparo) ===
            weapon.ValueRW.CurrentCooldown -= deltaTime;
            if (weapon.ValueRW.CurrentCooldown <= 0f)
            {
                // 0 = Projectile, 1 = Whip, 3 = Orbit
                if (archetype == 3) // ORBIT
                {
                    float angleStep = (math.PI * 2) / totalProjectiles;

                    for (int i = 0; i < totalProjectiles; i++)
                    {
                        Entity projectile = ecb.Instantiate(weapon.ValueRO.WeaponPrefab);
                        
                        // Adiciona componente de órbita
                        ecb.AddComponent(projectile, new OrbitMovement
                        {
                            CenterEntity = playerEntity,
                            Radius = 3.0f * finalSize, // Raio base ajustável
                            Speed = finalSpeed,
                            Angle = i * angleStep // Distribui uniformemente
                        });

                        // Configura dados básicos (Dano, etc)
                        ecb.SetComponent(projectile, new ProjectileData
                        {
                            Speed = finalSpeed,
                            Direction = float3.zero, // Controlado pelo OrbitSystem
                            LifeTime = finalDuration,
                            Damage = finalDamage,
                            Knockback = finalKnockback,
                            PierceCount = -1 // Infinito para Orbiting Weapon
                        });
                        
                        // Posição inicial (será corrigida no primeiro frame do OrbitSystem)
                        ecb.SetComponent(projectile, LocalTransform.FromPosition(playerPos));
                    }
                }
                else // PROJECTILE (Default)
                {
                    for (int i = 0; i < totalProjectiles; i++)
                    {
                        float3 targetDir = new float3(1, 0, 0); // Default direita
                        
                        if (enemyTransforms.Length > 0)
                        {
                            // Acha o mais próximo (Simples)
                            float closestDist = float.MaxValue;
                            for (int k = 0; k < enemyTransforms.Length; k++)
                            {
                                float d = math.distancesq(playerPos, enemyTransforms[k].Position); // Usa spawnPos (Player)
                                if (d < closestDist)
                                {
                                    closestDist = d;
                                    targetDir = math.normalize(enemyTransforms[k].Position - playerPos);
                                }
                            }
                        }
                        else
                        {
                            // Sem inimigos: Aleatório ou frente
                            var random = Unity.Mathematics.Random.CreateFromIndex((uint)(SystemAPI.Time.ElapsedTime * 1000 + i));
                            float2 r = random.NextFloat2Direction();
                            targetDir = new float3(r.x, 0, r.y);
                        }

                        // Instancia o Projétil
                        Entity projectile = ecb.Instantiate(weapon.ValueRO.WeaponPrefab);
                        
                        // Configura Posição e Rotação
                        ecb.SetComponent(projectile, LocalTransform.FromPositionRotationScale(playerPos, quaternion.LookRotationSafe(targetDir, math.up()), finalSize));
                        
                        // Configura Dados do Projétil
                        ecb.SetComponent(projectile, new ProjectileData
                        {
                            Speed = finalSpeed,
                            Direction = targetDir,
                            LifeTime = finalDuration,
                            Damage = finalDamage,
                            Knockback = finalKnockback
                        });
                    }
                }

                // Reseta Cooldown
                weapon.ValueRW.CurrentCooldown = weapon.ValueRO.BaseCooldown * (1.0f - stats.CooldownReduction);
            }
        }

        ecb.Playback(state.EntityManager);
        ecb.Dispose();
        enemyEntities.Dispose();
        enemyTransforms.Dispose();
    }
}
