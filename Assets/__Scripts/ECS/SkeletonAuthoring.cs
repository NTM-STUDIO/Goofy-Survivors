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
    [Tooltip("Objeto filho que contém o SpriteRenderer")]
    public GameObject VisualsObject;
    
    [Header("Sprites Direcionais (para ECS puro)")]
    public Sprite UpRightSprite;
    public Sprite UpLeftSprite;
    public Sprite DownRightSprite;
    public Sprite DownLeftSprite;

    public class Baker : Baker<SkeletonAuthoring>
    {
        public override void Bake(SkeletonAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);

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
            
            // Adiciona dados do sprite isométrico para ECS puro
            AddComponent(entity, new IsometricSpriteData
            {
                CurrentDirection = 0, // DownRight por defeito
                PreviousDirection = 0,
                FlipX = false
            });

            // Se tiver objeto visual filho, guarda a referência
            if (authoring.VisualsObject != null)
            {
                var visualEntity = GetEntity(authoring.VisualsObject, TransformUsageFlags.Dynamic);
                AddComponent(entity, new SpriteVisualReference
                {
                    VisualEntity = visualEntity
                });
                
                // Também guarda referência antiga para compatibilidade
                AddComponent(entity, new EnemyVisualsReference
                {
                    VisualsEntity = visualEntity
                });
            }
        }
    }
}

// Tag para identificar entidades que usam visual híbrido (mantido para compatibilidade)
public struct HybridVisualTag : IComponentData { }
