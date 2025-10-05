using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class UpgradeManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerStats playerStats;
    [SerializeField] private GameObject upgradePanel; // The parent panel for choices
    [SerializeField] private UpgradeChoiceUI upgradeChoicePrefab;
    [SerializeField] private Transform choicesContainer;

    [Header("Upgrade Pool")]
    [Tooltip("All possible stat upgrades that can be offered.")]
    [SerializeField] private List<StatUpgradeData> availableUpgrades;

    [Header("Rarity Settings")]
    [Tooltip("Define rarities from most common to most rare.")]
    [SerializeField] private List<RarityTier> rarityTiers;

    // A temporary class to hold the fully generated upgrade instance
    public class GeneratedUpgrade
    {
        public StatUpgradeData BaseData;
        public RarityTier Rarity;
        public float Value;
    }

    // For testing: call this from a level up event
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            TriggerLevelUp();
        }
    }

    public void TriggerLevelUp()
    {
        // Clear any previous choices
        foreach (Transform child in choicesContainer)
        {
            Destroy(child.gameObject);
        }

        int choicesCount = GetNumberOfChoices(playerStats.luck);
        List<GeneratedUpgrade> choices = GenerateUpgradeChoices(choicesCount);
        DisplayUpgradeChoices(choices);
        
        upgradePanel.SetActive(true);
        Time.timeScale = 0f; // Pause the game
    }

    private int GetNumberOfChoices(float currentLuck)
    {
        // Luck provides a chance for more choices.
        // Every 10 luck gives a 10% chance for a 4th option.
        // Every 20 luck gives a 10% chance for a 5th option.
        if (Random.Range(0f, 100f) < currentLuck / 2f) return 5;
        if (Random.Range(0f, 100f) < currentLuck) return 4;
        return 3;
    }

    private List<GeneratedUpgrade> GenerateUpgradeChoices(int count)
    {
        var generatedChoices = new List<GeneratedUpgrade>();
        var availableUpgradesCopy = new List<StatUpgradeData>(availableUpgrades);

        for (int i = 0; i < count; i++)
        {
            if (availableUpgradesCopy.Count == 0) break;

            // Select a random upgrade type from the remaining pool
            int randomIndex = Random.Range(0, availableUpgradesCopy.Count);
            StatUpgradeData chosenData = availableUpgradesCopy[randomIndex];
            availableUpgradesCopy.RemoveAt(randomIndex); // Ensure no duplicate stat types

            // Generate the full upgrade instance
            var generatedUpgrade = new GeneratedUpgrade();
            generatedUpgrade.BaseData = chosenData;
            generatedUpgrade.Rarity = DetermineRarity(playerStats.luck);
            
            // Calculate final value
            float baseValue = Random.Range(chosenData.baseValueMin, chosenData.baseValueMax);
            generatedUpgrade.Value = baseValue * generatedUpgrade.Rarity.valueMultiplier;

            generatedChoices.Add(generatedUpgrade);
        }
        return generatedChoices;
    }

    private RarityTier DetermineRarity(float currentLuck)
    {
        float totalWeight = 0;
        var weightedTiers = new List<(RarityTier, float)>();

        // Calculate modified weights based on luck
        foreach (var tier in rarityTiers)
        {
            // Luck increases the weight of all non-common rarities
            float luckModifier = (tier.rarity == Rarity.Common) ? 1f : 1f + (currentLuck / 100f);
            float modifiedWeight = tier.baseWeight * luckModifier;
            weightedTiers.Add((tier, modifiedWeight));
            totalWeight += modifiedWeight;
        }

        // Perform the weighted random roll
        float randomRoll = Random.Range(0, totalWeight);
        float currentWeight = 0;

        foreach (var (tier, weight) in weightedTiers)
        {
            currentWeight += weight;
            if (randomRoll <= currentWeight)
            {
                return tier;
            }
        }
        
        // Fallback (shouldn't happen)
        return rarityTiers.First();
    }

    private void DisplayUpgradeChoices(List<GeneratedUpgrade> choices)
    {
        foreach (var choice in choices)
        {
            UpgradeChoiceUI uiInstance = Instantiate(upgradeChoicePrefab, choicesContainer);
            uiInstance.Setup(choice, this);
        }
    }

    public void ApplyUpgrade(GeneratedUpgrade upgrade)
    {
        float value = upgrade.Value;
        
        // Apply the stat modification using a switch statement
        switch (upgrade.BaseData.statToUpgrade)
        {
            case StatType.MaxHP:
                playerStats.IncreaseMaxHP(Mathf.RoundToInt(value));
                break;
            case StatType.HPRegen:
                playerStats.IncreaseHPRegen(value);
                break;
            case StatType.DamageMultiplier:
                playerStats.IncreaseDamageMultiplier(value / 100f); // Convert percentage
                break;
            // ... Add cases for ALL other stats
            case StatType.CritChance:
                playerStats.IncreaseCritChance(value / 100f);
                break;
            case StatType.CritDamageMultiplier:
                playerStats.IncreaseCritDamageMultiplier(value / 100f);
                break;
            case StatType.AttackSpeedMultiplier:
                playerStats.IncreaseAttackSpeedMultiplier(value / 100f);
                break;
            case StatType.ProjectileCount:
                playerStats.IncreaseProjectileCount(Mathf.RoundToInt(value));
                break;
            case StatType.ProjectileSizeMultiplier:
                playerStats.IncreaseProjectileSizeMultiplier(value / 100f);
                break;
            case StatType.ProjectileSpeedMultiplier:
                playerStats.IncreaseProjectileSpeedMultiplier(value / 100f);
                break;
            case StatType.DurationMultiplier:
                playerStats.IncreaseDurationMultiplier(value / 100f);
                break;
            case StatType.KnockbackMultiplier:
                playerStats.IncreaseKnockbackMultiplier(value / 100f);
                break;
            case StatType.MovementSpeed:
                playerStats.IncreaseMovementSpeed(value /100f);
                break;
            case StatType.Luck:
                playerStats.IncreaseLuck(value);
                break;
            case StatType.PickupRange:
                playerStats.IncreasePickupRange(value / 100f);
                break;
            case StatType.XPGainMultiplier:
                playerStats.IncreaseXPGainMultiplier(value / 100f);
                break;
        }

        // Clean up UI and unpause
        foreach (Transform child in choicesContainer)
        {
            Destroy(child.gameObject);
        }
        upgradePanel.SetActive(false);
        Time.timeScale = 1f;
    }
}