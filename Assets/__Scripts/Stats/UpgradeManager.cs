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
    [Tooltip("A lista de raridades, ordenada da mais baixa para a mais alta.")]
    [SerializeField] private List<RarityTier> rarityTiers;

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

    /// <summary>
    /// Determines a rarity based on luck by gradually removing weight from lower tiers 
    /// and redistributing it proportionally to higher tiers.
    /// </summary>
    private RarityTier DetermineRarity(float currentLuck)
    {
        if (rarityTiers == null || rarityTiers.Count == 0) return null;

        // Define o intervalo de sorte para "drenar" completamente o peso de uma raridade.
        const float luckRangePerTier = 100f;

        // 1. Inicializa os pesos finais com os pesos base de cada raridade.
        var finalWeights = new List<float>();
        foreach (var tier in rarityTiers)
        {
            finalWeights.Add(tier.baseWeight);
        }

        // 2. Itera através de cada tier para calcular a redistribuição de peso.
        for (int i = 0; i < rarityTiers.Count; i++)
        {
            float tierLuckStart = i * luckRangePerTier;       // Ex: Comum (i=0) começa a perder peso em 0 de sorte.
            float tierLuckEnd = (i + 1) * luckRangePerTier; // Ex: Comum (i=0) tem seu peso zerado em 100 de sorte.

            // Se a sorte atual não está na faixa de "drenagem" deste tier, podemos parar.
            if (currentLuck < tierLuckStart)
            {
                break; // Otimização: como os tiers são ordenados, não há mais redistribuições a fazer.
            }

            // Calcula a porcentagem de redução (de 0.0 a 1.0) para o tier atual.
            float reductionFactor = Mathf.InverseLerp(tierLuckStart, tierLuckEnd, currentLuck);

            // Calcula o peso exato a ser removido do tier atual e redistribuído.
            float weightToRedistribute = rarityTiers[i].baseWeight * reductionFactor;

            if (weightToRedistribute <= 0) continue;

            // Reduz o peso do tier atual.
            finalWeights[i] -= weightToRedistribute;

            // 3. Redistribui o peso perdido para os tiers SUPERIORES.
            // Primeiro, calcula o peso total dos tiers que receberão a redistribuição.
            float totalWeightOfHigherTiers = 0;
            for (int j = i + 1; j < rarityTiers.Count; j++)
            {
                totalWeightOfHigherTiers += rarityTiers[j].baseWeight;
            }

            // Se houver tiers superiores para receber o peso...
            if (totalWeightOfHigherTiers > 0)
            {
                // Distribui o peso proporcionalmente com base no peso base de cada tier superior.
                for (int j = i + 1; j < rarityTiers.Count; j++)
                {
                    float proportion = rarityTiers[j].baseWeight / totalWeightOfHigherTiers;
                    finalWeights[j] += weightToRedistribute * proportion;
                }
            }
        }

        // 4. Sorteio ponderado final usando os 'finalWeights' calculados.
        float totalFinalWeight = finalWeights.Where(w => w > 0).Sum();

        if (totalFinalWeight <= 0) return rarityTiers.LastOrDefault(); // Fallback caso todos os pesos se tornem zero.

        float randomRoll = Random.Range(0, totalFinalWeight);
        float currentWeightSum = 0;

        for (int i = 0; i < rarityTiers.Count; i++)
        {
            if (finalWeights[i] <= 0) continue;

            currentWeightSum += finalWeights[i];
            if (randomRoll <= currentWeightSum)
            {
                return rarityTiers[i];
            }
        }

        return rarityTiers.Last(); // Fallback final
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

    public void PresentGuaranteedRarityChoices(RarityTier guaranteedRarity)
    {
        if (guaranteedRarity == null)
        {
            Debug.LogError("Tentativa de apresentar escolhas com uma raridade nula!");
            return;
        }

        // Pausa o jogo e limpa escolhas antigas
        gameManager.RequestPause();
        foreach (Transform child in choicesContainer)
            Destroy(child.gameObject);

        // Gera 3 opções de upgrade, todas com a raridade garantida
        int choicesCount = 3;
        List<GeneratedUpgrade> choices = GenerateGuaranteedUpgradeChoices(choicesCount, guaranteedRarity);

        // Mostra as opções na tela
        DisplayUpgradeChoices(choices);
        upgradePanel.SetActive(true);
    }

    /// <summary>
    /// Um método auxiliar para gerar upgrades com uma raridade forçada.
    /// </summary>
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
                // AQUI ESTÁ A MUDANÇA PRINCIPAL: Usa a raridade fornecida em vez de calculá-la
                Rarity = guaranteedRarity
            };

            // O cálculo do valor continua funcionando normalmente, usando a sorte para valores melhores
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

    public List<StatUpgradeData> GetAvailableUpgrades() { return availableUpgrades; }
    public List<RarityTier> GetRarityTiers() { return rarityTiers; }
}