using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using Net;

public class EndGamePanel : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI timeLastedText;
    [SerializeField] private TextMeshProUGUI reaperDamageText;
    [SerializeField] private TMP_InputField usernameInput;
    [SerializeField] private Button saveButton;
    [SerializeField] private TextMeshProUGUI buttonText; // Reference to button text
    [Header("Flow Buttons")]
    [SerializeField] private Button playAgainButton; // Will return to lobby instead of replaying immediately

    private db database;
    private GameManager gameManager;
    
    private float damageDone;
    private float timeLasted;
    private string userId;
    private bool isExistingUser = false;
    private IReadOnlyDictionary<string, float> abilityDamageTotals;

    private void Awake()
    {
        database = FindFirstObjectByType<db>();
        if (database == null)
        {
            database = gameObject.AddComponent<db>();
        }
        gameManager = GameManager.Instance;
        
        if (gameManager == null)
        {
            Debug.LogError("FATAL ERROR: EndGamePanel could not find GameManager.Instance!");
            return;
        }

        if (PlayerPrefs.HasKey("PlayerId"))
        {
            userId = PlayerPrefs.GetString("PlayerId");
            usernameInput.text = PlayerPrefs.GetString("PlayerUsername");
            isExistingUser = true;
            
            // Allow username editing for existing users
            usernameInput.interactable = true;
            saveButton.gameObject.SetActive(true);
            
            // Update button text if reference exists
            if (buttonText != null)
            {
                buttonText.text = "Update Username";
            }
        }
        else
        {
            userId = null;
            isExistingUser = false;
            usernameInput.interactable = true;
            saveButton.gameObject.SetActive(true);
            
            // Set button text for new users
            if (buttonText != null)
            {
                buttonText.text = "Save Score";
            }
        }
        
        // Limit username input to 14 characters
        usernameInput.characterLimit = 14;

        saveButton.onClick.AddListener(SaveScore);
        if (playAgainButton != null)
        {
            playAgainButton.onClick.AddListener(OnPlayAgainRestart);
        }
    }

    async void OnEnable()
    {
        UpdateEndGameStats();

        // Auto-save score for existing users (but don't hide the update button)
        if (!string.IsNullOrEmpty(userId) && isExistingUser)
        {
            await AutoSaveScore();
        }
    }

    private async Task AutoSaveScore()
    {
        var userSnapshot = await database.GetUserAsync(userId);
        var totalsToPersist = abilityDamageTotals ?? AbilityDamageTracker.GetTotalsSnapshot();
        if (userSnapshot.Exists)
        {
            var userDict = (IDictionary<string, object>)userSnapshot.Value;
            int existingDamage = System.Convert.ToInt32(userDict["damage"]);

            // Only update damage if current score is better
            if (damageDone > existingDamage)
            {
                database.NewGoofer(userId, PlayerPrefs.GetString("PlayerUsername"), (int)damageDone, totalsToPersist);
                Debug.Log($"Updated score for {PlayerPrefs.GetString("PlayerUsername")}: {(int)damageDone}");
            }
            else
            {
                Debug.Log($"Kept existing score for {PlayerPrefs.GetString("PlayerUsername")}: {existingDamage}");
            }
        }
        else
        {
            // First time saving
            database.NewGoofer(userId, PlayerPrefs.GetString("PlayerUsername"), (int)damageDone, totalsToPersist);
            Debug.Log($"Created new entry for {PlayerPrefs.GetString("PlayerUsername")}: {(int)damageDone}");
        }
    }

    public async void SaveScore()
    {
        string username = usernameInput.text;
        if (string.IsNullOrEmpty(username))
        {
            Debug.LogWarning("Username cannot be empty!");
            return;
        }

        // Generate new userId if this is the first time
        if (string.IsNullOrEmpty(userId))
        {
            userId = System.Guid.NewGuid().ToString();
            PlayerPrefs.SetString("PlayerId", userId);
            isExistingUser = true;
        }

        // Update username in PlayerPrefs (this allows name changes)
        string oldUsername = PlayerPrefs.GetString("PlayerUsername", "");
        PlayerPrefs.SetString("PlayerUsername", username);
        PlayerPrefs.Save();

        // Get existing damage score from database
        int scoreToSave = (int)damageDone;
        
        if (isExistingUser)
        {
            var userSnapshot = await database.GetUserAsync(userId);
            if (userSnapshot.Exists)
            {
                var userDict = (IDictionary<string, object>)userSnapshot.Value;
                int existingDamage = System.Convert.ToInt32(userDict["damage"]);
                
                // Keep the higher score between existing and current
                scoreToSave = Mathf.Max(existingDamage, (int)damageDone);
                
                if (!string.IsNullOrEmpty(oldUsername) && oldUsername != username)
                {
                    Debug.Log($"Username changed from '{oldUsername}' to '{username}'. Score preserved: {scoreToSave}");
                }
            }
        }

        // Save or update in database
        var totalsToPersist = abilityDamageTotals ?? AbilityDamageTracker.GetTotalsSnapshot();
        database.NewGoofer(userId, username, scoreToSave, totalsToPersist);
        
        // Update button text
        if (buttonText != null)
        {
            buttonText.text = "Update Username";
        }
        
        Debug.Log($"Saved/Updated: {username} with score: {scoreToSave}");
    }

    public void UpdateEndGameStats()
    {
        // --- Calculate and Display Time Survived (THE CLEAN WAY) ---
        // --- THIS IS THE FINAL FIX ---
        float totalTime = gameManager.GetTotalGameTime();
        float remainingTime = gameManager.GetRemainingTime();
        timeLasted = totalTime - remainingTime;

        int minutes = Mathf.FloorToInt(timeLasted / 60);
        int seconds = Mathf.FloorToInt(timeLasted % 60);
        timeLastedText.text = $"Time Survived: {minutes:00}:{seconds:00}";

        // --- Calculate and Display Damage to Reaper (THE CLEAN WAY) ---
        if (gameManager.reaperStats != null)
        {
            EnemyStats stats = gameManager.reaperStats;
            float reaperMaxHealth = stats.MaxHealth;
            float reaperCurrentHealth = stats.CurrentHealth;
            damageDone = reaperMaxHealth - reaperCurrentHealth;
            reaperDamageText.text = $"Damage to Reaper: {damageDone:F0}";
        }
        else
        {
            reaperDamageText.text = "Damage to Reaper: N/A";
        }
        abilityDamageTotals = AbilityDamageTracker.GetTotalsSnapshot();
    }

    private void OnPlayAgainRestart()
    {
        // Ensure the game is not paused
        try { GameManager.Instance.RequestResume(); } catch { }
        Time.timeScale = 1f;

        // Prefer calling the centralized Restart flow to keep behavior identical to the pause menu
        var settings = Object.FindFirstObjectByType<SettingsManager>(FindObjectsInactive.Include);
        if (settings != null)
        {
            settings.UI_Restart();
            return;
        }

        // Fallbacks if SettingsManager is not present for some reason
        try
        {
            // Try a soft singleplayer restart first
            GameManager.Instance.SoftResetSinglePlayerWorld();
            GameManager.Instance.StartGame();
            // Hide this panel to avoid overlay after restarting
            gameObject.SetActive(false);
            return;
        }
        catch { }

        // Final fallback: reload current scene (handles both SP and MP when no manager is available)
        var nm = NetworkManager.Singleton;
        var currentScene = SceneManager.GetActiveScene().name;
        var nsm = nm != null ? nm.SceneManager : null;
        if (nsm != null && nm.IsServer)
        {
            nsm.LoadScene(currentScene, LoadSceneMode.Single);
        }
        else
        {
            SceneManager.LoadScene(currentScene, LoadSceneMode.Single);
        }
    }
}