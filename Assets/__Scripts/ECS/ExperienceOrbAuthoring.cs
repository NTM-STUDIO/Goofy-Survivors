using Unity.Entities;
using UnityEngine;

public class ExperienceOrbAuthoring : MonoBehaviour
{
    public float XPValue = 10f;

    public class Baker : Baker<ExperienceOrbAuthoring>
    {
        public override void Bake(ExperienceOrbAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            
            AddComponent(entity, new ExperienceOrbData { XPValue = authoring.XPValue });
            AddComponent(entity, new ExperienceOrbTag());
        }
    }
}
