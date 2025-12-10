using Unity.Entities;
using UnityEngine;
using Unity.Mathematics;

public class SkeletonAuthoring : MonoBehaviour
{
    [Header("Stats")]
    public float Health = 100;
    public float Damage = 10;
    public float Speed = 3;

    [Header("Visuals")]
    public GameObject VisualsObject; // Arraste o filho 'Visuals' aqui

    public class Baker : Baker<SkeletonAuthoring>
    {
        public override void Bake(SkeletonAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

            if (authoring.VisualsObject != null)
            {
                AddComponent(entity, new EnemyVisualsReference
                {
                    VisualsEntity = GetEntity(authoring.VisualsObject, TransformUsageFlags.Dynamic)
                });
            }

            AddComponent(entity, new EnemyStatsData
            {
                CurrentHealth = authoring.Health,
                MaxHealth = authoring.Health,
                Damage = authoring.Damage,
                MoveSpeed = authoring.Speed
            });

            AddComponent(entity, new EnemyMovementData
            {
                TargetPosition = float3.zero,
                HasTarget = false
            });

            AddBuffer<DamageBufferElement>(entity);

            AddComponent(entity, new EnemyTag());
        }
    }
}
