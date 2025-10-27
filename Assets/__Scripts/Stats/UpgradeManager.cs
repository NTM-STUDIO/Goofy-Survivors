using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.Linq;

public class UpgradeManager : MonoBehaviour
{
    // ----------------------------
    // 🧩 SINGLETON SETUP
    // ----------------------------
    public static UpgradeManager Instance { get; private set; }

    private void Awake()
    {
        // Standard safe singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Optional: Persist between scenes (uncomment if needed)
        // DontDestroyOnLoad(gameObject);

        gameManager = GameManager.Instance;
    }

    // ----------------------------
    // 🧱 CONFIGURABLE FIELDS
    // ----------------------------

    [Header("UI References")]
    [SerializeField] private GameObject upgradePanel;
    [SerializeField] private UpgradeChoiceUI upgradeChoicePrefab;
    [SerializeField] private Transform choicesContainer;

    [Header("Upgrade Pool")]
    [SerializeField] private List<StatUpgradeData> availableUpgrades;

    [Header("Rarity Settings")]
    [Tooltip("Rarities ordered from lowest to highest.")]
    [SerializeField] private List<RarityTier> rarityTiers;

    private GameManager gameManager;
    private PlayerStats playerStats;

    // ----------------------------
    // 📦 INTERNAL STRUCTS
    // ----------------------------
    public class GeneratedUpgrade
    {
        public StatUpgradeData BaseData;
        public RarityTier Rarity;
        public float Value;
    }

    // ----------------------------
    // ⚙️ INTERNAL STATE
    // ----------------------------
    private Queue<int> levelUpQueue = new Queue<int>();
    private bool isUpgradeInProgress = false;

    // ----------------------------
    // 🚀 INITIALIZATION
    // ----------------------------
    public void Initialize(GameObject playerObject)
    {
        if (playerObject != null)
            playerStats = playerObject.GetComponent<PlayerStats>();

        if (playerStats == null)
        {
            Debug.LogError("FATAL ERROR: UpgradeManager could not find PlayerStats!", this);
            enabled = false;
        }
        else
        {
            Debug.Log("UpgradeManager initialized successfully.");
        }
    }

    // ----------------------------
    // 🎚️ LEVEL-UP QUEUE SYSTEM
    // ----------------------------
    public void AddLevelUpToQueue() => EnqueueMultipleLevelUps(1);

    public void EnqueueMultipleLevelUps(int amount)
    {
        if (playerStats == null || amount <= 0) return;

        for (int i = 0; i < amount; i++)
            levelUpQueue.Enqueue(1);

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
            isUpgradeInProgress = false;
            upgradePanel.SetActive(false);
            foreach (Transform child in choicesContainer) Destroy(child.gameObject);
            gameManager.RequestResume();
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    // ----------------------------
    // 🧮 UPGRADE GENERATION
    // ----------------------------
    private void ShowUpgradeChoices()
    {
        foreach (Transform child in choicesContainer) Destroy(child.gameObject);

        int choicesCount = GetNumberOfChoices(playerStats.luck);
        List<GeneratedUpgrade> choices = GenerateUpgradeChoices(choicesCount);
        DisplayUpgradeChoices(choices);

        upgradePanel.SetActive(true);
        gameManager.RequestPause();
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

            var generatedUpgrade = new GeneratedUpgrade
            {
                BaseData = chosenData,
                Rarity = DetermineRarity(playerStats.luck)
            };

            float luckAsPercentage = playerStats.luck / 500f;
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

    // ----------------------------
    // 💎 RARITY DETERMINATION
    // ----------------------------
    private RarityTier DetermineRarity(float currentLuck)
    {
        if (rarityTiers == null || rarityTiers.Count == 0) return null;

        List<float[]> anchors = new List<float[]>
        {
            new float[] {100, 0, 0, 0, 0, 0, 0},
            new float[] {0, 60, 20, 10, 4.99f, 0.1f, 0.01f},
            new float[] {0, 0, 70, 20, 10, 1, 0.1f},
            new float[] {0, 0, 0, 70, 25, 5, 1},
            new float[] {0, 0, 0, 0, 70, 25, 5},
            new float[] {0, 0, 0, 0, 0, 80, 20}
        };

        float[] interpolatedWeights = new float[rarityTiers.Count];
        int lowerIndex = Mathf.FloorToInt(currentLuck / 100f);
        int upperIndex = Mathf.Clamp(lowerIndex + 1, 0, anchors.Count - 1);
        float t = Mathf.InverseLerp(lowerIndex * 100f, upperIndex * 100f, currentLuck);

        for (int i = 0; i < rarityTiers.Count; i++)
            interpolatedWeights[i] = Mathf.Lerp(anchors[lowerIndex][i], anchors[upperIndex][i], t);

        float total = interpolatedWeights.Sum();
        for (int i = 0; i < interpolatedWeights.Length; i++)
            interpolatedWeights[i] /= total;

        float roll = Random.value;
        float accum = 0f;
        for (int i = 0; i < rarityTiers.Count; i++)
        {
            accum += interpolatedWeights[i];
            if (roll <= accum) return rarityTiers[i];
        }

        return rarityTiers.Last();
    }

    // ----------------------------
    // 🧰 DISPLAY + APPLY
    // ----------------------------
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

        ProcessNextUpgrade();
    }

    // ----------------------------
    // 💫 GUARANTEED RARITY
    // ----------------------------
    public void PresentGuaranteedRarityChoices(RarityTier guaranteedRarity)
    {
        if (guaranteedRarity == null)
        {
            Debug.LogError("Guaranteed rarity is null!");
            return;
        }

        gameManager.RequestPause();
        foreach (Transform child in choicesContainer) Destroy(child.gameObject);

        int choicesCount = 1;
        List<GeneratedUpgrade> choices = GenerateGuaranteedUpgradeChoices(choicesCount, guaranteedRarity);
        DisplayUpgradeChoices(choices);
        upgradePanel.SetActive(true);
    }

    private List<GeneratedUpgrade> GenerateGuaranteedUpgradeChoices(int count, RarityTier guaranteedRarity)
    {
        var generatedChoices = new List<GeneratedUpgrade>();
        var availableUpgradesCopy = new List<StatUpgradeData>(availableUpgrades);

        for (int i = 0; i < count; i++)
        {
            if (availableUpgradesCopy.Count == 0) break;

            int randomIndex = Random.Range(0, availableUpgradesCopy.Count);
            StatUpgradeData chosenData = availableUpgradesCopy[randomIndex];
            availableUpgradesCopy.RemoveAt(randomIndex);

            var generatedUpgrade = new GeneratedUpgrade
            {
                BaseData = chosenData,
                Rarity = guaranteedRarity
            };

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

    // ----------------------------
    // 📤 GETTERS
    // ----------------------------
    public List<StatUpgradeData> GetAvailableUpgrades() => availableUpgrades;
    public List<RarityTier> GetRarityTiers() => rarityTiers;
}
