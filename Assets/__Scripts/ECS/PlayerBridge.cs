using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

public class PlayerBridge : MonoBehaviour
{
    private Entity _playerEntity;
    private EntityManager _entityManager;

    void Start()
    {
        _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        _playerEntity = _entityManager.CreateEntity(typeof(PlayerPositionSingleton));
    }

    void Update()
    {
        if (_entityManager.Exists(_playerEntity))
        {
            _entityManager.SetComponentData(_playerEntity, new PlayerPositionSingleton
            {
                Position = (float3)transform.position
            });
        }
    }

    void OnDestroy()
    {
        if (_entityManager != null && _entityManager.Exists(_playerEntity))
        {
            _entityManager.DestroyEntity(_playerEntity);
        }
    }
}
