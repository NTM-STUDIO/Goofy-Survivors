using Unity.Entities;
using Unity.Mathematics;

public struct EnemyStatsData : IComponentData
{
    public float CurrentHealth;
    public float MaxHealth;
    public float Damage;
    public float MoveSpeed;
}

public struct EnemyMovementData : IComponentData
{
    public float3 TargetPosition;
    public bool HasTarget;
}

public struct EnemyTag : IComponentData { }

public struct PlayerPositionSingleton : IComponentData
{
    public float3 Position;
}
