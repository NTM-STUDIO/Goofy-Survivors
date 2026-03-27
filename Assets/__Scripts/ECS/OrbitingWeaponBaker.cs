/* using Unity.Entities;
using UnityEngine;
using Unity.Physics;

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
            Direction = Unity.Mathematics.float3.zero,
            PierceCount = -1 // Default para Orbiting Weapon
        });

        // Adiciona a Tag para ser detectado como projétil
        AddComponent(entity, new ProjectileTag());

        // --- FÍSICA ---
        // Se o prefab tiver Collider mas não tiver Rigidbody, o Unity Physics pode convertê-lo como Static.
        // Para garantir que ele gera eventos de Trigger, precisamos de PhysicsCollider e PhysicsTrigger.
        
        // O Baker padrão do Unity já converte Colliders, mas vamos garantir que é um Trigger.
        // Se o Collider no prefab não for Trigger, o sistema de dano pode falhar.
        // Mas como não podemos mudar o Collider aqui facilmente sem PhysicsShapeAuthoring,
        // assumimos que o usuário configurou "Is Trigger" no prefab.
        
        // Se o prefab NÃO tiver Rigidbody, adicionamos PhysicsVelocity para ele ser considerado Dinâmico (Kinematic)
        // Isso permite que ele colida com outros Kinematics ou Statics se necessário, e mova-se corretamente.
        if (GetComponent<Rigidbody>() == null)
        {
            // VERIFICAÇÃO DE SEGURANÇA:
            // Se o Collider não for Trigger, vai bloquear o Player!
            var collider = GetComponent<Collider>();
            if (collider != null && !collider.isTrigger)
            {
                Debug.LogError($"[OrbitingWeaponBaker] O Collider no prefab '{authoring.name}' NÃO é Trigger! Isso vai bloquear o movimento do Player. Por favor marque 'Is Trigger' no Collider.", authoring);
            }

            AddComponent(entity, new PhysicsVelocity());
            AddComponent(entity, new PhysicsMass { InverseMass = 0f }); // Kinematic (massa infinita)
        }
    }
}
 */