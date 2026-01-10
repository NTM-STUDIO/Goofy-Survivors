using Unity.Entities;
using UnityEngine;

public struct GameAssetsData : IComponentData
{
    public Entity XpOrbPrefab;
}

public class GameAssetsAuthoring : MonoBehaviour
{
    public GameObject XpOrbPrefab;

    public class Baker : Baker<GameAssetsAuthoring>
    {
        public override void Bake(GameAssetsAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            
            AddComponent(entity, new GameAssetsData
            {
                XpOrbPrefab = GetEntity(authoring.XpOrbPrefab, TransformUsageFlags.Dynamic)
            });

            // Inicializa o Singleton de XP
            AddComponent(entity, new XpToProcessData { Amount = 0 });
        }
    }
}
