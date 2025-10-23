using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerExperience : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("CRITICAL: Drag the XP Slider UI element here.")]
    [SerializeField] private Slider xpSlider;
    [Tooltip("CRITICAL: Drag the Level Text UI element here.")]
    [SerializeField] private TMP_Text levelText;

    [Header("System References")]
    [Tooltip("CRITICAL: Drag the UpgradeManager object here.")]
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

    // This script now has no references to set in Start() or Awake(). It waits.
    void Start()
    {
    }

    /// <summary>
    /// The GameManager calls this and GIVES it the spawned player.
    /// </summary>
    public void Initialize()
    {
        if (upgradeManager == null || xpSlider == null || levelText == null)
        {
            Debug.LogError("FATAL ERROR on PlayerExperience: One or more CRITICAL REFERENCES are NOT ASSIGNED in the Inspector! Leveling up will fail.", this);
            enabled = false;
            return;
        }
        
        UpdateUI();
        Debug.Log("PlayerExperience Manager Initialized successfully.");
    }

    public void AddXP(float xp)
    {
        if (enabled == false) return;
        currentXP += xp;
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