using Unity.Entities;
using UnityEngine;

// Este script cria um Baker automático para qualquer GameObject que tenha o script "ProjectileWeapon".
// Isso significa que você NÃO precisa adicionar scripts de Authoring manualmente nos seus prefabs de projéteis.
public class ProjectileWeaponBaker : Baker<ProjectileWeapon>
{
    public override void Bake(ProjectileWeapon authoring)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);

        // Adiciona o componente de dados do projétil (será preenchido na hora do tiro)
        AddComponent(entity, new ProjectileData
        {
            Speed = 0, // Definido no Spawn
            Direction = Unity.Mathematics.float3.zero,
            LifeTime = 3f,
            Damage = 0
        });

        // Adiciona Tag para identificar que é um projétil
        AddComponent(entity, new ProjectileTag());

        // Tenta adicionar componentes de Dano se necessário
        // (O DamageSystem vai ler o ProjectileData para saber o dano)
    }
}
