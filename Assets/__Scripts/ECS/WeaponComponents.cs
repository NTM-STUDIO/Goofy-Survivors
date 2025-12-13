using Unity.Entities;

public struct WeaponDamageData : IComponentData
{
    public float DamageAmount;
    public float KnockbackForce;
}

public struct ProjectileMovementData : IComponentData
{
    public float Speed;
    public float LifeTime; // Tempo até destruir
}

public struct WeaponTag : IComponentData { }
