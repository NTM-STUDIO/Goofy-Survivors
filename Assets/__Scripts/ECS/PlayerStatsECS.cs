using Unity.Entities;

public struct PlayerStatsECS : IComponentData
{
    public float DamageMultiplier;
    public float CooldownReduction;
    public float AttackSpeedMultiplier; // Afeta velocidade do projétil
    public float DurationMultiplier;
    public float KnockbackMultiplier;
    public float ProjectileSizeMultiplier;
    public int ExtraProjectiles;
}
