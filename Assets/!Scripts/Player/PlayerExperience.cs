using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerExperience : MonoBehaviour
{
    [Header("UI References")]
    public Slider xpSlider;
    public TMP_Text levelText;

    [Header("Experience Settings")]
    public int currentLevel = 1;
    public float currentXP = 0;
    public float xpToNextLevel = 100;
    public float xpIncreaseFactor = 1.2f; // How much harder the next level is

    [Header("System References")]
    [SerializeField] private UpgradeManager upgradeManager;
    [SerializeField] private UIManager uiManager;

    public void Start()
    {
        //how to find component by name?
        //Find Upgrade Manager xpSlider and LevelText
        upgradeManager = FindObjectOfType<UpgradeManager>();



        uiManager = FindObjectOfType<UIManager>();
        xpSlider = uiManager.xpSlider;
        levelText = uiManager.levelText;

        xpSlider.maxValue = xpToNextLevel;
        xpSlider.value = currentXP;
        levelText.text = $"Lvl: {currentLevel}";
    }

    public void AddXP(float xp)
    {
        currentXP += xp;
        xpSlider.value = currentXP; 

        while (currentXP >= xpToNextLevel)
        {
            LevelUp();
        }
        xpSlider.maxValue = xpToNextLevel;
    }

    private void LevelUp()
    {
        currentXP -= xpToNextLevel; 
        currentLevel++;
        xpToNextLevel *= xpIncreaseFactor;
        levelText.text = $"Lvl: {currentLevel}";
        xpSlider.value = currentXP;
        xpSlider.maxValue = xpToNextLevel;


        if (upgradeManager != null)
        {
            upgradeManager.TriggerLevelUp();
        }
    }
}