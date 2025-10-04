using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    [Header("Character Data")]
    public PlayerCharacterData characterData;
    
    // Your existing stat fields
    [HideInInspector] public int maxHp;
    [HideInInspector] public float hpRegen; // Changed to float for precision
    [HideInInspector] public float damageMultiplier;
    [HideInInspector] public float critChance;
    [HideInInspector] public float critDamageMultiplier;
    [HideInInspector] public float attackSpeedMultiplier;
    [HideInInspector] public int projectileCount;
    [HideInInspector] public float projectileSizeMultiplier;
    [HideInInspector] public float projectileSpeedMultiplier;
    [HideInInspector] public float durationMultiplier;
    [HideInInspector] public float knockbackMultiplier;
    [HideInInspector] public float movementSpeed;
    [HideInInspector] public float luck;
    [HideInInspector] public float pickupRange; // Changed to float for precision
    [HideInInspector] public float xpGainMultiplier;

    private void Awake()
    {
        // Your Awake method is fine as is
        if (characterData != null)
        {
            maxHp = characterData.maxHp;
            hpRegen = characterData.hpRegen;
            damageMultiplier = characterData.damageMultiplier;
            critChance = characterData.critChance;
            critDamageMultiplier = characterData.critDamageMultiplier;
            attackSpeedMultiplier = characterData.attackSpeedMultiplier;
            projectileCount = characterData.projectileCount;
            projectileSizeMultiplier = characterData.projectileSizeMultiplier;
            projectileSpeedMultiplier = characterData.projectileSpeedMultiplier;
            durationMultiplier = characterData.durationMultiplier;
            knockbackMultiplier = characterData.knockbackMultiplier;
            movementSpeed = characterData.movementSpeed;
            luck = characterData.luck;
            pickupRange = characterData.pickupRange;
            xpGainMultiplier = characterData.xpGainMultiplier;
        }
    }

    // --- Public Methods to Modify Stats ---
    
    public void IncreaseMaxHP(int amount) { maxHp += amount; }
    public void IncreaseHPRegen(float amount) { hpRegen += amount; }
    public void IncreaseDamageMultiplier(float amount) { damageMultiplier += amount; }
    public void IncreaseCritChance(float amount) { critChance += amount; }
    public void IncreaseCritDamageMultiplier(float amount) { critDamageMultiplier += amount; }
    public void IncreaseAttackSpeedMultiplier(float amount) { attackSpeedMultiplier += amount; }
    public void IncreaseProjectileCount(int amount) { projectileCount += amount; }
    public void IncreaseProjectileSizeMultiplier(float amount) { projectileSizeMultiplier += amount; }
    public void IncreaseProjectileSpeedMultiplier(float amount) { projectileSpeedMultiplier += amount; }
    public void IncreaseDurationMultiplier(float amount) { durationMultiplier += amount; }
    public void IncreaseKnockbackMultiplier(float amount) { knockbackMultiplier += amount; }
    public void IncreaseMovementSpeed(float amount) { movementSpeed += amount; }
    public void IncreaseLuck(float amount) { luck += amount; }
    public void IncreasePickupRange(float amount) { pickupRange += amount; }
    public void IncreaseXPGainMultiplier(float amount) { xpGainMultiplier += amount; }
}