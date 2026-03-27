using Unity.Entities;
using Unity.Mathematics;

namespace GoofySurvivors.ECS {
    public struct DecorationComponent : IComponentData {
        public float Lifetime;
    }

    public struct DecorationPrefabElement : IBufferElementData {
        public Entity Prefab;
    }

    public struct SharedDecorationData : IComponentData {
        public Entity ArchetypePrefab;
    }
}