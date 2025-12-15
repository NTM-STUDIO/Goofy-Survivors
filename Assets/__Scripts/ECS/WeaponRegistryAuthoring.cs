using Unity.Entities;
using Unity.Collections;
using UnityEngine;

// Componente que vai guardar a lista de todas as armas convertidas em Entidades
public struct WeaponRegistryData : IComponentData
{
    public Entity WeaponPrefabBufferEntity;
}

// Buffer para guardar os prefabs das armas (acessível por índice/ID)
public struct WeaponPrefabElement : IBufferElementData
{
    public Entity PrefabEntity;
    public int WeaponID;
}

public class WeaponRegistryAuthoring : MonoBehaviour
{
    public WeaponRegistry Registry; // Arraste seu WeaponRegistry aqui

    public class Baker : Baker<WeaponRegistryAuthoring>
    {
        public override void Bake(WeaponRegistryAuthoring authoring)
        {
            if (authoring.Registry == null) return;

            var entity = GetEntity(TransformUsageFlags.None);
            
            // Cria um buffer dinâmico para armazenar os prefabs
            var buffer = AddBuffer<WeaponPrefabElement>(entity);

            for (int i = 0; i < authoring.Registry.allWeapons.Count; i++)
            {
                var weaponData = authoring.Registry.allWeapons[i];
                if (weaponData == null || weaponData.weaponPrefab == null) continue;

                // Converte o prefab da arma (GameObject) para Entity
                var weaponEntityPrefab = GetEntity(weaponData.weaponPrefab, TransformUsageFlags.Dynamic);

                buffer.Add(new WeaponPrefabElement
                {
                    PrefabEntity = weaponEntityPrefab,
                    WeaponID = i
                });
            }

            // Adiciona o componente singleton para acessarmos isso de qualquer lugar
            AddComponent(entity, new WeaponRegistryData
            {
                WeaponPrefabBufferEntity = entity
            });
        }
    }
}
