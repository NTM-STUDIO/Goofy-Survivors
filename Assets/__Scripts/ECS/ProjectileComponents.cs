using Unity.Entities;
using Unity.Mathematics;

public struct ProjectileData : IComponentData
{
    public float Speed;
    public float3 Direction;
    public float LifeTime;
    public float Damage;
    public float Knockback;
    public int PierceCount; // -1 = Infinito
}

public struct ProjectileTag : IComponentData { }

public struct OrbitMovement : IComponentData
{
    public float Angle;
    public float Radius;
    public float Speed;
    public Entity CenterEntity; // Entidade ao redor da qual orbitar (Player)
}

