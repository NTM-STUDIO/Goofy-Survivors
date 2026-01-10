using Unity.Entities;
using Unity.Mathematics;

// Dados da Arma (Configuração + Estado)
public struct WeaponControllerData : IComponentData
{
    // Configuração (Vem do WeaponData)
    public Entity WeaponPrefab; // O prefab da bala/projétil
    public float BaseCooldown;
    public float BaseDamage;
    public float BaseSpeed;
    public float BaseDuration;
    public float BaseKnockback;
    public int ProjectileAmount;
    public float BaseArea; // Tamanho
    public int Archetype; // 0=Projectile, 1=Whip, 2=Aura, 3=Orbit, etc.

    // Estado
    public float CurrentCooldown;
}

// Tag para identificar armas orbitais (se formos fazer)
public struct OrbitWeaponTag : IComponentData { }
