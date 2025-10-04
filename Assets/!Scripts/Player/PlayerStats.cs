using UnityEngine;

public class PlayerStats : MonoBehaviour
{
    [Header("Character Data")]
    public PlayerCharacterData characterData;
    [HideInInspector] public int maxHp;
    [HideInInspector] public int hpRegen;
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
    [HideInInspector] public int pickupRange;
    [HideInInspector] public float xpGainMultiplier;

    private void Awake()
    {
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

    public void IncreasePickupRange(int amount)
    {
        pickupRange += amount;
    }
}