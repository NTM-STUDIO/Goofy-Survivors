using Unity.Entities;
using UnityEngine;

public class DamageVisualsAuthoring : MonoBehaviour
{
    public class Baker : Baker<DamageVisualsAuthoring>
    {
        public override void Bake(DamageVisualsAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new DamageVisualsSingleton());
            AddBuffer<DamageVisualEvent>(entity);
        }
    }
}
