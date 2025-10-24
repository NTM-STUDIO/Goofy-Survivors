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

    // --- NEW: Add a variable to hold the reference to the player's stats ---
    private PlayerStats playerStats;

    /// <summary>
    /// The GameManager calls this and GIVES it the spawned player.
    /// </summary>
    // --- MODIFIED: The Initialize method now accepts the player object ---
    public void Initialize(GameObject playerObject)
    {
        if (upgradeManager == null || xpSlider == null || levelText == null)
        {
            Debug.LogError("FATAL ERROR on PlayerExperience: One or more CRITICAL REFERENCES are NOT ASSIGNED in the Inspector! Leveling up will fail.", this);
            enabled = false;
            return;
        }

        // --- NEW: Get the PlayerStats component from the provided player object ---
        playerStats = playerObject.GetComponent<PlayerStats>();
        if (playerStats == null)
        {
            Debug.LogError("PlayerExperience could not find a PlayerStats component on the spawned player!", this);
            enabled = false;
            return;
        }
        
        UpdateUI();
        Debug.Log("PlayerExperience Manager Initialized successfully.");
    }

    public void AddXP(float xp)
    {
        if (enabled == false) return;
        
        // --- MODIFIED: Use the player's XP gain multiplier ---
        currentXP += xp * (playerStats != null ? playerStats.xpGainMultiplier : 1f);

        while (currentXP >= xpToNextLevel)
        {
            LevelUp();
        }
        UpdateUI();
    }

    private void LevelUp()
    {
        currentXP -= xpToNextLevel;
        currentLevel++;
        
        // --- NEW: Tell the PlayerStats component to apply its scaling bonuses ---
        if (playerStats != null)
        {
            playerStats.ApplyLevelUpScaling();
        }
        // --- End of new code ---

        if (currentLevel <= 10)
        {
            xpToNextLevel += earlyLevelXpBonus;
        }
        else if (currentLevel <= 25)
        {
            xpToNextLevel *= midGameScalingFactor;
        }
        else if (currentLevel <= 35)
        {
            xpToNextLevel *= lateGameScalingFactor;
        }
        else
        {
            xpToNextLevel *= endGameScalingFactor;
        }

        xpToNextLevel = Mathf.FloorToInt(xpToNextLevel);

        if (upgradeManager != null)
        {
            upgradeManager.AddLevelUpToQueue();
        }
    }

    private void UpdateUI()
    {
        if(xpSlider == null || levelText == null) return;
        levelText.text = $"Lvl: {currentLevel}";
        xpSlider.maxValue = xpToNextLevel;
        xpSlider.value = currentXP;
    }
}