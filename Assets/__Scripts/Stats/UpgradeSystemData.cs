using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// Enum to identify each stat.
public enum StatType
{
    MaxHP,
    HPRegen,
    DamageMultiplier,
    CritChance,
    CritDamageMultiplier,
    CooldownReduction,       // NEW: Reduces cooldown between attacks (0.1 = 10% CDR)
    AttackSpeedMultiplier,   // CHANGED: Now controls projectile travel speed AND aura/melee tick/animation speed
    ProjectileCount,
    ProjectileSizeMultiplier,
    DurationMultiplier,
    KnockbackMultiplier,     // CHANGED: Now penetrates enemy knockback resistance
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
    Shadow,
    Mythic
}

// MERGED CLASS: Includes fields from your script and the new blending logic.
[System.Serializable]
public class RarityTier
{
    public Rarity rarity;
    public string name;
    public float valueMultiplier;
    public Color backgroundColor; // From your original script
    [Tooltip("The base chance weight at 0 luck.")]
    public float baseWeight;
    [Tooltip("The target weight when luck reaches the 'Max Luck Value'.")]
    public float maxLuckWeight; // New field for the blending system
}