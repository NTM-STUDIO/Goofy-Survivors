using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Threading.Tasks;

public class EndGamePanel : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI timeLastedText;
    [SerializeField] private TextMeshProUGUI reaperDamageText;
    [SerializeField] private TMP_InputField usernameInput;
    [SerializeField] private Button saveButton;
    [SerializeField] private TextMeshProUGUI buttonText; // Reference to button text

    private db database;
    private GameManager gameManager;
    
    private float damageDone;
    private float timeLasted;
    private string userId;
    private bool isExistingUser = false;

    private void Awake()
    {
        database = db.Instance;
        gameManager = GameManager.Instance;
        
        if (database == null || gameManager == null)
        {
            Debug.LogError("FATAL ERROR: EndGamePanel could not find db.Instance or GameManager.Instance!");
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
        if (userSnapshot.Exists)
        {
            var userDict = (IDictionary<string, object>)userSnapshot.Value;
            int existingDamage = System.Convert.ToInt32(userDict["damage"]);

            // Only update damage if current score is better
            if (damageDone > existingDamage)
            {
                database.NewGoofer(userId, PlayerPrefs.GetString("PlayerUsername"), (int)damageDone);
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
            database.NewGoofer(userId, PlayerPrefs.GetString("PlayerUsername"), (int)damageDone);
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
        database.NewGoofer(userId, username, scoreToSave);
        
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
    }
}