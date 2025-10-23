using UnityEngine;
using TMPro; // Make sure to import TextMeshPro for UI elements
using UnityEngine.UI;

/// <summary>
/// Manages the end game UI panel, displaying final stats like time survived
/// and damage dealt to the final boss ("Reaper").
/// </summary>
public class EndGamePanel : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("The TextMeshPro UI element to display the total time the player survived.")]
    [SerializeField] private TextMeshProUGUI timeLastedText;

    [Tooltip("The TextMeshPro UI element to display the total damage dealt to the Reaper.")]
    [SerializeField] private TextMeshProUGUI reaperDamageText;

    [Tooltip("The InputField for the player to enter their name.")]
    [SerializeField] private TMP_InputField usernameInput;

    [Tooltip("The button to save the score.")]
    [SerializeField] private Button saveButton;

    private db database;
    private float damageDone;
    private float timeLasted;

    void Start()
    {
        database = FindFirstObjectByType<db>();
        if (database == null)
        {
            Debug.LogError("db script not found in the scene!");
        }

        saveButton.onClick.AddListener(SaveScore);
    }

    /// <summary>
    /// This function is called when the GameObject becomes enabled and active.
    /// It automatically updates the UI with the final game stats.
    /// </summary>
    void OnEnable()
    {
        UpdateEndGameStats();
    }

    public void SaveScore()
    {
        Debug.Log("Attempting to save score...");
        string username = usernameInput.text;
        if (string.IsNullOrEmpty(username))
        {
            Debug.LogError("Username is empty!");
            return;
        }

        if (database != null)
        {
            database.WriteNewScore(username, (int)damageDone);
            Debug.Log("Score saved!");
            saveButton.interactable = false; // Disable button after saving
        }
    }

    /// <summary>
    /// Finds the necessary game objects and components to calculate and display the final stats.
    /// </summary>
    public void UpdateEndGameStats()
    {
        // --- Calculate and Display Time Survived ---
        // Access the GameManager singleton to get the game timer values.
        if (GameManager.Instance != null)
        {
            float totalTime = GameManager.Instance.totalGameTime;
            float remainingTime = GameManager.Instance.GetRemainingTime();
            timeLasted = totalTime - remainingTime;

            // Format the time from seconds into a more readable MM:SS format.
            int minutes = Mathf.FloorToInt(timeLasted / 60);
            int seconds = Mathf.FloorToInt(timeLasted % 60);
            timeLastedText.text = $"Time Survived: {minutes:00}:{seconds:00}";
        }
        else
        {
            // Display an error if the GameManager can't be found.
            timeLastedText.text = "Time Survived: Error";
            Debug.LogError("EndGamePanel could not find the GameManager.Instance!");
        }


        // --- Calculate and Display Damage to Reaper ---
        // Find the Reaper enemy in the scene by its tag.
        GameObject reaperObject = GameObject.FindGameObjectWithTag("Reaper");

        if (reaperObject != null)
        {
            // Get the EnemyStats component from the Reaper GameObject.
            EnemyStats reaperStats = reaperObject.GetComponent<EnemyStats>();

            if (reaperStats != null)
            {
                // --- CORRECTED LOGIC ---
                // We now read the MaxHealth value that was stored in EnemyStats when the Reaper first spawned.
                // This is 100% accurate, regardless of difficulty changes during the fight.
                float reaperMaxHealth = reaperStats.MaxHealth;
                float reaperCurrentHealth = reaperStats.CurrentHealth;

                // Damage done is the difference between its starting health and its current health.
                damageDone = reaperMaxHealth - reaperCurrentHealth;

                // Display the calculated damage, formatted as a whole number.
                reaperDamageText.text = $"Damage to Reaper: {damageDone:F0}";
            }
            else
            {
                // Handle cases where the Reaper exists but lacks the necessary stats component.
                reaperDamageText.text = "Damage to Reaper: Stats not found.";
                Debug.LogError("Found 'Reaper' object, but it is missing the EnemyStats component!");
            }
        }
        else
        {
            // Display a message if the Reaper enemy wasn't found in the scene.
            reaperDamageText.text = "Damage to Reaper: N/A";
            Debug.LogWarning("EndGamePanel could not find a GameObject with the tag 'Reaper'. Make sure the tag is set correctly on your Reaper prefab/instance.");
        }
    }
}