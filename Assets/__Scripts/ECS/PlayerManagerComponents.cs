using Unity.Entities;

// Dados de Stats do Player (Vida, XP, Nível)
public struct PlayerStatsData : IComponentData
{
    public float CurrentHealth;
    public float MaxHealth;
    public float CurrentXP;
    public float MaxXP;
    public int Level;
    
    // Stats de Combate
    public float PickupRange;
    public float DamageMultiplier;
    public float CooldownReduction;
    public float MoveSpeed;
}

// Componente para Orbes de XP
public struct ExperienceOrbData : IComponentData
{
    public float XPValue;
}

// Tag para identificar Orbes
public struct ExperienceOrbTag : IComponentData { }

// Tag para identificar o Player
public struct PlayerTag : IComponentData { }
