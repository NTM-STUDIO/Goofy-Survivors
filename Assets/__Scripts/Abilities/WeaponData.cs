using UnityEngine;

// An enum to define all possible targeting behaviors for projectiles.
public enum TargetingStyle
{
    Closest,        // Targets the closest enemy. (Chinelo behavior)
    Random,         // Shoots in a random direction. (Pedra behavior)
    Mixed,          // Targets closest, then shoots the rest randomly.
    Strongest,      // Targets the enemy with the most current health.
    MostGrouped     // Targets the enemy with the most other enemies nearby.
}

public enum WeaponArchetype
{
    Projectile,
    Whip,
    Aura,
    Orbit,
    Laser,
    Shield,
    Clone
}

[CreateAssetMenu(fileName = "New WeaponData", menuName = "Goofy Survivors/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [Header("General")]
    public string weaponID;
    public string weaponName;
    public string description;
    public Sprite icon;
    public GameObject weaponPrefab;

    [Header("Core Behavior")]
    public WeaponArchetype archetype;

    [Header("Projectile Targeting")]
    // This dropdown will only be relevant if the Archetype is Projectile.
    public TargetingStyle targetingStyle;
    // This field is only used for the 'MostGrouped' targeting style.
    public float groupingRange = 5f;

    [Header("Base Stats")]
    public float damage = 10f;
    public float cooldown = 1f;
    public float area = 1f;
    public int amount = 1;
    public float speed = 1f;
    public float duration = 1f;
    public float criticalChance = 0f;
    public float criticalMultiplier = 1.5f;
    public bool pierce = false;
    public int pierceCount = 1;
    public int knockback = 0;
}