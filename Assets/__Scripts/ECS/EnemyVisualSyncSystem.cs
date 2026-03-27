using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using System.Collections.Generic;

// Componente para guardar referência ao GameObject visual (managed)
public class EnemyVisualGameObject : IComponentData
{
    public GameObject Visual;
}

// Sistema que sincroniza posição ECS -> GameObject visual
// Isso permite usar o RobustIsometricController original!
public partial class EnemyVisualSyncSystem : SystemBase
{
    protected override void OnUpdate()
    {
        // Percorre todas as entidades que têm visual híbrido
        Entities
            .WithoutBurst() // Necessário para acessar GameObjects
            .ForEach((Entity entity, in LocalTransform transform, in EnemyVisualGameObject visual) =>
            {
                if (visual.Visual != null)
                {
                    // Se tiver Rigidbody, usa MovePosition para respeitar a física
                    var rb = visual.Visual.GetComponent<Rigidbody>();
                    Vector3 targetPos = new Vector3(transform.Position.x, transform.Position.y, transform.Position.z);

                    if (rb != null && !rb.isKinematic)
                    {
                        rb.MovePosition(targetPos);
                    }
                    else
                    {
                        visual.Visual.transform.position = targetPos;
                    }
                }
            }).Run();
    }
}
