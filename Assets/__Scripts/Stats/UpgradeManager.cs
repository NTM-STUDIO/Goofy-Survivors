using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;

public class UpgradeManager : NetworkBehaviour
{
    public static UpgradeManager Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject upgradePanel;
    [SerializeField] private UpgradeChoiceUI upgradeChoicePrefab;
    [SerializeField] private Transform choicesContainer;

    [Header("Upgrade Pool")]
    [SerializeField] private List<StatUpgradeData> availableUpgrades;

    [Header("Rarity Settings")]
    [SerializeField] private List<RarityTier> rarityTiers;

    [Header("Multiplayer Timer")]
    [SerializeField] private float mpChoiceTimeout = 5f; // Tempo limite para escolher em MP

    private GameManager gameManager;
    private PlayerStats playerStats; // Referência ao stats do jogador LOCAL
    private Coroutine autoChoiceCoroutine;
    private List<GeneratedUpgrade> currentChoices = new List<GeneratedUpgrade>();
    private bool hasChosenThisRound = false; // Se já escolheu nesta ronda
    private GeneratedUpgrade pendingUpgrade = null; // Upgrade escolhido a aguardar aplicação

    public class GeneratedUpgrade
    {
        public StatUpgradeData BaseData;
        public RarityTier Rarity;
        public float Value;
    }

    private Queue<int> levelUpQueue = new Queue<int>();
    private bool isUpgradeInProgress = false;

    // --- ADICIONAR ISTO AO UPGRADEMANAGER.CS ---

    public void ForceReset()
    {
        // 0. Cancela timer de escolha automática
        if (autoChoiceCoroutine != null)
        {
            StopCoroutine(autoChoiceCoroutine);
            autoChoiceCoroutine = null;
        }
        currentChoices.Clear();
        hasChosenThisRound = false;
        pendingUpgrade = null;

        // 1. Limpa a fila de níveis pendentes
        levelUpQueue.Clear();
        isUpgradeInProgress = false;

        // 2. Fecha o painel visualmente
        if (upgradePanel != null) upgradePanel.SetActive(false);

        // 3. Destrói as cartas que estavam lá (para não duplicar no próximo nível)
        if (choicesContainer != null)
        {
            foreach (Transform child in choicesContainer) Destroy(child.gameObject);
        }
        
        Debug.Log("[UpgradeManager] Estado resetado com sucesso.");
    }

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

    public void Initialize(GameObject playerObject)
    {
        if (playerObject != null)
            playerStats = playerObject.GetComponent<PlayerStats>();
    }

    // ----------------------------
    // CORREÇÃO AQUI: GARANTIR REFERÊNCIA AO JOGADOR LOCAL
    // ----------------------------
    public void GenerateAndShowOptions()
    {
        // Se não tivermos referência ao stats (comum no Cliente), tentamos encontrar agora
        if (playerStats == null)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null && NetworkManager.Singleton.LocalClient.PlayerObject != null)
            {
                playerStats = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerStats>();
                Debug.Log("[UpgradeManager] PlayerStats local encontrado dinamicamente.");
            }
            else
            {
                // Fallback para Singleplayer se a rede não estiver pronta
                playerStats = FindObjectOfType<PlayerStats>();
            }
        }

        if (playerStats == null)
        {
            Debug.LogError("[UpgradeManager] ERRO FATAL: Não foi possível encontrar o PlayerStats do jogador local! O menu não vai abrir.");
            return;
        }

        // Adiciona 1 nível à fila e processa
        EnqueueMultipleLevelUps(1);
    }

    public void EnqueueMultipleLevelUps(int amount)
    {
        if (playerStats == null) return;

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
                // Se for Multiplayer, avisa o servidor
                if (gameManager.isP2P && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                {
                    gameManager.ConfirmUpgradeSelectionServerRpc();
                }
                // Se for Singleplayer, resume direto
                else
                {
                    gameManager.RequestPause(false);
                }
            }
        }
    }

    private void ShowUpgradeChoices()
    {
        foreach (Transform child in choicesContainer) Destroy(child.gameObject);

        // Reset flags para nova ronda
        hasChosenThisRound = false;
        pendingUpgrade = null;

        // Usa a sorte do jogador local
        int choicesCount = GetNumberOfChoices(playerStats.luck);
        currentChoices = GenerateUpgradeChoices(choicesCount);
        DisplayUpgradeChoices(currentChoices);

        upgradePanel.SetActive(true);

        // Em Multiplayer, inicia timer de 5s (sempre espera os 5s completos)
        if (gameManager != null && gameManager.isP2P)
        {
            if (autoChoiceCoroutine != null) StopCoroutine(autoChoiceCoroutine);
            autoChoiceCoroutine = StartCoroutine(MPTimerCoroutine());
        }
    }

    private IEnumerator MPTimerCoroutine()
    {
        // Espera os 5 segundos completos (tempo real, não afetado por Time.timeScale)
        float elapsed = 0f;
        while (elapsed < mpChoiceTimeout)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // Tempo esgotado
        if (!hasChosenThisRound)
        {
            // Não escolheu - dá aleatório
            Debug.Log("[UpgradeManager] Tempo esgotado! Escolhendo upgrade aleatório...");
            if (currentChoices != null && currentChoices.Count > 0)
            {
                int randomIndex = Random.Range(0, currentChoices.Count);
                pendingUpgrade = currentChoices[randomIndex];
            }
        }

        // Aplica o upgrade (escolhido ou aleatório)
        if (pendingUpgrade != null)
        {
            ApplyUpgradeInternal(pendingUpgrade);
        }
        
        // Limpa UI e processa próximo
        FinalizeCurrentUpgrade();
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
        // Se a lista estiver vazia, evita erro
        if (availableUpgrades == null || availableUpgrades.Count == 0) return generatedChoices;

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
            uiInstance.Setup(choice, this);
            if (firstChoice == null) firstChoice = uiInstance.gameObject;
        }

        if (firstChoice != null)
            EventSystem.current.SetSelectedGameObject(firstChoice);
    }

    // Chamado quando o jogador clica numa carta
    public void ApplyUpgrade(GeneratedUpgrade upgrade)
    {
        // Em MP, guarda a escolha e esconde as cartas (mas espera o timer acabar)
        if (gameManager != null && gameManager.isP2P)
        {
            if (hasChosenThisRound) return; // Já escolheu, ignora
            
            hasChosenThisRound = true;
            pendingUpgrade = upgrade;
            
            // Esconde as cartas visualmente para dar feedback
            foreach (Transform child in choicesContainer) 
                Destroy(child.gameObject);
            
            Debug.Log("[UpgradeManager] Escolha registada. Aguardando fim do timer...");
            return; // NÃO aplica ainda, o timer irá aplicar
        }

        // Em Singleplayer, aplica imediatamente
        ApplyUpgradeInternal(upgrade);
        ProcessNextUpgrade();
    }

    // Aplica o upgrade aos stats do jogador
    private void ApplyUpgradeInternal(GeneratedUpgrade upgrade)
    {
        if (upgrade == null || playerStats == null) return;
        
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
            case StatType.CooldownReduction: playerStats.IncreaseCooldownReduction(value / 100f); break;
            case StatType.DurationMultiplier: playerStats.IncreaseDurationMultiplier(value / 100f); break;
            case StatType.KnockbackMultiplier: playerStats.IncreaseKnockbackMultiplier(value / 100f); break;
            case StatType.MovementSpeed: playerStats.IncreaseMovementSpeed(value / 100f * playerStats.movementSpeed); break;
            case StatType.Luck: playerStats.IncreaseLuck(value); break;
            case StatType.PickupRange: playerStats.IncreasePickupRange(value * playerStats.pickupRange - playerStats.pickupRange); break;
            case StatType.XPGainMultiplier: playerStats.IncreaseXPGainMultiplier(value / 100f); break;
        }
        
        Debug.Log($"[UpgradeManager] Upgrade aplicado: {upgrade.BaseData.statToUpgrade} +{value:F1}");
    }

    // Chamado após o timer acabar em MP
    private void FinalizeCurrentUpgrade()
    {
        // Limpa UI
        upgradePanel.SetActive(false);
        foreach (Transform child in choicesContainer) Destroy(child.gameObject);
        EventSystem.current.SetSelectedGameObject(null);

        // Reset
        hasChosenThisRound = false;
        pendingUpgrade = null;
        currentChoices.Clear();
        autoChoiceCoroutine = null;

        // Verifica se há mais níveis na fila
        if (levelUpQueue.Count > 0)
        {
            levelUpQueue.Dequeue();
            ShowUpgradeChoices();
        }
        else
        {
            // Acabaram-se os upgrades, avisa o servidor
            isUpgradeInProgress = false;
            if (gameManager != null && NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                gameManager.ConfirmUpgradeSelectionServerRpc();
            }
        }
    }

    // --- MÉTODOS AUXILIARES ---
    
    public void ClosePanel()
    {
        if (upgradePanel != null) upgradePanel.SetActive(false);
        if (choicesContainer != null)
        {
            foreach (Transform child in choicesContainer) Destroy(child.gameObject);
        }
    }

    public void PresentGuaranteedRarityChoices(RarityTier guaranteedRarity)
    {
        if (guaranteedRarity == null) return;
        
        // NOTA: Isto deve ser chamado via GameManager para sincronizar pausa se for necessário
        foreach (Transform child in choicesContainer) Destroy(child.gameObject);

        // Garante stats
        if (playerStats == null && NetworkManager.Singleton.LocalClient?.PlayerObject != null)
            playerStats = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerStats>();

        int choicesCount = 1;
        List<GeneratedUpgrade> choices = GenerateGuaranteedUpgradeChoices(choicesCount, guaranteedRarity);
        DisplayUpgradeChoices(choices);
        upgradePanel.SetActive(true);
    }

    private List<GeneratedUpgrade> GenerateGuaranteedUpgradeChoices(int count, RarityTier guaranteedRarity)
    {
        var generatedChoices = new List<GeneratedUpgrade>();
        if (availableUpgrades == null || availableUpgrades.Count == 0) return generatedChoices;
        
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

            float luckValue = (playerStats != null) ? playerStats.luck : 0f;
            float luckAsPercentage = luckValue / 100f;
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