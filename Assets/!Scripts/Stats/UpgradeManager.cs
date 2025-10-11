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

    public float luckScalingFactor = 0.1f; // How much luck influences upgrade values

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

    // MODIFIED: Queue to handle multiple level-ups
    private Queue<int> levelUpQueue = new Queue<int>();
    private bool isUpgradeInProgress = false;

    void Awake()
    {
        // Find PlayerStats by PlayerTag
        if (playerStats == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                playerStats = playerObject.GetComponent<PlayerStats>();
            }
            else
            {
                Debug.LogError("UpgradeManager Error: Player with tag 'Player' not found! Make sure your player is tagged correctly.");
            }
        }
    }

    // MODIFIED: Public method for PlayerExperience to call
    public void AddLevelUpToQueue()
    {
        levelUpQueue.Enqueue(1); // Enqueue a "token" for a level up
        // If an upgrade isn't already being shown, process the next one
        if (!isUpgradeInProgress)
        {
            ProcessNextUpgrade();
        }
    }

    // MODIFIED: New method to process the queue
    private void ProcessNextUpgrade()
    {
        // If there are level-ups waiting in the queue
        if (levelUpQueue.Count > 0)
        {
            isUpgradeInProgress = true;
            levelUpQueue.Dequeue(); // Consume one level-up token
            ShowUpgradeChoices(); // Show the upgrade panel
        }
        else
        {
            // If the queue is empty, we are done
            isUpgradeInProgress = false;
        }
    }

    // MODIFIED: Renamed TriggerLevelUp to ShowUpgradeChoices and made it private
    private void ShowUpgradeChoices()
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
        // ... (rest of the method is unchanged)
        if (Random.Range(0f, 100f) < currentLuck / 2f) return 5;
        if (Random.Range(0f, 100f) < currentLuck) return 4;
        return 3;
    }

    private List<GeneratedUpgrade> GenerateUpgradeChoices(int count)
    {
        // ... (method is unchanged)
        var generatedChoices = new List<GeneratedUpgrade>();
        var availableUpgradesCopy = new List<StatUpgradeData>(availableUpgrades);

        for (int i = 0; i < count; i++)
        {
            if (availableUpgradesCopy.Count == 0) break;

            int randomIndex = Random.Range(0, availableUpgradesCopy.Count);
            StatUpgradeData chosenData = availableUpgradesCopy[randomIndex];
            availableUpgradesCopy.RemoveAt(randomIndex);

            var generatedUpgrade = new GeneratedUpgrade();
            generatedUpgrade.BaseData = chosenData;
            generatedUpgrade.Rarity = DetermineRarity(playerStats.luck);
            
            float baseValue = Random.Range(chosenData.baseValueMin, chosenData.baseValueMax);
            generatedUpgrade.Value = baseValue * generatedUpgrade.Rarity.valueMultiplier;

            generatedChoices.Add(generatedUpgrade);
        }
        return generatedChoices;
    }

    private RarityTier DetermineRarity(float currentLuck)
    {
        // ... (method is unchanged)
        float totalWeight = 0;
        var weightedTiers = new List<(RarityTier, float)>();

        foreach (var tier in rarityTiers)
        {
            float luckModifier = (tier.rarity == Rarity.Common) ? 1f : 1f + (currentLuck / 100f);
            float modifiedWeight = tier.baseWeight * luckModifier;
            weightedTiers.Add((tier, modifiedWeight));
            totalWeight += modifiedWeight;
        }

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
        
        return rarityTiers.First();
    }

    private void DisplayUpgradeChoices(List<GeneratedUpgrade> choices)
    {
        // ... (method is unchanged)
        foreach (var choice in choices)
        {
            UpgradeChoiceUI uiInstance = Instantiate(upgradeChoicePrefab, choicesContainer);
            uiInstance.Setup(choice, this);
        }
    }

    public void ApplyUpgrade(GeneratedUpgrade upgrade)
    {
        float value = upgrade.Value;

        // ... (switch statement is unchanged)
        switch (upgrade.BaseData.statToUpgrade)
        {
            case StatType.MaxHP:
                playerStats.IncreaseMaxHP(Mathf.RoundToInt(value));
                break;
            case StatType.HPRegen:
                playerStats.IncreaseHPRegen(value);
                break;
            case StatType.DamageMultiplier:
                playerStats.IncreaseDamageMultiplier(value / 100f);
                break;
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
                playerStats.IncreaseMovementSpeed(value / 100f * playerStats.movementSpeed);
                break;
            case StatType.Luck:
                playerStats.IncreaseLuck(value);
                break;
            case StatType.PickupRange:
                playerStats.IncreasePickupRange(value * playerStats.pickupRange - playerStats.pickupRange);
                break;
            case StatType.XPGainMultiplier:
                playerStats.IncreaseXPGainMultiplier(value / 100f);
                break;
        }

        Debug.Log($"Applied Upgrade: {upgrade.BaseData.statToUpgrade} +{value} ({upgrade.Rarity.rarity})");
        playerStats.PrintStats();
        
        // MODIFIED: Logic for handling the panel and time scale has changed
        
        // Don't unpause or hide the panel yet. First, check if more upgrades are waiting.
        if (levelUpQueue.Count > 0)
        {
            // If there's another level-up waiting, immediately show the next set of choices.
            ProcessNextUpgrade();
        }
        else
        {
            // Only if the queue is empty do we unpause and hide the panel.
            isUpgradeInProgress = false; // Mark that we are done upgrading for now
            foreach (Transform child in choicesContainer)
            {
                Destroy(child.gameObject);
            }
            upgradePanel.SetActive(false);
            Time.timeScale = 1f;
        }
    }
}