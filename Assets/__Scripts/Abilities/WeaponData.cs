using UnityEngine;

public enum WeaponArchetype
{
    Projectile,
    Whip,
    Aura,
    Orbit,
    Laser,
    Shield,
    Custom
}

[CreateAssetMenu(fileName = "New WeaponData", menuName = "Goofy Survivors/Weapon Data")]
public class WeaponData : ScriptableObject
{
    [Header("General")]
    public string weaponName;
    public string description;
    public Sprite icon;
    public GameObject weaponPrefab; // The visual representation of the weapon

    [Header("Core Behavior")]
    public WeaponArchetype archetype;

    [Header("Base Stats")]
    public float damage = 10f;
    public float cooldown = 1f;
    public float area = 1f; // Can represent size, radius, etc.
    public int amount = 1; // Number of projectiles, whips, etc.
    public float speed = 1f; // Projectile speed or rotation speed
    public float duration = 1f; // How long the weapon is active

    public float criticalChance = 0f; // Chance to deal critical damage
    public float criticalMultiplier = 1.5f; // Multiplier for critical hits

    public bool pierce = false; // Whether the weapon pierces enemies
    public int pierceCount = 1; // Number of enemies it can pierce through

    public int knockback = 0; // Knockback force applied to enemies

    public int maxLevel = 8; // Maximum upgrade level

    // Add more stats as needed: knockback, crit chance, etc.
}