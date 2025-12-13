using Unity.Entities;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Transforms; // Necessário para LocalTransform
using UnityEngine;
using System.Collections.Generic;

public class PlayerBridge : MonoBehaviour
{
    private Entity _playerEntity;
    private EntityManager _entityManager;
    private PlayerWeaponManager _weaponManager;
    private WeaponRegistry _weaponRegistry;

    // Fila de armas para adicionar (caso o ECS ainda não tenha carregado o Registry)
    private Queue<WeaponData> _pendingWeapons = new Queue<WeaponData>();

    void Start()
    {
        _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        _weaponManager = GetComponent<PlayerWeaponManager>();
        
        // Tenta pegar o Registry do WeaponManager se existir
        if (_weaponManager != null)
        {
            // Usamos Reflection ou acesso direto se for público, mas vamos assumir que conseguimos pegar o Registry
            // O PlayerWeaponManager tem um campo 'weaponRegistry' serializado.
            // Se não for público, teremos que arrastar manualmente no Inspector deste script também.
        }

        // Cria a entidade do Player com Tag, Posição e Stats
        _playerEntity = _entityManager.CreateEntity(
            typeof(PlayerPositionSingleton), 
            typeof(PlayerTag),
            typeof(PlayerStatsData),
            typeof(PlayerStatsECS),
            typeof(LocalTransform) // Adiciona LocalTransform para a câmera seguir
        );

        // Inicializa Stats Básicos
        _entityManager.SetComponentData(_playerEntity, new PlayerStatsData
        {
            CurrentHealth = 100,
            MaxHealth = 100,
            CurrentXP = 0,
            MaxXP = 100,
            Level = 1,
            PickupRange = 5f,
            MoveSpeed = 5f
        });

        _entityManager.SetComponentData(_playerEntity, new PlayerStatsECS
        {
            DamageMultiplier = 1f,
            CooldownReduction = 0f,
            AttackSpeedMultiplier = 1f,
            DurationMultiplier = 1f,
            KnockbackMultiplier = 1f,
            ProjectileSizeMultiplier = 1f,
            ExtraProjectiles = 0
        });
        
        // Inicializa Posição
        _entityManager.SetComponentData(_playerEntity, LocalTransform.FromPosition((float3)transform.position));

        // Inscreve no evento de adicionar arma
        if (_weaponManager != null)
        {
            _weaponManager.OnWeaponAdded += HandleWeaponAdded;
        }
    }

    private void HandleWeaponAdded(WeaponData weaponData)
    {
        _pendingWeapons.Enqueue(weaponData);
    }

    void Update()
    {
        if (_entityManager.Exists(_playerEntity))
        {
            float3 pos = (float3)transform.position;
            _entityManager.SetComponentData(_playerEntity, new PlayerPositionSingleton
            {
                Position = pos
            });
            
            // Atualiza LocalTransform também para a câmera seguir
            _entityManager.SetComponentData(_playerEntity, LocalTransform.FromPosition(pos));
        }

        // Processa armas pendentes
        if (_pendingWeapons.Count > 0)
        {
            ProcessPendingWeapons();
        }
    }

    private void ProcessPendingWeapons()
    {
        // Verifica se o Singleton do Registry existe
        var query = _entityManager.CreateEntityQuery(typeof(WeaponRegistryData));
        if (query.IsEmpty) return; // Ainda não carregou a SubScene

        var registryEntity = query.GetSingletonEntity();
        var registryData = _entityManager.GetComponentData<WeaponRegistryData>(registryEntity);
        var buffer = _entityManager.GetBuffer<WeaponPrefabElement>(registryData.WeaponPrefabBufferEntity);

        // Precisamos do WeaponRegistry (ScriptableObject) para saber o ID
        // Se não tivermos referência direta, podemos tentar achar pelo nome ou ID string se tiver
        // Mas o ideal é ter o WeaponRegistry aqui.
        if (_weaponRegistry == null && _weaponManager != null)
        {
            // Tenta pegar via GetComponent se não tiver exposto
            // Vamos assumir que o usuário vai arrastar para o campo public abaixo se falhar
        }

        while (_pendingWeapons.Count > 0)
        {
            var weaponData = _pendingWeapons.Peek();
            
            // Acha o ID da arma
            int weaponId = -1;
            
            // Procura no buffer pelo ID (assumindo que a ordem é a mesma do Registry)
            // O buffer tem {PrefabEntity, WeaponID}.
            // O WeaponID no buffer é o índice do Registry.
            
            // Como não temos o ID numérico fácil aqui sem o Registry, vamos iterar o buffer e comparar?
            // Não, o buffer só tem Entity e Int.
            
            // Solução: Precisamos do WeaponRegistry aqui.
            if (RegistryReference == null) 
            {
                Debug.LogWarning("PlayerBridge: WeaponRegistry reference missing!");
                return; 
            }

            weaponId = RegistryReference.GetWeaponId(weaponData);

            if (weaponId != -1)
            {
                // Acha o prefab no buffer
                Entity prefabEntity = Entity.Null;
                for(int i=0; i<buffer.Length; i++)
                {
                    if (buffer[i].WeaponID == weaponId)
                    {
                        prefabEntity = buffer[i].PrefabEntity;
                        break;
                    }
                }

                if (prefabEntity != Entity.Null)
                {
                    CreateWeaponEntity(weaponData, prefabEntity);
                    _pendingWeapons.Dequeue();
                }
                else
                {
                    Debug.LogWarning($"PlayerBridge: Weapon Prefab for ID {weaponId} not found in ECS Registry!");
                    _pendingWeapons.Dequeue(); // Remove para não travar, mas avisa
                }
            }
            else
            {
                _pendingWeapons.Dequeue();
            }
        }
    }

    private void CreateWeaponEntity(WeaponData data, Entity prefab)
    {
        var entity = _entityManager.CreateEntity(typeof(WeaponControllerData), typeof(WeaponVisualState));
        
        _entityManager.SetComponentData(entity, new WeaponControllerData
        {
            WeaponPrefab = prefab,
            BaseCooldown = data.cooldown,
            BaseDamage = data.damage,
            BaseSpeed = data.speed,
            BaseDuration = data.duration,
            BaseKnockback = data.knockback,
            ProjectileAmount = data.amount,
            BaseArea = data.area,
            Archetype = (int)data.archetype,
            CurrentCooldown = 0
        });

        _entityManager.SetComponentData(entity, new WeaponVisualState
        {
            IsSpawned = false,
            VisualInstance = Entity.Null
        });
        
        // Debug.Log($"ECS Weapon Created: {data.name}");
    }

    public WeaponRegistry RegistryReference; // Arraste no Inspector

    void OnDestroy()
    {
        if (_weaponManager != null)
        {
            _weaponManager.OnWeaponAdded -= HandleWeaponAdded;
        }

        if (_entityManager != null && _entityManager.Exists(_playerEntity))
        {
            _entityManager.DestroyEntity(_playerEntity);
        }
    }
}

