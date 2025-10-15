using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class UpgradeManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerStats playerStats;
    [SerializeField] private GameObject upgradePanel;
    [SerializeField] private UpgradeChoiceUI upgradeChoicePrefab;
    [SerializeField] private Transform choicesContainer;

    [Header("Upgrade Pool")]
    [SerializeField] private List<StatUpgradeData> availableUpgrades;

    [Header("Rarity Settings")]
    [SerializeField] private List<RarityTier> rarityTiers;
    [Tooltip("The luck value at which rarities will have their 'Max Luck Weight'.")]
    [SerializeField] private float maxLuckForRarity = 100f;

    public class GeneratedUpgrade
    {
        public StatUpgradeData BaseData;
        public RarityTier Rarity;
        public float Value;
    }

    private Queue<int> levelUpQueue = new Queue<int>();
    private bool isUpgradeInProgress = false;

    void Awake()
    {
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

    public void AddLevelUpToQueue()
    {
        levelUpQueue.Enqueue(1);
        if (!isUpgradeInProgress)
        {
            ProcessNextUpgrade();
        }
    }

    private void ProcessNextUpgrade()
    {
        if (levelUpQueue.Count > 0)
        {
            isUpgradeInProgress = true;
            levelUpQueue.Dequeue();
            ShowUpgradeChoices();
        }
        else
        {
            isUpgradeInProgress = false;
        }
    }
    
    private void ShowUpgradeChoices()
    {
        foreach (Transform child in choicesContainer)
        {
            Destroy(child.gameObject);
        }

        int choicesCount = GetNumberOfChoices(playerStats.luck);
        List<GeneratedUpgrade> choices = GenerateUpgradeChoices(choicesCount);
        DisplayUpgradeChoices(choices);
        
        upgradePanel.SetActive(true);
        Time.timeScale = 0f;
    }
    
    private int GetNumberOfChoices(float currentLuck)
    {
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

            int randomIndex = Random.Range(0, availableUpgradesCopy.Count);
            StatUpgradeData chosenData = availableUpgradesCopy[randomIndex];
            availableUpgradesCopy.RemoveAt(randomIndex);

            var generatedUpgrade = new GeneratedUpgrade();
            generatedUpgrade.BaseData = chosenData;
            generatedUpgrade.Rarity = DetermineRarity(playerStats.luck);
            
            float luckAsPercentage = playerStats.luck / 100f;
            float rangeDifference = chosenData.baseValueMax - chosenData.baseValueMin;
            float boostAmount = rangeDifference * luckAsPercentage;
            float luckAdjustedMin = chosenData.baseValueMin + boostAmount;
            
            float effectiveMin = Mathf.Min(luckAdjustedMin, chosenData.baseValueMax);
            
            float baseValue = Random.Range(effectiveMin, chosenData.baseValueMax);
            
            generatedUpgrade.Value = baseValue * generatedUpgrade.Rarity.valueMultiplier;

            generatedChoices.Add(generatedUpgrade);
        }
        return generatedChoices;
    }

    private RarityTier DetermineRarity(float currentLuck)
    {
        if (rarityTiers == null || rarityTiers.Count == 0) return null;

        float blendFactor = Mathf.Clamp01(currentLuck / maxLuckForRarity);

        var modifiedWeights = new List<float>();
        float totalWeight = 0;

        foreach (var tier in rarityTiers)
        {
            float currentWeight = Mathf.Lerp(tier.baseWeight, tier.maxLuckWeight, blendFactor);
            if (currentWeight > 0)
            {
                modifiedWeights.Add(currentWeight);
                totalWeight += currentWeight;
            } else {
                modifiedWeights.Add(0);
            }
        }
        
        float randomRoll = Random.Range(0, totalWeight);
        float currentWeightSum = 0;

        for (int i = 0; i < rarityTiers.Count; i++)
        {
            currentWeightSum += modifiedWeights[i];
            if (randomRoll <= currentWeightSum)
            {
                return rarityTiers[i];
            }
        }
        
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
        
        if (levelUpQueue.Count > 0)
        {
            ProcessNextUpgrade();
        }
        else
        {
            isUpgradeInProgress = false;
            foreach (Transform child in choicesContainer)
            {
                Destroy(child.gameObject);
            }
            upgradePanel.SetActive(false);
            Time.timeScale = 1f;
        }
    }
}