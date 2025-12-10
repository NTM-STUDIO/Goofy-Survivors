using UnityEngine;
using System.Collections.Generic; // Required for using Lists

// This helper class will now use your existing StatType enum.
// [System.Serializable] makes it show up in the Inspector.
[System.Serializable]
public class StatBonus
{
    [Tooltip("The stat to modify.")]
    public StatType stat;

    [Tooltip("The value to add to the stat.")]
    public float value;
}

[CreateAssetMenu(fileName = "New Player Character", menuName = "GoofySurvivors/Player Character Data")]
public class PlayerCharacterData : ScriptableObject
{
    [Header("Player Info")]
    public string characterName;
    public GameObject playerPrefab;
    public GameObject defaultWeaponPrefab;

    [Header("Base Stats (Level 1)")]
    public int maxHp = 100;
    public float hpRegen = 0f;
    public float damageMultiplier = 1.0f;
    [Range(0f, 1f)]
    public float critChance = 0.05f;
    public float critDamageMultiplier = 2.0f;
    public float attackSpeedMultiplier = 1.0f;
    public int projectileCount = 1;
    public float projectileSizeMultiplier = 1.0f;
    [Range(0f, 0.9f)]
    [Tooltip("Reduces cooldown between attacks (0 = 0%, 0.9 = 90% reduction)")]
    public float cooldownReduction = 0f;
    public float durationMultiplier = 1.0f;
    public float knockbackMultiplier = 1.0f;
    public float movementSpeed = 5.0f;
    public float luck = 0f;
    public float pickupRange = 2f;
    public float xpGainMultiplier = 1.0f;
    public int pierceCount = 0;

    [Header("Starting Bonuses")]
    [Tooltip("Bonuses applied once at the start of the game, on top of base stats.")]
    public List<StatBonus> startingBonuses = new List<StatBonus>();

    [Header("Scaling Bonuses Per Level")]
    [Tooltip("Bonuses applied every time the player levels up.")]
    public List<StatBonus> scalingBonusesPerLevel = new List<StatBonus>();
}