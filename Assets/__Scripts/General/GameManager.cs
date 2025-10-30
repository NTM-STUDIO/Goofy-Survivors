using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

<<<<<<< Updated upstream
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public UIManager uiManager;

    public enum GameState { Playing, Paused, GameOver }
    public GameState currentState;

    [Header("Timer Settings")]
    public float totalGameTime = 900f; // 15 minutos em segundos
    private float currentTime;
    private bool isTimerRunning = false;
=======
public class GameManager : NetworkBehaviour // Must be a NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Mode Settings")]
    public bool isP2P = false;

    [Header("Core Manager References")]
    [SerializeField] private UIManager uiManager;
    [SerializeField] private EnemySpawner enemySpawner;
    [SerializeField] private EnemyDespawner enemyDespawner;
    [SerializeField] private UpgradeManager upgradeManager;
    [SerializeField] private PlayerExperience playerExperience;

    private Movement localPlayer;
    private GameObject chosenPlayerPrefab;

    [Header("Prefabs & Spawn Points")]
    [SerializeField] private Transform playerSpawnPoint;
    [SerializeField] private GameObject bossPrefab;
    [SerializeField] private Transform bossSpawnPoint;

    private Dictionary<ulong, GameObject> playerUnitSelections = new Dictionary<ulong, GameObject>();

    public enum GameState { PreGame, Playing, Paused, GameOver }
    public GameState CurrentState { get; private set; }

    [Header("Timer Settings")]
    [SerializeField] private float totalGameTime = 900f;
    private float currentTime;
    private NetworkVariable<float> networkCurrentTime = new NetworkVariable<float>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private float timerUIAccumulator = 0f;
>>>>>>> Stashed changes

    [Header("Difficulty Settings")]
    [Tooltip("O multiplicador de vida inicial para os inimigos.")]
    public float currentEnemyHealthMultiplier = 1f;
    [Tooltip("O multiplicador de dano inicial para os inimigos.")]
    public float currentEnemyDamageMultiplier = 1f;
    
    [Space]
    [Tooltip("Quanto o multiplicador de vida aumenta a cada minuto.")]
    public float healthIncreasePerMinute = 40f; 
    [Tooltip("Quanto o multiplicador de dano aumenta a cada minuto.")]
    public float damageIncreasePerMinute = 5f; 

    private int lastMinuteMark  = 0; 

    [Header("Boss Settings")]
    public GameObject bossPrefab;
    public Transform bossSpawnPoint;
    private bool bossSpawned = false;

    [Header("Referências")]
    public Movement player; 
    public EnemySpawner enemySpawner;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }

        //Find playerMovement by player tag
        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                player = playerObject.GetComponent<Movement>();
            }
            else
            {
                Debug.LogError("GameManager Error: Player with tag 'Player' not found! Make sure your player is tagged correctly.");
            }
        }
    }

    void Start()
    {
        StartGame();
    }

    #region Starting Weapon Logic
    public override void OnNetworkSpawn()
    {
<<<<<<< Updated upstream
        if (currentState == GameState.Playing)
=======
        base.OnNetworkSpawn();
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
            // Handle the host player who is already connected when the server starts
            HandleClientConnected(NetworkManager.Singleton.LocalClientId);
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        if (IsServer)
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
            }
        }
    }

    private void HandleClientConnected(ulong clientId)
    {
        Debug.Log($"[GameManager] Handling connection for new Client ID: {clientId}");
        StartCoroutine(GiveStartingWeaponAfterPlayerSpawns(clientId));
    }

    private IEnumerator GiveStartingWeaponAfterPlayerSpawns(ulong clientId)
    {
        NetworkObject playerNetworkObject = null;
        // Keep looping until the player's object is officially spawned and registered.
        while (playerNetworkObject == null)
        {
            yield return null; // Wait one frame before checking again
            if (NetworkManager.Singleton.SpawnManager != null)
            {
                playerNetworkObject = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId);
            }
        }

        Debug.Log($"[GameManager] Found player object for Client ID: {clientId}. Getting PlayerWeaponManager.");

        PlayerWeaponManager pwm = playerNetworkObject.GetComponent<PlayerWeaponManager>();
        if (pwm != null)
        {
            Debug.Log($"[GameManager] Calling Server_GiveStartingWeapon on player for Client ID: {clientId}.");
            pwm.Server_GiveStartingWeapon();
        }
        else
        {
            Debug.LogError($"[GameManager] FATAL: Player object for Client {clientId} is missing a PlayerWeaponManager component!");
        }
    }
    #endregion

    void Update()
    {
        // The IsOwner check is only needed for scripts on player objects.
        // For a singleton GameManager, we just need to check if we are the host/server.
        // We can leave HandleInput to run on all clients for responsiveness.
        HandleInput();

        if (isP2P && !IsServer) // Changed from IsHost to IsServer for clarity
        {
            UpdateTimerUI(networkCurrentTime.Value);
            return;
        }
        
        if (CurrentState == GameState.Playing)
>>>>>>> Stashed changes
        {
            UpdateTimer();
            CheckForDifficultyIncrease();
            CheckForBossSpawn();
        }
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            uiManager.ShowStatsPanel(!uiManager.statsPanel.activeSelf);
        }
    }

<<<<<<< Updated upstream
    public void RestartGame()
    {
        // Reload the current scene
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void StartGame()
=======
    public void SetChosenPlayerPrefab(GameObject playerPrefab)
    {
        if(isP2P) return;
        chosenPlayerPrefab = playerPrefab;
    }

    public void SetPlayerSelections_P2P(Dictionary<ulong, GameObject> selections)
    {
        if(!isP2P) return;
        playerUnitSelections = selections;
    }

    public void StartGame()
    {
        if (CurrentState == GameState.Playing) return;

        if (isP2P)
        {
            if(IsHost) StartGame_P2P_Host();
        }
        else
        {
            StartGame_SinglePlayer();
        }
    }

    private void StartGame_SinglePlayer()
    {
        if (chosenPlayerPrefab == null)
        {
            Debug.LogError("StartGame SP: Nenhum prefab de jogador foi escolhido!");
            return;
        }
        GameObject playerObject = Instantiate(chosenPlayerPrefab, playerSpawnPoint.position, playerSpawnPoint.rotation);
        InitializeAllSystems(playerObject);

        // Also give starting weapon in single-player
        PlayerWeaponManager pwm = playerObject.GetComponent<PlayerWeaponManager>();
        if (pwm != null)
        {
            // Since this is single-player, the PWM's Start() method will handle this.
            // No extra call is needed here.
        }
    }

    private void StartGame_P2P_Host()
    {
        if (playerUnitSelections.Count == 0)
        {
            Debug.LogError("StartGame P2P: Dicionário de seleções de jogadores está vazio!");
            return;
        }

        foreach(var entry in playerUnitSelections)
        {
            GameObject playerObject = Instantiate(entry.Value, playerSpawnPoint.position, playerSpawnPoint.rotation);
            playerObject.GetComponent<NetworkObject>().SpawnAsPlayerObject(entry.Key);
        }
        InitializeGameClientRpc();
    }
    
    [ClientRpc]
    private void InitializeGameClientRpc()
    {
        // This check is important because a client might receive this RPC before their player object is ready.
        if (NetworkManager.Singleton.LocalClient.PlayerObject != null)
        {
            GameObject localPlayerObject = NetworkManager.Singleton.LocalClient.PlayerObject.gameObject;
            InitializeAllSystems(localPlayerObject);
        }
    }

    private void InitializeAllSystems(GameObject playerObject)
    {
        localPlayer = playerObject.GetComponent<Movement>();
        playerExperience?.Initialize(playerObject);
        upgradeManager?.Initialize(playerObject);
        // Enemy despawner should only run on the host/server in P2P mode.
        if (!isP2P || IsHost)
        {
            enemyDespawner?.Initialize(playerObject);
        }

        if (!isP2P || IsHost)
        {
            enemySpawner?.StartSpawning();
        }
        
        if (bossSpawnPoint == null)
            bossSpawnPoint = GameObject.FindGameObjectWithTag("BossSpawn")?.transform;

        CurrentState = GameState.Playing;
        bossSpawned = false;
        lastDifficultyIncreaseMark = 0;
        
        if (!isP2P)
        {
            currentTime = totalGameTime;
        }
        else if (IsHost)
        {
            networkCurrentTime.Value = totalGameTime;
        }
        
        currentEnemyHealthMultiplier = 1f;
        currentEnemyDamageMultiplier = 1f;
        currentProjectileSpeed = baseProjectileSpeed;
        currentFireRate = baseFireRate;
        currentSightRange = baseSightRange;

        if(uiManager) uiManager.OnGameStart();
    }

    public void PlayerDied()
    {
        if (CurrentState == GameState.GameOver) return;
        
        if (isP2P)
        {
            PlayerDiedServerRpc();
        }
        else
        {
            GameOver();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void PlayerDiedServerRpc()
    {
        GameOverClientRpc();
    }

    private void GameOver()
    {
        if (CurrentState == GameState.GameOver) return;
        CurrentState = GameState.GameOver;
        if (localPlayer != null) localPlayer.enabled = false;
        
        if (uiManager != null)
        {
            uiManager.ShowEndGamePanel(true);
        }
    }

    [ClientRpc]
    private void GameOverClientRpc()
    {
        GameOver();
    }

    public void RestartGame()
    {
        if(isP2P)
        {
            // Proper shutdown logic
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.Shutdown();
            }
            if(Instance != null) Destroy(Instance.gameObject);
            SceneManager.LoadScene(0); // Assuming scene 0 is your main menu
            return;
        }
        
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
    
    public void RequestPause(bool showMenu = false)
>>>>>>> Stashed changes
    {
        currentState = GameState.Playing;
        currentTime = totalGameTime;
        isTimerRunning = true;
        bossSpawned = false;
        lastMinuteMark = 0;
        

        currentEnemyHealthMultiplier = 1f;
        currentEnemyDamageMultiplier = 1f;

        Time.timeScale = 1f;
<<<<<<< Updated upstream

        if (player != null) player.enabled = true;
        enemySpawner.StartSpawning();
    }

=======
        if (localPlayer != null) localPlayer.enabled = true;
        uiManager?.ShowPauseMenu(false);
    }
    
>>>>>>> Stashed changes
    private void UpdateTimer()
    {
        if (isTimerRunning)
        {
            currentTime -= Time.deltaTime;
            if (currentTime <= 0)
            {
                currentTime = 0;
                EndGame();
            }
            uiManager.UpdateTimerText(currentTime);
        }
    }

    private void CheckForDifficultyIncrease()
    {
        int currentMinute = Mathf.FloorToInt((totalGameTime - currentTime) / 60);
        if (currentMinute > lastMinuteMark)
        {
            lastMinuteMark = currentMinute;
            IncreaseDifficulty();
        }
    }

    private void IncreaseDifficulty()
    {
        currentEnemyHealthMultiplier += healthIncreasePerMinute;
        currentEnemyDamageMultiplier += damageIncreasePerMinute;

        Debug.Log($"DIFICULDADE AUMENTADA! Minuto {lastMinuteMark}. Multiplicador de Vida: {currentEnemyHealthMultiplier:F2}x, Multiplicador de Dano: {currentEnemyDamageMultiplier:F2}x");
    }

    private void CheckForBossSpawn()
    {
        if (!bossSpawned && currentTime <= 10.0f)
        {
            SpawnBoss();
            bossSpawned = true;
        }
    }

    private void SpawnBoss()
    {
        if (bossPrefab != null && bossSpawnPoint != null)
        {
<<<<<<< Updated upstream
            Instantiate(bossPrefab, bossSpawnPoint.position + Vector3.up * 3.5f, bossSpawnPoint.rotation);
            Debug.Log("🔥 O Chefe Final apareceu!");
=======
            bossObject.GetComponent<NetworkObject>().Spawn(true);
        }
        
        reaperStats = bossObject.GetComponent<EnemyStats>();
        Debug.Log("O Boss Final apareceu!");
    }
    
    private void CheckForDifficultyIncrease()
    {
        float relevantTime = isP2P ? networkCurrentTime.Value : currentTime;
        int currentInterval = Mathf.FloorToInt((totalGameTime - relevantTime) / difficultyIncreaseInterval);

        if (currentInterval > lastDifficultyIncreaseMark)
        {
            lastDifficultyIncreaseMark = currentInterval;
            IncreaseDifficulty();
>>>>>>> Stashed changes
        }
    }

    public void TogglePause()
    {
<<<<<<< Updated upstream
        if (currentState == GameState.Playing)
        {
            currentState = GameState.Paused;
            Time.timeScale = 0f;
            if (player != null) player.enabled = false;
            uiManager.ShowPauseMenu(true);
        }
        else if (currentState == GameState.Paused)
        {
            currentState = GameState.Playing;
            Time.timeScale = 1f;
            Time.timeScale = 1f;
            if (player != null) player.enabled = true;
            uiManager.ShowPauseMenu(false);
        }
=======
        currentEnemyHealthMultiplier *= generalStrengthMultiplier;
        currentEnemyDamageMultiplier *= generalStrengthMultiplier;
        currentProjectileSpeed *= speedMultiplier;
        currentFireRate = Mathf.Max(0.2f, currentFireRate / fireRateMultiplier);
        Debug.Log($"Dificuldade Aumentada (x{lastDifficultyIncreaseMark}) | HP×{currentEnemyHealthMultiplier:F2}, DMG×{currentEnemyDamageMultiplier:F2}");
    }
    
    public float GetRemainingTime()
    {
        return isP2P ? networkCurrentTime.Value : currentTime;
>>>>>>> Stashed changes
    }

    public void PlayerDied()
    {
        currentState = GameState.GameOver;
        isTimerRunning = false;
        if (player != null) player.enabled = false;
        // enemySpawner.StopSpawning();
        uiManager.ShowUsernameInput();
    }

    public void SubmitUsername(string username)
    {
        StartCoroutine(SubmitToDatabase(username, (int)currentTime));
    }

    private IEnumerator SubmitToDatabase(string username, int score)
    {
        Debug.Log($"📡 Submeter: Utilizador - {username}, Pontuação (tempo restante) - {score}");
        yield return new WaitForSeconds(1); // Simula delay de rede
        Debug.Log("✅ Submissão concluída (placeholder)!");
    }

    private void EndGame()
    {
        isTimerRunning = false;
        currentState = GameState.GameOver;
        Debug.Log("🏁 O tempo acabou! Fim de jogo.");
    }
    
    public float GetRemainingTime() { return currentTime; }
    public void SetGameDuration(float newDuration)
    {
        totalGameTime = newDuration;
        currentTime = newDuration;
        bossSpawned = false;
        lastMinuteMark = 0;
    }
}