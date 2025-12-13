using Unity.Entities;
using UnityEngine;

// Coloque este script no PREFAB do Projétil (Bala, Magia, etc)
public class ProjectileAuthoring : MonoBehaviour
{
    // Estes valores são sobrescritos pelo WeaponControllerSystem na hora do tiro,
    // mas servem como padrão se você arrastar o prefab na cena para testar.
    public float Damage = 10f;
    public float Speed = 10f;
    public float LifeTime = 5f;
    public float Knockback = 5f;

    public class Baker : Baker<ProjectileAuthoring>
    {
        public override void Bake(ProjectileAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            AddComponent(entity, new ProjectileData
            {
                Damage = authoring.Damage,
                Speed = authoring.Speed,
                LifeTime = authoring.LifeTime,
                Knockback = authoring.Knockback,
                Direction = new Unity.Mathematics.float3(1,0,0) // Default
            });

            AddComponent(entity, new ProjectileTag());
        }
    }
}
