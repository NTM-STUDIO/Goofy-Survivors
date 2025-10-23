using UnityEngine;
using TMPro; // Make sure to import TextMeshPro for UI elements
using UnityEngine.UI;
using JetBrains.Annotations;
using System.Collections.Generic;
using System.Threading.Tasks;

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
    private string userId;

    private async void Awake()
    {
        database = FindFirstObjectByType<db>();
        if (database == null)
        {
            Debug.LogError("db script not found in the scene!");
        }

        // Check for saved player data
        if (PlayerPrefs.HasKey("PlayerId"))
        {
            userId = PlayerPrefs.GetString("PlayerId");
            usernameInput.text = PlayerPrefs.GetString("PlayerUsername");
            usernameInput.interactable = false;
            saveButton.gameObject.SetActive(false); // Hide save button for existing users
        }
        else
        {
            userId = null;
            usernameInput.interactable = true;
            saveButton.gameObject.SetActive(true); // Show save button for new users
            saveButton.onClick.AddListener(SaveScore);
        }
    }

    void Start()
    {
        // The original content of Start() is moved to Awake()
        // to ensure it runs before OnEnable().
    }

    /// <summary>
    /// This function is called when the GameObject becomes enabled and active.
    /// It automatically updates the UI with the final game stats and handles auto-saving.
    /// </summary>
    async void OnEnable()
    {
        UpdateEndGameStats();

        // Auto-save for existing users
        if (!string.IsNullOrEmpty(userId))
        {
            await AutoSaveScore();
        }
    }

    private async Task AutoSaveScore()
    {
        if (database != null)
        {
            var userSnapshot = await database.GetUserAsync(userId);
            if (userSnapshot.Exists)
            {
                var userDict = (IDictionary<string, object>)userSnapshot.Value;
                int existingScore = System.Convert.ToInt32(userDict["score"]);

                if (timeLasted > existingScore)
                {
                    database.NewGoofer(userId, PlayerPrefs.GetString("PlayerUsername"), (int)timeLasted, (int)damageDone);
                    Debug.Log("Score updated automatically!");
                }
                else
                {
                    Debug.Log("New score is not higher. Not updating.");
                }
            }
            else
            {
                // User in prefs but not DB, save new score
                database.NewGoofer(userId, PlayerPrefs.GetString("PlayerUsername"), (int)timeLasted, (int)damageDone);
                Debug.Log("User in prefs but not DB. New score saved!");
            }
        }
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

        // This part is now only for the first-time save
        if (string.IsNullOrEmpty(userId))
        {
            userId = System.Guid.NewGuid().ToString();
            PlayerPrefs.SetString("PlayerId", userId);
            PlayerPrefs.SetString("PlayerUsername", username);
            PlayerPrefs.Save();
            Debug.Log($"New player created. ID: {userId}, Username: {username}");

            if (database != null)
            {
                database.NewGoofer(userId, username, (int)timeLasted, (int)damageDone);
                Debug.Log("New score saved!");
            }
            saveButton.gameObject.SetActive(false); // Hide button after first save
            usernameInput.interactable = false;
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