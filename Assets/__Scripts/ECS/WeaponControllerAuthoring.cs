using Unity.Entities;
using UnityEngine;

public class WeaponControllerAuthoring : MonoBehaviour
{
    public WeaponData WeaponData; // Arraste o ScriptableObject da arma aqui

    public class Baker : Baker<WeaponControllerAuthoring>
    {
        public override void Bake(WeaponControllerAuthoring authoring)
        {
            if (authoring.WeaponData == null) return;

            var entity = GetEntity(TransformUsageFlags.Dynamic);

            // Converte o Prefab da arma (GameObject) para Entity
            // Nota: O Prefab da arma DEVE ter um Authoring component (ex: ProjectileAuthoring) para ser convertido corretamente.
            Entity weaponPrefabEntity = Entity.Null;
            if (authoring.WeaponData.weaponPrefab != null)
            {
                weaponPrefabEntity = GetEntity(authoring.WeaponData.weaponPrefab, TransformUsageFlags.Dynamic);
            }

            AddComponent(entity, new WeaponControllerData
            {
                WeaponPrefab = weaponPrefabEntity,
                BaseCooldown = authoring.WeaponData.cooldown,
                BaseDamage = authoring.WeaponData.damage,
                BaseSpeed = authoring.WeaponData.speed,
                BaseDuration = authoring.WeaponData.duration,
                BaseKnockback = authoring.WeaponData.knockback,
                ProjectileAmount = authoring.WeaponData.amount,
                BaseArea = authoring.WeaponData.area,
                CurrentCooldown = 0
            });
        }
    }
}
