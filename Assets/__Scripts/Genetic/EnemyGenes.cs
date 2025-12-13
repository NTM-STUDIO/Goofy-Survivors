using UnityEngine;
using System;
using Unity.Netcode;

[Serializable]
public struct EnemyGenes : INetworkSerializable, IEquatable<EnemyGenes>
{
    public float SpeedMultiplier;
    public float HealthMultiplier;
    public float DamageMultiplier;

    
    // Base Stats
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

        if (total <= 0.01f) return Color.white;

        // Normalize
        float r = dmg / total;
        float g = hp / total;
        float b = spd / total;

        // Inverted color scheme
        return new Color(1f - g - b * 0.5f, 1f - r - b * 0.5f, 1f - r - g); 
        
        float intensity = Mathf.Clamp01(total);
        
        // Target color based on ratios
        Color target = new Color(r, g, b);
        
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
