using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;

public class PlayerExperience : NetworkBehaviour
{
    [Header("UI References")]
    [SerializeField] private Slider xpSlider;
    [SerializeField] private TMP_Text levelText;

    [Header("System")]
    [SerializeField] private UpgradeManager upgradeManager;

    [Header("State")]
    [SerializeField] private int currentLevel = 1;
    [SerializeField] private float currentXP = 0;
    [SerializeField] private float xpToNextLevel = 100;

    // Settings de Curva de XP...
    [SerializeField] private float earlyLevelXpBonus = 60f;
    [SerializeField] private float midGameScalingFactor = 1.1f;
    [SerializeField] private float lateGameScalingFactor = 1.01f;
    [SerializeField] private float endGameScalingFactor = 1.01f;

    private PlayerStats playerStats;

    public void Initialize(GameObject playerObject)
    {
        if (upgradeManager == null) upgradeManager = FindObjectOfType<UpgradeManager>();
        
        // Tenta encontrar UI no UIManager se as referências locais falharem
        if (xpSlider == null && GameManager.Instance.uiManager != null)
        {
            // Nota: Se não tiveres acesso direto às variaveis do UIManager, 
            // o UpdateUI abaixo trata disso através de métodos públicos.
        }

        playerStats = playerObject.GetComponent<PlayerStats>();
        UpdateUI();
    }

    public void ResetState()
    {
        currentLevel = 1;
        currentXP = 0f;
        xpToNextLevel = 100f;
        UpdateUI();
    }

    // Recebe XP localmente (Singleplayer)
    public void AddXP(float xp)
    {
        if (!enabled) return;
        
        float multiplier = (playerStats != null) ? playerStats.xpGainMultiplier : 1f;
        float finalXp = xp * multiplier;

        if (GameManager.Instance.isP2P)
        {
            if (IsServer) ProcessXPGain(finalXp);
        }
        else
        {
            ProcessXPGain(finalXp);
        }
    }

    // Recebe XP do Servidor (Multiplayer)
    public void AddXPFromServerScaled(float scaledXp)
    {
        if (!enabled) return;
        ProcessXPGain(scaledXp);
    }

    private void ProcessXPGain(float amount)
    {
        currentXP += amount;
        int levelsGained = 0;

        while (currentXP >= xpToNextLevel)
        {
            currentXP -= xpToNextLevel;
            currentLevel++;
            levelsGained++;

            if (playerStats != null) playerStats.ApplyLevelUpScaling();

            // Curva XP
            if (currentLevel <= 10) xpToNextLevel += earlyLevelXpBonus;
            else if (currentLevel <= 25) xpToNextLevel *= midGameScalingFactor;
            else if (currentLevel <= 35) xpToNextLevel *= lateGameScalingFactor;
            else xpToNextLevel *= endGameScalingFactor;
            xpToNextLevel = Mathf.FloorToInt(xpToNextLevel);
        }

        UpdateUI();

        if (levelsGained > 0)
        {
            if (GameManager.Instance.isP2P)
            {
                if (IsServer) GameManager.Instance.TriggerTeamLevelUp();
            }
            else
            {
                if (upgradeManager != null)
                {
                    GameManager.Instance.RequestPauseForLevelUp();
                    upgradeManager.EnqueueMultipleLevelUps(levelsGained);
                }
            }
        }
    }

    private void UpdateUI()
    {
        // Tenta atualizar via UIManager primeiro (mais seguro em MP)
        if (GameManager.Instance.uiManager != null)
        {
            GameManager.Instance.uiManager.UpdateXPBar(currentXP, xpToNextLevel);
            GameManager.Instance.uiManager.UpdateLevelText(currentLevel);
        }
        // Fallback local
        else
        {
            if (xpSlider != null)
            {
                xpSlider.maxValue = xpToNextLevel;
                xpSlider.value = currentXP;
            }
            if (levelText != null) levelText.text = $"Lvl: {currentLevel}";
        }
    }
}