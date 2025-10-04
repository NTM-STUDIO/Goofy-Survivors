using UnityEngine;

// Enum to identify each stat. This avoids using error-prone strings.
public enum StatType
{
    MaxHP,
    HPRegen,
    DamageMultiplier,
    CritChance,
    CritDamageMultiplier,
    AttackSpeedMultiplier,
    ProjectileCount,
    ProjectileSizeMultiplier,
    ProjectileSpeedMultiplier,
    DurationMultiplier,
    KnockbackMultiplier,
    MovementSpeed,
    Luck,
    PickupRange,
    XPGainMultiplier
}

// Enum for the different rarity tiers.
public enum Rarity
{
    Common,
    Rare,
    Epic,
    Legendary,
    Ultra,
    OneIn100
}

// A simple class to hold all data related to a rarity tier.
// [System.Serializable] allows us to edit this in the Inspector.
[System.Serializable]
public class RarityTier
{
    public Rarity rarity;
    public string name;
    public float valueMultiplier;
    public Color backgroundColor;
    [Tooltip("The base chance weight. Higher is more common.")]
    public float baseWeight;
}