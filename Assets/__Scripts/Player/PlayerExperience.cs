using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using System.Collections.Generic;
using System.Linq;

public class PlayerExperience : NetworkBehaviour
{
    public static PlayerExperience Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private Slider xpSlider;
    [SerializeField] private TMP_Text levelText;

    [Header("System References")]
    [SerializeField] private UpgradeManager upgradeManager;

    [Header("Global Team State")]
    // NetworkVariables para Multiplayer
    private readonly NetworkVariable<int> netCurrentLevel = new NetworkVariable<int>(1);
    private readonly NetworkVariable<float> netCurrentXP = new NetworkVariable<float>(0f);
    private readonly NetworkVariable<float> netXpToNextLevel = new NetworkVariable<float>(100f);

    // Variáveis locais para Singleplayer (quando a rede está desligada)
    private int localCurrentLevel = 1;
    private float localCurrentXP = 0f;
    private float localXpToNextLevel = 100f;

    [Header("XP Curve Settings")]
    [SerializeField] private float earlyLevelXpBonus = 60f;
    [SerializeField] private float midGameScalingFactor = 1.1f;
    [SerializeField] private float lateGameScalingFactor = 1.01f;
    [SerializeField] private float endGameScalingFactor = 1.01f;

    // --- PROPRIEDADES INTELIGENTES (Detectam SP vs MP) ---
    public int CurrentLevel 
    {
        get => IsNetworkRunning ? netCurrentLevel.Value : localCurrentLevel;
        private set 
        {
            if (IsNetworkRunning && IsServer) netCurrentLevel.Value = value;
            else localCurrentLevel = value;
        }
    }

    public float CurrentXP 
    {
        get => IsNetworkRunning ? netCurrentXP.Value : localCurrentXP;
        private set 
        {
            if (IsNetworkRunning && IsServer) netCurrentXP.Value = value;
            else localCurrentXP = value;
        }
    }

    public float XpToNextLevel 
    {
        get => IsNetworkRunning ? netXpToNextLevel.Value : localXpToNextLevel;
        private set 
        {
            if (IsNetworkRunning && IsServer) netXpToNextLevel.Value = value;
            else localXpToNextLevel = value;
        }
    }

    private bool IsNetworkRunning => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    public void Initialize()
    {
        if (upgradeManager == null) upgradeManager = FindObjectOfType<UpgradeManager>();
        UpdateUI();
    }

    public override void OnNetworkSpawn()
    {
        // Sincroniza UI quando os valores mudam na rede
        netCurrentXP.OnValueChanged += OnXPChanged;
        netCurrentLevel.OnValueChanged += OnLevelChanged;
        netXpToNextLevel.OnValueChanged += OnXPToNextChanged;

        Debug.Log($"[PlayerExperience] OnNetworkSpawn - IsServer:{IsServer} - XP:{netCurrentXP.Value} Level:{netCurrentLevel.Value}");
        UpdateUI();
    }

    private void OnXPChanged(float prev, float curr)
    {
        Debug.Log($"[PlayerExperience] XP Changed: {prev:F1} -> {curr:F1} (IsServer:{IsServer})");
        UpdateUI();
    }

    private void OnLevelChanged(int prev, int curr)
    {
        Debug.Log($"[PlayerExperience] Level Changed: {prev} -> {curr} (IsServer:{IsServer})");
        UpdateUI();
    }

    private void OnXPToNextChanged(float prev, float curr)
    {
        UpdateUI();
    }

    public override void OnNetworkDespawn()
    {
        netCurrentXP.OnValueChanged -= OnXPChanged;
        netCurrentLevel.OnValueChanged -= OnLevelChanged;
        netXpToNextLevel.OnValueChanged -= OnXPToNextChanged;
    }

    public void ResetState()
    {
        // Reseta tanto as vars de rede como as locais
        if (IsNetworkRunning && IsServer)
        {
            netCurrentLevel.Value = 1;
            netCurrentXP.Value = 0f;
            netXpToNextLevel.Value = 100f;
        }
        
        localCurrentLevel = 1;
        localCurrentXP = 0f;
        localXpToNextLevel = 100f;
        
        UpdateUI();
    }

    // --- CHAMADO PELO GAMEMANAGER OU ORBS ---
    public void AddGlobalXP(float amount)
    {
        // Em MP, só o servidor calcula. Em SP, calcula sempre.
        if (IsNetworkRunning && !IsServer) return;

        // 1. Calcular Multiplicador de Equipa
        float teamMultiplier = GetTeamXPMultiplier();
        float finalXp = amount * teamMultiplier;

        // 2. Adicionar XP
        // Usamos a propriedade CurrentXP que já trata de setar na rede ou local
        float newXP = CurrentXP + finalXp;
        
        if (IsNetworkRunning && IsServer) netCurrentXP.Value = newXP;
        else localCurrentXP = newXP;

        // 3. Verificar Level Up
        int levelsGained = 0;
        
        // Usamos loop para suportar múltiplos níveis de uma vez
        while (CurrentXP >= XpToNextLevel)
        {
            LevelUpCalculation();
            levelsGained++;
        }

        // 4. Se subiu de nível
        if (levelsGained > 0)
        {
            if (IsNetworkRunning)
            {
                if (IsServer) GameManager.Instance.TriggerTeamLevelUp();
            }
            else
            {
                // Singleplayer Puro
                if (upgradeManager != null)
                {
                    GameManager.Instance.RequestPauseForLevelUp();
                    upgradeManager.EnqueueMultipleLevelUps(levelsGained);
                }
            }
        }
        
        // Em SP, atualiza a UI imediatamente. Em MP, o OnValueChanged trata disso.
        if (!IsNetworkRunning) UpdateUI();
    }

    private float GetTeamXPMultiplier()
    {
        // Encontra todos os PlayerStats na cena (funciona em SP e MP)
        var allStats = FindObjectsByType<PlayerStats>(FindObjectsSortMode.None);
        
        if (allStats.Length == 0) return 1f;

        float totalMult = 0f;
        foreach (var stat in allStats)
        {
            totalMult += stat.xpGainMultiplier;
        }

        // Retorna a Média da equipa
        return totalMult / allStats.Length;
    }

    private void LevelUpCalculation()
    {
        float currentReq = XpToNextLevel;
        
        // Subtrai XP e sobe nível
        if (IsNetworkRunning && IsServer)
        {
            netCurrentXP.Value -= currentReq;
            netCurrentLevel.Value++;
        }
        else
        {
            localCurrentXP -= currentReq;
            localCurrentLevel++;
        }

        int newLvl = CurrentLevel; // Lê o valor atualizado

        // Calcula nova dificuldade
        if (newLvl <= 10) currentReq += earlyLevelXpBonus;
        else if (newLvl <= 25) currentReq *= midGameScalingFactor;
        else if (newLvl <= 35) currentReq *= lateGameScalingFactor;
        else currentReq *= endGameScalingFactor;

        // Atualiza requisito
        if (IsNetworkRunning && IsServer) netXpToNextLevel.Value = Mathf.FloorToInt(currentReq);
        else localXpToNextLevel = Mathf.FloorToInt(currentReq);
    }

    private void UpdateUI()
    {
        // 1. Tenta usar o UIManager
        var ui = GameManager.Instance.uiManager;
        if (ui != null)
        {
            ui.UpdateXPBar(CurrentXP, XpToNextLevel);
            ui.UpdateLevelText(CurrentLevel);
        }
        // 2. Fallback local
        else
        {
            if (xpSlider != null)
            {
                xpSlider.maxValue = XpToNextLevel;
                xpSlider.value = CurrentXP;
            }
            if (levelText != null)
            {
                levelText.text = $"Lvl: {CurrentLevel}";
            }
        }
    }
}