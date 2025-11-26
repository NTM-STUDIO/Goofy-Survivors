using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode; // Adicionado

public class UpgradeManager : NetworkBehaviour // Mudado de MonoBehaviour para NetworkBehaviour
{
    public static UpgradeManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        gameManager = GameManager.Instance;
    }

    // --- ADICIONA ISTO AO UPGRADEMANAGER.CS ---

    public void ClosePanel()
    {
        // Fecha o painel visualmente
        if (upgradePanel != null)
        {
            upgradePanel.SetActive(false);

            
        }

        // Limpa os botões antigos para não ficarem lá quando abrires da próxima vez
        if (choicesContainer != null)
        {
            foreach (Transform child in choicesContainer)
            {
                Destroy(child.gameObject);
            }
        }
    }

    [Header("UI References")]
    [SerializeField] private GameObject upgradePanel;
    [SerializeField] private UpgradeChoiceUI upgradeChoicePrefab;
    [SerializeField] private Transform choicesContainer;

    [Header("Upgrade Pool")]
    [SerializeField] private List<StatUpgradeData> availableUpgrades;

    [Header("Rarity Settings")]
    [SerializeField] private List<RarityTier> rarityTiers;

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
    }

    // ----------------------------
    // 🔗 CONEXÃO COM GAME MANAGER (NOVO)
    // ----------------------------

    // Este é o método que o GameManager chama no ClientRpc
    public void GenerateAndShowOptions()
    {
        // Adiciona 1 nível à fila e processa
        EnqueueMultipleLevelUps(1);
    }

    // ----------------------------
    // 🎚️ LEVEL-UP QUEUE SYSTEM
    // ----------------------------
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
            // Acabaram-se as cartas.
            isUpgradeInProgress = false;
            upgradePanel.SetActive(false);
            
            foreach (Transform child in choicesContainer) Destroy(child.gameObject);
            EventSystem.current.SetSelectedGameObject(null);

            if (gameManager != null)
            {
                // --- CORREÇÃO AQUI ---
                
                // 1. Se for Multiplayer, avisa o servidor
                if (gameManager.isP2P && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                {
                    gameManager.ConfirmUpgradeSelectionServerRpc();
                }
                // 2. Se for Singleplayer, resume o jogo diretamente
                else
                {
                    gameManager.RequestPause(false); // false = Despausar
                }
            }
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

        // NOTA: Removemos o gameManager.RequestPauseForLevelUp() daqui.
        // O GameManager JÁ pausou o jogo via RPC antes de chamar este método.
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

            // Lógica de sorte
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

    private void DisplayUpgradeChoices(List<GeneratedUpgrade> choices)
    {
        GameObject firstChoice = null;
        foreach (var choice in choices)
        {
            UpgradeChoiceUI uiInstance = Instantiate(upgradeChoicePrefab, choicesContainer);
            // Liga o botão à função ApplyUpgrade
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

        // Processa o próximo da fila ou fecha e avisa o servidor
        ProcessNextUpgrade();
    }

    public void PresentGuaranteedRarityChoices(RarityTier guaranteedRarity)
    {
        if (guaranteedRarity == null)
        {
            Debug.LogError("Guaranteed rarity is null!");
            return;
        }

        // Para itens garantidos, ainda precisamos pausar, mas cuidado em MP
        // Se isto for um baú local, o GameManager deve tratar da pausa

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

    public List<StatUpgradeData> GetAvailableUpgrades() => availableUpgrades;
    public List<RarityTier> GetRarityTiers() => rarityTiers;
}