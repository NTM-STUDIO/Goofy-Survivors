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

    private db database;
    private GameManager gameManager;
    
    private float damageDone;
    private float timeLasted;
    private string userId;

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
            usernameInput.interactable = false;
            saveButton.gameObject.SetActive(false);
        }
        else
        {
            userId = null;
            usernameInput.interactable = true;
            saveButton.gameObject.SetActive(true);
            saveButton.onClick.AddListener(SaveScore);
        }
    }

    async void OnEnable()
    {
        UpdateEndGameStats();

        if (!string.IsNullOrEmpty(userId))
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
            int existingScore = System.Convert.ToInt32(userDict["score"]);

            if (timeLasted > existingScore)
            {
                database.NewGoofer(userId, PlayerPrefs.GetString("PlayerUsername"), (int)timeLasted, (int)damageDone);
            }
        }
        else
        {
            database.NewGoofer(userId, PlayerPrefs.GetString("PlayerUsername"), (int)timeLasted, (int)damageDone);
        }
    }

    public void SaveScore()
    {
        string username = usernameInput.text;
        if (string.IsNullOrEmpty(username)) return;

        if (string.IsNullOrEmpty(userId))
        {
            userId = System.Guid.NewGuid().ToString();
            PlayerPrefs.SetString("PlayerId", userId);
            PlayerPrefs.SetString("PlayerUsername", username);
            PlayerPrefs.Save();
            
            database.NewGoofer(userId, username, (int)timeLasted, (int)damageDone);
            
            saveButton.gameObject.SetActive(false);
            usernameInput.interactable = false;
        }
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