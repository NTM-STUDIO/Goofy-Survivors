using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

// Este sistema sincroniza os dados ECS com o SpriteRenderer do GameObject
// É necessário porque SpriteRenderer é um componente managed (MonoBehaviour)
// Roda sem Burst porque precisa de aceder a GameObjects
[UpdateInGroup(typeof(PresentationSystemGroup))]
public partial class SpriteRendererSyncSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Sincroniza sprites direcionais
        Entities
            .WithoutBurst()
            .ForEach((Entity entity, in IsometricSpriteData spriteData, in SpriteVisualReference visualRef) =>
            {
                // Só atualiza se a direção mudou
                if (spriteData.CurrentDirection == spriteData.PreviousDirection) return;
                
                // Tenta obter o GameObject do visual
                if (!EntityManager.Exists(visualRef.VisualEntity)) return;
                
                // Nota: Em ECS puro com Entities Graphics, usaríamos MaterialMeshInfo
                // Como estamos a usar SpriteRenderer (MonoBehaviour), precisamos de um companion GameObject
                // O Unity mantém o GameObject "companion" para entidades baked de prefabs com renderers
                
            }).Run();

        // Sincroniza posição do visual com a entidade pai (se necessário)
        Entities
            .WithoutBurst()
            .ForEach((in LocalTransform transform, in EnemyVisualGameObject visual) =>
            {
                if (visual.Visual != null)
                {
                    visual.Visual.transform.position = new Vector3(
                        transform.Position.x,
                        transform.Position.y,
                        transform.Position.z
                    );
                }
            }).Run();
    }
}
