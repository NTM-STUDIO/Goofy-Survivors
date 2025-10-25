using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.Linq;

public class UpgradeManager : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject upgradePanel;
    [SerializeField] private UpgradeChoiceUI upgradeChoicePrefab;
    [SerializeField] private Transform choicesContainer;
    
    [Header("Upgrade Pool")]
    [SerializeField] private List<StatUpgradeData> availableUpgrades;

    [Header("Rarity Settings")]
    [SerializeField] private List<RarityTier> rarityTiers;
    [Tooltip("The luck value at which rarities will have their 'Max Luck Weight'.")]
    [SerializeField] private float maxLuckForRarity = 100f;

    // Internal References
    private GameManager gameManager;
    private PlayerStats playerStats; 

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
        gameManager = GameManager.Instance;
    }

    /// <summary>
    /// Called by GameManager after the player is spawned.
    /// </summary>
    public void Initialize(GameObject playerObject)
    {
        if (playerObject != null)
        {
            playerStats = playerObject.GetComponent<PlayerStats>();
        }

        if (playerStats == null)
        {
            Debug.LogError("FATAL ERROR: UpgradeManager could not find PlayerStats on the spawned player object! Leveling up will fail.", this);
            enabled = false;
        }
        else
        {
            Debug.Log("UpgradeManager Initialized successfully.");
        }
    }

    /// <summary>
    /// Enqueue a single level-up
    /// </summary>
    public void AddLevelUpToQueue()
    {
        EnqueueMultipleLevelUps(1);
    }

    /// <summary>
    /// Enqueue multiple level-ups at once (corrects pause issue)
    /// </summary>
    public void EnqueueMultipleLevelUps(int amount)
    {
        if (playerStats == null || amount <= 0) return;

        for (int i = 0; i < amount; i++)
            levelUpQueue.Enqueue(1);

        // Process the queue if not already in progress
        if (!isUpgradeInProgress)
            ProcessNextUpgrade();
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
            // All upgrades processed
            isUpgradeInProgress = false;
            upgradePanel.SetActive(false);
            foreach (Transform child in choicesContainer) Destroy(child.gameObject);
            gameManager.RequestResume(); // resume only once
            EventSystem.current.SetSelectedGameObject(null);
        }
    }
    
    private void ShowUpgradeChoices()
    {
        // Clear previous choices
        foreach (Transform child in choicesContainer)
            Destroy(child.gameObject);

        int choicesCount = GetNumberOfChoices(playerStats.luck);
        List<GeneratedUpgrade> choices = GenerateUpgradeChoices(choicesCount);
        DisplayUpgradeChoices(choices);

        upgradePanel.SetActive(true);
        gameManager.RequestPause(); // pause only once at start of this panel
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
            } 
            else 
            {
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
        GameObject firstChoice = null; 
        foreach (var choice in choices)
        {
            UpgradeChoiceUI uiInstance = Instantiate(upgradeChoicePrefab, choicesContainer);
            uiInstance.Setup(choice, this);
            if (firstChoice == null) firstChoice = uiInstance.gameObject;
        }

        if (firstChoice != null)
            EventSystem.current.SetSelectedGameObject(firstChoice);
    }

    public void ApplyUpgrade(GeneratedUpgrade upgrade)
    {
        float value = upgrade.Value;
        switch (upgrade.BaseData.statToUpgrade)
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
        
        ProcessNextUpgrade(); // chama próximo painel ou fecha todos
    }

    public List<StatUpgradeData> GetAvailableUpgrades() { return availableUpgrades; }
    public List<RarityTier> GetRarityTiers() { return rarityTiers; }
}
