using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    [Header("Character Data")]
    public PlayerCharacterData characterData;

    // --- Your Original Stat Fields ---
    [HideInInspector] public int maxHp;
    [HideInInspector] public float hpRegen;
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
    [HideInInspector] public float pickupRange;
    [HideInInspector] public float xpGainMultiplier;

    private void Awake()
    {
        if (characterData != null)
        {
            maxHp = characterData.maxHp; //Blouso
            hpRegen = characterData.hpRegen; //Blouso
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

    // --- Public Methods to INCREASE Stats ---
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

    // --- Public Methods to DECREASE Stats (Essential for removing buffs) ---
    public void DecreaseMaxHP(int amount) { maxHp -= amount; }
    public void DecreaseHPRegen(float amount) { hpRegen -= amount; }
    public void DecreaseDamageMultiplier(float amount) { damageMultiplier -= amount; }
    public void DecreaseCritChance(float amount) { critChance -= amount; }
    public void DecreaseCritDamageMultiplier(float amount) { critDamageMultiplier -= amount; }
    public void DecreaseAttackSpeedMultiplier(float amount) { attackSpeedMultiplier -= amount; }
    public void DecreaseProjectileCount(int amount) { projectileCount -= amount; }
    public void DecreaseProjectileSizeMultiplier(float amount) { projectileSizeMultiplier -= amount; }
    public void DecreaseProjectileSpeedMultiplier(float amount) { projectileSpeedMultiplier -= amount; }
    public void DecreaseDurationMultiplier(float amount) { durationMultiplier -= amount; }
    public void DecreaseKnockbackMultiplier(float amount) { knockbackMultiplier -= amount; }
    public void DecreaseMovementSpeed(float amount) { movementSpeed -= amount; }
    public void DecreaseLuck(float amount) { luck -= amount; }
    public void DecreasePickupRange(float amount) { pickupRange -= amount; }
    public void DecreaseXPGainMultiplier(float amount) { xpGainMultiplier -= amount; }


    public void PrintStats()
    {
        Debug.Log("Stats Initialized from Character Data");
        Debug.Log($"MaxHP: {maxHp}, HPRegen: {hpRegen}, DamageMultiplier: {damageMultiplier}");
        Debug.Log($"CritChance: {critChance}, CritDamageMultiplier: {critDamageMultiplier}, AttackSpeedMultiplier: {attackSpeedMultiplier}");
        Debug.Log($"ProjectileCount: {projectileCount}, ProjectileSizeMultiplier: {projectileSizeMultiplier}, ProjectileSpeedMultiplier: {projectileSpeedMultiplier}");
        Debug.Log($"DurationMultiplier: {durationMultiplier}, KnockbackMultiplier: {knockbackMultiplier}, MovementSpeed: {movementSpeed}");
        Debug.Log($"Luck: {luck}, PickupRange: {pickupRange}, XPGainMultiplier: {xpGainMultiplier}");
    }

}