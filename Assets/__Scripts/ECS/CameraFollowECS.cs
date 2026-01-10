using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public class CameraFollowECS : MonoBehaviour
{
    public Vector3 Offset = new Vector3(0, 10, -10);
    public float SmoothSpeed = 5f;

    private EntityManager _entityManager;
    private Entity _playerEntity;

    void Start()
    {
        _entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
    }

    void LateUpdate()
    {
        // Tenta encontrar o Player se ainda não tivermos a referência
        if (_playerEntity == Entity.Null)
        {
            // Busca a primeira entidade que tenha a tag PlayerTag
            var query = _entityManager.CreateEntityQuery(typeof(PlayerTag));
            if (!query.IsEmpty)
            {
                _playerEntity = query.GetSingletonEntity();
            }
            return;
        }

        // Se o player existe, segue ele
        if (_entityManager.Exists(_playerEntity) && _entityManager.HasComponent<LocalTransform>(_playerEntity))
        {
            var playerTransform = _entityManager.GetComponentData<LocalTransform>(_playerEntity);
            Vector3 targetPos = (Vector3)playerTransform.Position + Offset;
            
            transform.position = Vector3.Lerp(transform.position, targetPos, SmoothSpeed * Time.deltaTime);
        }
    }
}
