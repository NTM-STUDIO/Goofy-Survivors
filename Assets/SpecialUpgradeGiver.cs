/*using UnityEngine;
using System.Linq;

public class SpecialUpgradeGiver : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("CRITICAL: Drag your Special Upgrade UI Panel here.")]
    [SerializeField] private SpecialUpgradeUI specialUpgradePanel;

    [Header("Upgrade Generation")]
    [Tooltip("CRITICAL: Drag your UpgradeManager object here.")]
    [SerializeField] private UpgradeManager upgradeManager;
    [Tooltip("CRITICAL: Drag your Player object here.")]
    [SerializeField] private PlayerStats playerStats;

    [Header("Rarity Settings")]
    [Tooltip("The chance (out of 100) to get a Mythic upgrade. The rest is the chance for Shadow.")]
    [Range(0, 100)]
    [SerializeField] private float mythicalChance = 50f;

    private UpgradeManager.GeneratedUpgrade generatedUpgrade;

    // --- THIS IS THE PART WE FIXED ---
    // We removed the unreliable FindObjectOfType calls.
    // Now it just checks if YOU have assigned the objects in the Inspector.
    void Awake()
    {
        if (upgradeManager == null || specialUpgradePanel == null || playerStats == null)
        {
            Debug.LogError("FATAL ERROR on SpecialUpgradeGiver: One or more critical references are NOT ASSIGNED in the Inspector! Disabling this object.", this);
            gameObject.SetActive(false); // Disable to prevent further errors
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            GenerateAndShowSpecialUpgrade();
            GetComponent<Collider>().enabled = false;
        }
    }

    private void GenerateAndShowSpecialUpgrade()
    {
        Rarity chosenRarityEnum = Random.Range(0f, 100f) < mythicalChance ? Rarity.Mythic : Rarity.Shadow;
        
        RarityTier chosenRarityTier = upgradeManager.GetRarityTiers().FirstOrDefault(tier => tier.rarity == chosenRarityEnum);
        if (chosenRarityTier == null)
        {
            Debug.LogError($"Could not find RarityTier data for {chosenRarityEnum}.", this);
            Destroy(gameObject);
            return;
        }

        var availableUpgrades = upgradeManager.GetAvailableUpgrades();
        if (availableUpgrades.Count == 0)
        {
            Destroy(gameObject);
            return;
        }
        StatUpgradeData chosenStatData = availableUpgrades[Random.Range(0, availableUpgrades.Count)];

        generatedUpgrade = new UpgradeManager.GeneratedUpgrade
        {
            BaseData = chosenStatData,
            Rarity = chosenRarityTier,
            Value = Random.Range(chosenStatData.baseValueMin, chosenStatData.baseValueMax) * chosenRarityTier.valueMultiplier
        };

        specialUpgradePanel.Show(generatedUpgrade, this);
        Time.timeScale = 0f;
    }
    
    public void ApplyUpgradeAndDestroy()
    {
        if (generatedUpgrade == null) return;
        
        ApplyStatToPlayer(generatedUpgrade.BaseData.statToUpgrade, generatedUpgrade.Value);
        
        Debug.Log($"Applied Special Upgrade: {generatedUpgrade.BaseData.statToUpgrade} +{generatedUpgrade.Value} ({generatedUpgrade.Rarity.rarity})");

        Time.timeScale = 1f;
        Destroy(gameObject);
    }
    
    // NOTE: This part must contain all the stats from your main UpgradeManager's ApplyUpgrade method
    private void ApplyStatToPlayer(StatType stat, float value)
    {
        switch (stat)
        {
            case StatType.MaxHP: playerStats.IncreaseMaxHP(Mathf.RoundToInt(value)); break;
            case StatType.HPRegen: playerStats.IncreaseHPRegen(value); break;
            case StatType.DamageMultiplier: playerStats.IncreaseDamageMultiplier(value / 100f); break;
            case StatType.CritChance: playerStats.IncreaseCritChance(value / 100f); break;
            case StatType.CritDamageMultiplier: playerStats.IncreaseCritDamageMultiplier(value / 100f); break;
            case StatType.AttackSpeedMultiplier: playerStats.IncreaseAttackSpeedMultiplier(value / 100f); break;
            case StatType.ProjectileCount: playerStats.IncreaseProjectileCount(Mathf.RoundToInt(value)); break;
            case StatType.ProjectileSizeMultiplier: playerStats.IncreaseProjectileSizeMultiplier(value / 100f); break;
            case StatType.ProjectileSpeedMultiplier: playerStats.IncreaseProjectileSpeedMultiplier(value / 100f); break;
            case StatType.DurationMultiplier: playerStats.IncreaseDurationMultiplier(value / 100f); break;
            case StatType.KnockbackMultiplier: playerStats.IncreaseKnockbackMultiplier(value / 100f); break;
            case StatType.MovementSpeed: playerStats.IncreaseMovementSpeed(value / 100f * playerStats.movementSpeed); break;
            case StatType.Luck: playerStats.IncreaseLuck(value); break;
            case StatType.PickupRange: playerStats.IncreasePickupRange(value * playerStats.pickupRange - playerStats.pickupRange); break;
            case StatType.XPGainMultiplier: playerStats.IncreaseXPGainMultiplier(value / 100f); break;
        }
    }
}*/