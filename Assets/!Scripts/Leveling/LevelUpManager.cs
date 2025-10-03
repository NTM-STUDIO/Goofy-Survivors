using UnityEngine;
using UnityEngine.UI; // If you want to update UI text for XP/Level

public class LevelUpManager : MonoBehaviour
{
    public static LevelUpManager Instance { get; private set; }

    [Header("XP & Leveling")]
    public int currentXP = 0;
    public int xpToNextLevel = 100;
    public int currentLevel = 1;

    [Header("Difficulty")]
    public float currentDifficulty = 1f;
    public float difficultyIncreasePer100XP = 1.4f;
    private int xpMilestone = 0; // Tracks the last 100xp mark

    [Header("UI")]
    public GameObject levelUpUIPrefab; // Drag your UI prefab here
    public Transform canvasTransform; // Drag your main UI Canvas here

    public Slider xpSlider; // Optional: Slider to show XP progress


    void Awake()
    {
        // Singleton Pattern: Ensures there's only one instance of this manager.
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }
    }

        void Start()
        {
            // Initialize the XP milestone
            xpMilestone = 0;
    
            // Optional: Initialize the XP slider
            if (xpSlider != null)
            {
                xpSlider.maxValue = xpToNextLevel;
                xpSlider.value = currentXP;
            }
        }

    public void AddXP(int amount)
    {
        currentXP += amount;

        // Check for difficulty increase
        while (currentXP >= xpMilestone + 100)
        {
            xpMilestone += 100;
            currentDifficulty += difficultyIncreasePer100XP;
            Debug.Log("Difficulty Increased! New Difficulty: " + currentDifficulty);
        }

        // Check for level up
        if (currentXP >= xpToNextLevel)
        {
            LevelUp();
        }

        xpSlider.value = currentXP;
        // Update UI text here if you have any
    }

    private void LevelUp()
    {
        // Carry over excess XP
        currentXP -= xpToNextLevel;
        currentLevel++;
        

        // Increase the XP required for the next level (e.g., by 20%)
        xpToNextLevel = Mathf.RoundToInt(xpToNextLevel * 1.2f);
        xpSlider.maxValue = xpToNextLevel;
        xpSlider.value = currentXP;

        Debug.Log("LEVEL UP! Now Level: " + currentLevel);

        // Pause the game and show the level up UI
        Time.timeScale = 0f;
        Instantiate(levelUpUIPrefab, canvasTransform);
    }

    // Call this method from your UI button when an upgrade is chosen
    public void ResumeGame()
    {
        Time.timeScale = 1f;
    }
}