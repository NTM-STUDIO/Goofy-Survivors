using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerExperience : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Slider xpSlider;
    [SerializeField] private TMP_Text levelText;

    [Header("System References")]
    [SerializeField] private UpgradeManager upgradeManager;

    [Header("Experience State")]
    [SerializeField] private int currentLevel = 1;
    [SerializeField] private float currentXP = 0;
    [SerializeField] private float xpToNextLevel = 100;

    [Header("XP Curve Settings")]
    [SerializeField] private float earlyLevelXpBonus = 60f;
    [SerializeField] private float midGameScalingFactor = 1.1f;
    [SerializeField] private float lateGameScalingFactor = 1.01f;
    [SerializeField] private float endGameScalingFactor = 1.01f;

    private PlayerStats playerStats;

    public void Initialize(GameObject playerObject)
    {
        if (upgradeManager == null || xpSlider == null || levelText == null)
        {
            Debug.LogError("PlayerExperience missing references!", this);
            enabled = false;
            return;
        }

        playerStats = playerObject.GetComponent<PlayerStats>();
        if (playerStats == null)
        {
            Debug.LogError("PlayerExperience: Could not find PlayerStats on player!", this);
            enabled = false;
            return;
        }

        UpdateUI();
    }

    public void AddXP(float xp)
    {
        if (!enabled) return;

        // Aplica multiplicador
        currentXP += xp * (playerStats != null ? playerStats.xpGainMultiplier : 1f);

        int levelUps = 0;

        // Conta quantos níveis sobem
        while (currentXP >= xpToNextLevel)
        {
            currentXP -= xpToNextLevel;
            currentLevel++;
            levelUps++;

            if (playerStats != null)
                playerStats.ApplyLevelUpScaling();

            // Ajusta XP necessário
            if (currentLevel <= 10) xpToNextLevel += earlyLevelXpBonus;
            else if (currentLevel <= 25) xpToNextLevel *= midGameScalingFactor;
            else if (currentLevel <= 35) xpToNextLevel *= lateGameScalingFactor;
            else xpToNextLevel *= endGameScalingFactor;

            xpToNextLevel = Mathf.FloorToInt(xpToNextLevel);
        }

        // Só chama UpgradeManager **uma vez** com o total de level-ups
        if (levelUps > 0 && upgradeManager != null)
        {
            upgradeManager.EnqueueMultipleLevelUps(levelUps);
        }

        UpdateUI();
    }

    private void LevelUp()
    {
        currentXP -= xpToNextLevel;
        currentLevel++;

        if (playerStats != null)
            playerStats.ApplyLevelUpScaling();

        if (currentLevel <= 10)
            xpToNextLevel += earlyLevelXpBonus;
        else if (currentLevel <= 25)
            xpToNextLevel *= midGameScalingFactor;
        else if (currentLevel <= 35)
            xpToNextLevel *= lateGameScalingFactor;
        else
            xpToNextLevel *= endGameScalingFactor;

        xpToNextLevel = Mathf.FloorToInt(xpToNextLevel);
    }

    private void UpdateUI()
    {
        if (xpSlider == null || levelText == null) return;

        levelText.text = $"Lvl: {currentLevel}";
        xpSlider.maxValue = xpToNextLevel;
        xpSlider.value = currentXP;
    }
}
