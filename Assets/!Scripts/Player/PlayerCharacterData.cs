using UnityEngine;

[CreateAssetMenu(fileName = "New Player Character", menuName = "GoofySurvivors/Player Character Data")]
public class PlayerCharacterData : ScriptableObject
{
    [Header("Player Info")]
    public string characterName;
    public GameObject playerPrefab; // The player's visual representation
    public GameObject defaultWeaponPrefab; // The starting weapon for this character

    [Header("Base Stats")]
    public int maxHp = 175;
    public int hpRegen = 10;
    public float damageMultiplier = 1.2f;

    [Range(0f, 1f)]
    public float critChance = 0.01f;
    public float critDamageMultiplier = 2.0f;
    public float attackSpeedMultiplier = 1.0f;
    public int projectileCount = 0;
    public float projectileSizeMultiplier = 1.0f;
    public float projectileSpeedMultiplier = 5.0f;
    public float durationMultiplier = 1.1f;
    public float knockbackMultiplier = 1.0f;
    public float movementSpeed = 1.0f;
    public float luck = 0f;
    public int pickupRange = 5;
    public float xpGainMultiplier = 1.1f;
}