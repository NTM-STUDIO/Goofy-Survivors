using UnityEngine;
using System;
using Unity.Netcode;

[Serializable]
public struct EnemyGenes : INetworkSerializable, IEquatable<EnemyGenes>
{
    public float SpeedMultiplier;
    public float HealthMultiplier;
    public float DamageMultiplier;
    // Removed SizeMultiplier as requested
    
    // Default constructor for standard genes (base stats)
    public static EnemyGenes Default => new EnemyGenes
    {
        SpeedMultiplier = 1.0f,
        HealthMultiplier = 1.0f,
        DamageMultiplier = 1.0f
    };

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref SpeedMultiplier);
        serializer.SerializeValue(ref HealthMultiplier);
        serializer.SerializeValue(ref DamageMultiplier);
    }

    public bool Equals(EnemyGenes other)
    {
        return Mathf.Approximately(SpeedMultiplier, other.SpeedMultiplier) &&
               Mathf.Approximately(HealthMultiplier, other.HealthMultiplier) &&
               Mathf.Approximately(DamageMultiplier, other.DamageMultiplier);
    }

    public Color GetGeneColor()
    {
        // Calculate tint based on strongest deviation from 1.0
        float spd = Mathf.Max(0, SpeedMultiplier - 1f);
        float hp = Mathf.Max(0, HealthMultiplier - 1f);
        float dmg = Mathf.Max(0, DamageMultiplier - 1f);
        float total = spd + hp + dmg;

        if (total <= 0.01f) return Color.white; // Base stats

        // Normalize
        float r = dmg / total;   // Red = Damage
        float g = hp / total;    // Green = HP
        float b = spd / total;   // Blue = Speed

        // Mix with white to keep it visible/not too dark
        // Or simply use the RGB components directly to tint
        return new Color(1f - g - b * 0.5f, 1f - r - b * 0.5f, 1f - r - g); 
        // Wait, standard tinting logic usually Multiplies.
        // Let's return a tint color where the specialized channel is saturated.
        // If high HP -> Greenish (R low, B low)
        
        // Simpler approach:
        // Base color is White. Subtract channels of weaker stats.
        // Pure HP (Green) -> R=0, B=0, G=1.
        
        // Let's use an additive influence on a neutral base or just simple RGB mixing
        // Red = Dmg, Green = Hp, Blue = Spd.
        // Base is (1,1,1).
        // If Dmg is high, we want Reddish. (1, 0, 0)
        
        float intensity = Mathf.Clamp01(total); // How "mutated" it is
        
        // Target color based on ratios
        Color target = new Color(r, g, b);
        
        // If mutation is small, look normal. If high, look mutated.
        // Ensure it's not too dark: normalize max component to 1
        float maxC = Mathf.Max(target.r, target.g, target.b);
        if (maxC > 0)
        {
            target /= maxC;
        }
        else
        {
            target = Color.white;
        }

        return Color.Lerp(Color.white, target, Mathf.Min(1f, total)); 
    }

    public string GetDominantTrait()
    {
        if (DamageMultiplier >= HealthMultiplier && DamageMultiplier >= SpeedMultiplier) return "Damage";
        if (HealthMultiplier >= SpeedMultiplier) return "Health";
        return "Speed";
    }
}
