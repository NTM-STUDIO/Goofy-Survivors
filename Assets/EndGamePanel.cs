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
    [SerializeField] private TextMeshProUGUI saveStatusText; // Optional: feedback message (e.g., "Score saved!")
    
    [Header("Ability Stats Display")]
    [Tooltip("ScrollView onde as estatísticas das habilidades serão exibidas")]
    [SerializeField] private ScrollRect abilityStatsScrollView;
    [Tooltip("Prefab do RowAbilityDmgStat para mostrar estatísticas de cada habilidade")]
    [SerializeField] private GameObject abilityStatRowPrefab;
    [Tooltip("Transform do Content dentro da ScrollView onde as rows serão instanciadas (Opcional - será detectado automaticamente da ScrollView se não atribuído)")]
    [SerializeField] private Transform abilityStatsContainer;
    [Tooltip("Registro de armas para obter os ícones das habilidades")]
    [SerializeField] private WeaponRegistry weaponRegistry;
    
    [Header("Flow Buttons")]
    [SerializeField] private Button playAgainButton; // Will return to lobby instead of replaying immediately

    private db database;
    private GameManager gameManager;
    
    private float damageDone;
    private float timeLasted;
    private string userId;
    private bool isExistingUser = false;
    private IReadOnlyDictionary<string, float> abilityDamageTotals;
    private readonly List<GameObject> spawnedRows = new List<GameObject>();

    private void Awake()
    {
        // Initialize database for THIS client (works in both SP and MP)
        database = FindFirstObjectByType<db>();
        if (database == null)
        {
            // Create a local DB instance for this client
            database = gameObject.AddComponent<db>();
            Debug.Log("[EndGamePanel] Created local database instance for this client");
        }
        else
        {
            Debug.Log("[EndGamePanel] Using existing database instance");
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
        // Clear old visual rows but capture current run stats first
        ClearAbilityStats();
        
        // Clear previous save status message
        if (saveStatusText != null)
        {
            saveStatusText.gameObject.SetActive(false);
        }
        
        UpdateEndGameStats();
        
        Debug.Log($"[EndGamePanel] OnEnable - Captured stats: damageDone={damageDone:F0}, abilityCount={abilityDamageTotals?.Count ?? 0}");

        // Auto-save score for ALL users (both existing and new)
        if (database != null)
        {
            await AutoSaveScore();
        }
        else
        {
            Debug.LogWarning("[EndGamePanel] Database not available, auto-save skipped");
        }
    }

    void OnDisable()
    {
        // Clear local cache when panel is hidden (safe to do now since save is complete)
        Debug.Log("[EndGamePanel] OnDisable - Clearing local stat cache");
        abilityDamageTotals = null;
        damageDone = 0f;
        timeLasted = 0f;
    }

    private async Task AutoSaveScore()
    {
        if (database == null)
        {
            Debug.LogWarning("[EndGamePanel] Database not initialized. Cannot auto-save score.");
            return;
        }

        // NOTE: We allow saving with 0 damage to register the player ID in the database
        Debug.Log($"[EndGamePanel] AutoSave initiated - damageDone={damageDone:F0}");

        // Generate userId if this is a new user
        if (string.IsNullOrEmpty(userId))
        {
            userId = System.Guid.NewGuid().ToString();
            PlayerPrefs.SetString("PlayerId", userId);
            isExistingUser = true;
            
            // Set default username if empty
            if (string.IsNullOrEmpty(usernameInput.text))
            {
                usernameInput.text = $"Goofer{UnityEngine.Random.Range(1000, 9999)}";
            }
            
            PlayerPrefs.SetString("PlayerUsername", usernameInput.text);
            PlayerPrefs.Save();
            
            Debug.Log($"[EndGamePanel] Created new userId for auto-save: {userId}, username: {usernameInput.text}");
        }
        
        try
        {
            var userSnapshot = await database.GetUserAsync(userId);
            var totalsToPersist = abilityDamageTotals ?? AbilityDamageTracker.GetTotalsSnapshot();
            
            Debug.Log($"[EndGamePanel] AutoSave: damageDone={damageDone:F0}, abilities={totalsToPersist?.Count ?? 0}");
            
            if (userSnapshot.Exists)
        {
            var userDict = (IDictionary<string, object>)userSnapshot.Value;
            int existingDamage = System.Convert.ToInt32(userDict["damage"]);

            // Update if current score is better OR equal (to update abilities data)
            if (damageDone >= existingDamage)
            {
                database.NewGoofer(userId, PlayerPrefs.GetString("PlayerUsername"), (int)damageDone, totalsToPersist);
                
                if (damageDone > existingDamage)
                {
                    Debug.Log($"✅ Updated score for {PlayerPrefs.GetString("PlayerUsername")}: {existingDamage} → {(int)damageDone}");
                    
                    // Show feedback to user
                    if (saveStatusText != null)
                    {
                        saveStatusText.text = $"New high score saved! ({(int)damageDone} damage)";
                        saveStatusText.gameObject.SetActive(true);
                    }
                }
                else
                {
                    Debug.Log($"📊 Updated abilities data for {PlayerPrefs.GetString("PlayerUsername")}: {(int)damageDone} damage");
                    
                    // Show feedback to user
                    if (saveStatusText != null)
                    {
                        saveStatusText.text = $"Stats updated: {(int)damageDone} damage";
                        saveStatusText.gameObject.SetActive(true);
                    }
                }
            }
            else
            {
                Debug.Log($"⏩ Kept existing score for {PlayerPrefs.GetString("PlayerUsername")}: {existingDamage} (current: {(int)damageDone})");
                
                // Show feedback to user
                if (saveStatusText != null)
                {
                    saveStatusText.text = $"Previous best: {existingDamage} damage";
                    saveStatusText.gameObject.SetActive(true);
                }
            }
        }
        else
        {
            // First time saving
            database.NewGoofer(userId, PlayerPrefs.GetString("PlayerUsername"), (int)damageDone, totalsToPersist);
            Debug.Log($"🆕 Created new entry for {PlayerPrefs.GetString("PlayerUsername")}: {(int)damageDone}");
            
            // Show feedback to user
            if (saveStatusText != null)
            {
                saveStatusText.text = $"Score saved! Welcome, {PlayerPrefs.GetString("PlayerUsername")}!";
                saveStatusText.gameObject.SetActive(true);
            }
        }
        
        // Update button text to reflect that user now exists
        if (buttonText != null)
        {
            buttonText.text = "Update Username";
        }
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[EndGamePanel] Failed to auto-save score: {ex.Message}\n{ex.StackTrace}");
        }
    }

    public async void SaveScore()
    {
        if (database == null)
        {
            Debug.LogWarning("[EndGamePanel] Database not initialized. Cannot save score.");
            return;
        }
        
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
        
        try
        {
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
            
            // Show success feedback with clear indication of what happened
            if (saveStatusText != null)
            {
                if (!string.IsNullOrEmpty(oldUsername) && oldUsername != username)
                {
                    saveStatusText.text = $"Username updated to '{username}'! (Best: {scoreToSave})";
                }
                else if (isExistingUser && scoreToSave > (int)damageDone)
                {
                    // Score wasn't better, kept the old one
                    saveStatusText.text = $"High score kept: {scoreToSave} damage";
                }
                else if (scoreToSave == (int)damageDone)
                {
                    // Current score saved
                    saveStatusText.text = $"Score saved: {scoreToSave} damage";
                }
                else
                {
                    // This shouldn't happen but handle edge case
                    saveStatusText.text = $"Saved! ({scoreToSave} damage)";
                }
                saveStatusText.gameObject.SetActive(true);
            }
            
            Debug.Log($"Saved/Updated: {username} with score: {scoreToSave} (current run: {(int)damageDone})");
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[EndGamePanel] Failed to save score: {ex.Message}");
            
            // Show error feedback
            if (saveStatusText != null)
            {
                saveStatusText.text = "Failed to save score!";
                saveStatusText.gameObject.SetActive(true);
            }
        }
    }

    public void UpdateEndGameStats()
    {
        // Clear any lingering stats from previous runs
        ClearAbilityStats();
        
        // --- Calculate and Display Time Survived (THE CLEAN WAY) ---
        // --- THIS IS THE FINAL FIX ---
        float totalTime = gameManager.GetTotalGameTime();
        float remainingTime = gameManager.GetRemainingTime();
        timeLasted = totalTime - remainingTime;

        int minutes = Mathf.FloorToInt(timeLasted / 60);
        int seconds = Mathf.FloorToInt(timeLasted % 60);
        timeLastedText.text = $"Time Survived: {minutes:00}:{seconds:00}";

        // --- Calculate and Display Damage to Reaper (Individual per player) ---
        // Get local player's Reaper damage (properly tracked per-player now)
        PlayerStats localPlayer = GetLocalPlayerStats();
        if (localPlayer != null)
        {
            damageDone = localPlayer.totalReaperDamageDealt;
            Debug.Log($"[EndGamePanel] UpdateEndGameStats - Player Reaper damage: {damageDone:F0}");
            
            // Obter estatísticas de habilidade apenas deste jogador
            abilityDamageTotals = AbilityDamageTracker.GetPlayerTotalsSnapshot(localPlayer);
            Debug.Log($"[EndGamePanel] Captured {abilityDamageTotals?.Count ?? 0} ability stats for local player");
        }
        else
        {
            // Fallback to old system (combined Reaper damage) if player not found
            damageDone = gameManager.GetReaperDamage();
            Debug.LogWarning($"[EndGamePanel] Could not find local PlayerStats, using combined Reaper damage fallback: {damageDone:F0}");
            
            // Fallback para estatísticas combinadas
            abilityDamageTotals = AbilityDamageTracker.GetTotalsSnapshot();
        }
        
        if (damageDone > 0)
        {
            reaperDamageText.text = $"Damage to Reaper: {damageDone:F0}";
        }
        else
        {
            // Allow 0 damage - player still participated and should be registered
            reaperDamageText.text = $"Damage to Reaper: {damageDone:F0}";
        }
        
        // Popula as estatísticas das habilidades na ScrollView
        PopulateAbilityStats();
    }

    /// <summary>
    /// Obtém o PlayerStats do jogador local (owner em multiplayer ou único em single-player)
    /// </summary>
    private PlayerStats GetLocalPlayerStats()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            // Multiplayer: procura pelo jogador que é owner local
            var allPlayers = FindObjectsByType<PlayerStats>(FindObjectsSortMode.None);
            foreach (var player in allPlayers)
            {
                var netObj = player.GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsOwner)
                {
                    return player;
                }
            }
        }
        else
        {
            // Single-player: retorna o primeiro PlayerStats encontrado
            return FindFirstObjectByType<PlayerStats>();
        }
        return null;
    }

    /// <summary>
    /// Limpa as rows antigas e cria novas rows com as estatísticas de cada habilidade
    /// </summary>
    private void PopulateAbilityStats()
    {

        // Limpa rows anteriores
        ClearAbilityStats();

        // Debug: Mostra o estado atual das referências

        // Se não tiver container atribuído, tenta obter automaticamente da ScrollView
        if (abilityStatsContainer == null && abilityStatsScrollView != null)
        {
            abilityStatsContainer = abilityStatsScrollView.content;

            // Verifica se a ScrollView tem content válido
            if (abilityStatsContainer == null)
            {
                return;
            }
        }

        // Verifica se temos as referências necessárias
        if (abilityStatRowPrefab == null)
        {
            return;
        }

        // Verifica se o prefab tem o componente AbilityStatRow
        if (abilityStatRowPrefab.GetComponent<AbilityStatRow>() == null)
        {
            return;
        }

        if (abilityStatsScrollView == null && abilityStatsContainer == null)
        {
            return;
        }

        if (abilityStatsContainer == null)
        {
            return;
        }

        // Verifica se o container tem os componentes necessários
        if (abilityStatsContainer.GetComponent<VerticalLayoutGroup>() == null)
        {
        }
        else
        {
        }

        if (abilityDamageTotals == null)
        {
            Debug.Log("[EndGamePanel] abilityDamageTotals é null, tentando obter...");
            // Se ainda não capturamos, obtemos as stats do jogador local
            PlayerStats localPlayer = GetLocalPlayerStats();
            if (localPlayer != null)
            {
                Debug.Log($"[EndGamePanel] Jogador local encontrado, obtendo stats...");
                abilityDamageTotals = AbilityDamageTracker.GetPlayerTotalsSnapshot(localPlayer);
                Debug.Log($"[EndGamePanel] Stats obtidas: {abilityDamageTotals?.Count ?? 0} habilidades");
            }
            else
            {
                Debug.LogWarning("[EndGamePanel] Jogador local NÃO encontrado!");
                abilityDamageTotals = AbilityDamageTracker.GetTotalsSnapshot();
            }
        }

        if (abilityDamageTotals == null || abilityDamageTotals.Count == 0)
        {
            Debug.LogWarning($"[EndGamePanel] Nenhuma stat de habilidade para exibir! (null={abilityDamageTotals == null}, count={abilityDamageTotals?.Count ?? 0})");
            return;
        }
        
        Debug.Log($"[EndGamePanel] Populando {abilityDamageTotals.Count} habilidades na UI");


        // Ordena as habilidades por dano (maior para menor) e cria uma row para cada uma
        var sortedAbilities = new List<KeyValuePair<string, float>>(abilityDamageTotals);
        sortedAbilities.Sort((a, b) => b.Value.CompareTo(a.Value));

        foreach (var ability in sortedAbilities)
        {
            GameObject rowObj = Instantiate(abilityStatRowPrefab, abilityStatsContainer);
            spawnedRows.Add(rowObj);


            AbilityStatRow row = rowObj.GetComponent<AbilityStatRow>();
            if (row != null)
            {
                // Tenta obter o ícone da arma baseado no nome da habilidade
                Texture weaponIcon = GetWeaponIcon(ability.Key);
                
                row.SetData(ability.Key, ability.Value, weaponIcon);
            }
            else
            {
            }
        }


        // Força atualização do layout após criar as rows
        LayoutRebuilder.ForceRebuildLayoutImmediate(abilityStatsContainer as RectTransform);
    }

    /// <summary>
    /// Obtém o ícone da arma baseado no nome da habilidade
    /// </summary>
    /// <param name="abilityName">Nome da habilidade</param>
    /// <returns>Texture do ícone da arma ou null se não encontrado</returns>
    private Texture GetWeaponIcon(string abilityName)
    {
        if (weaponRegistry == null)
        {
            Debug.LogWarning("[EndGamePanel] WeaponRegistry não está atribuído! Não é possível obter ícones das armas.");
            return null;
        }

        try
        {
            // Procura a arma no registry baseado no nome da habilidade
            foreach (var weapon in weaponRegistry.allWeapons)
            {
                if (weapon != null && weapon.weaponName == abilityName)
                {
                    // Verifica se tem sprite e converte para Texture
                    if (weapon.icon != null)
                    {
                        Debug.Log($"[EndGamePanel] Ícone encontrado para {abilityName}: {weapon.icon.name}");
                        return weapon.icon.texture;
                    }
                }
            }

            Debug.LogWarning($"[EndGamePanel] Ícone não encontrado para a habilidade: {abilityName}");
            return null;
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[EndGamePanel] Erro ao buscar ícone para {abilityName}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Limpa todas as rows de estatísticas de habilidades previamente criadas
    /// </summary>
    private void ClearAbilityStats()
    {
        foreach (var row in spawnedRows)
        {
            if (row != null)
            {
                Destroy(row);
            }
        }
        spawnedRows.Clear();
    }

    /// <summary>
    /// Método público para testar a criação das rows (pode ser chamado do Inspector)
    /// </summary>
    [ContextMenu("Test Populate Ability Stats")]
    public void TestPopulateAbilityStats()
    {
        Debug.Log("[EndGamePanel] TestPopulateAbilityStats() chamado manualmente");
        PopulateAbilityStats();
    }

    private void OnPlayAgainRestart()
    {
        // Clear visual stats but DON'T reset local cache yet
        // The tracker will be reset by GameManager when the NEW game starts
        ClearAbilityStats();
        
        // NOTE: We intentionally keep abilityDamageTotals and damageDone until the panel is disabled
        // This ensures stats are available for late saves or multiplayer sync

        // Ensure the game is not paused
        try { GameManager.Instance.RequestResume(); } catch { }
        Time.timeScale = 1f;

        // Prefer calling the centralized Restart flow to keep behavior identical to the pause menu
        var settings = Object.FindFirstObjectByType<SettingsManager>(FindObjectsInactive.Include);
        if (settings != null)
        {
            Debug.Log("[EndGamePanel] Found SettingsManager, calling UI_Restart()");
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