
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TMPro;
public class PlayerExperience : MonoBehaviour
{
    public Slider xpSlider;

    public TMP_Text levelText;
    public int currentLevel = 1;
    public float currentXP = 0;
    public float xpToNextLevel = 100;
    public float xpIncreaseFactor = 1.2f; // How much harder the next level is

    public void Start()
    {
        xpSlider.maxValue = xpToNextLevel;
        xpSlider.value = currentXP;
        levelText.text = $"Lvl: {currentLevel}";
    }
    public void AddXP(float xp)
    {
        currentXP += xp;
        xpSlider.maxValue = xpToNextLevel;
        xpSlider.value = currentXP;
        while (currentXP >= xpToNextLevel)
        {
            LevelUp();
        }
    }

    private void LevelUp()
    {
        currentLevel++;
        currentXP -= xpToNextLevel;
        xpToNextLevel *= xpIncreaseFactor;
        xpSlider.maxValue = xpToNextLevel;
        xpSlider.value = currentXP;
        levelText.text = $"Lvl: {currentLevel}";

    }
}