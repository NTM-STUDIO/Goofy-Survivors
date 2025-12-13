using Unity.Entities;
using UnityEngine;

// Baker automático para armas orbitais (Panela, etc)
// Se o seu prefab já tem o script "OrbitingWeapon", este baker vai convertê-lo automaticamente para ECS.
public class OrbitingWeaponBaker : Baker<OrbitingWeapon>
{
    public override void Bake(OrbitingWeapon authoring)
    {
        var entity = GetEntity(TransformUsageFlags.Dynamic);

        // Adiciona ProjectileData (necessário para o DamageSystem calcular dano)
        AddComponent(entity, new ProjectileData
        {
            Damage = 0, // Será sobrescrito pelo WeaponControllerSystem
            Speed = 0,
            LifeTime = 0,
            Knockback = 0,
            Direction = Unity.Mathematics.float3.zero
        });

        // Adiciona a Tag para ser detectado como projétil
        AddComponent(entity, new ProjectileTag());
    }
}
