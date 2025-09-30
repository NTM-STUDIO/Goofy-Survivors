using UnityEngine;
using UnityEngine.Events;

// Attach this script to your Player GameObject.
public class PlayerExperience : MonoBehaviour
{
    public int currentLevel = 1;
    public float currentXP = 0;
    public float xpToNextLevel = 100;
    public float xpIncreaseFactor = 1.2f; // How much harder the next level is


    public UnityEvent<int> OnLevelUp;

    public void AddXP(float xp)
    {
        currentXP += xp;
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
        
        Debug.Log($"Leveled up to Level {currentLevel}!");
        OnLevelUp.Invoke(currentLevel);
    }
}