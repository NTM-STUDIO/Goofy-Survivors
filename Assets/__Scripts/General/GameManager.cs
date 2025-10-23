using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Core Manager References")]
    [Tooltip("CRITICAL: Drag the UIManager object here.")]
    [SerializeField] private UIManager uiManager;
    [Tooltip("CRITICAL: Drag the EnemySpawner object here.")]
    [SerializeField] private EnemySpawner enemySpawner;
    [Tooltip("CRITICAL: Drag the EnemyDespawner object here.")]
    [SerializeField] private EnemyDespawner enemyDespawner;
    [Tooltip("CRITICAL: Drag the UpgradeManager object here.")]
    [SerializeField] private UpgradeManager upgradeManager;
    [Tooltip("CRITICAL: Drag the PlayerExperience object from your scene here.")]
    [SerializeField] private PlayerExperience playerExperience;

    [Header("Player References (Internal)")]
    private Movement player; // Found on the spawned player
    private GameObject chosenPlayerPrefab; // Set by the Unit Carousel

    [Header("Prefabs & Spawn Points")]
    [Tooltip("CRITICAL: The Transform where the player will be spawned.")]
    [SerializeField] private Transform playerSpawnPoint;
    [SerializeField] private GameObject bossPrefab;
    [SerializeField] private Transform bossSpawnPoint;

    public enum GameState { PreGame, Playing, Paused, GameOver }
    public GameState CurrentState { get; private set; }

    [Header("Timer Settings")]
    [SerializeField] private float totalGameTime = 900f;
    private float currentTime;
    private bool isTimerRunning = false;

    [Header("General Difficulty Settings")]
    public float currentEnemyHealthMultiplier { get; private set; } = 1f;
    public float currentEnemyDamageMultiplier { get; private set; } = 1f;
    [Space]
    [SerializeField] private float healthIncreasePerMinute = 5f;
    [SerializeField] private float damageIncreasePerMinute = 2f;

    [Header("Difficulty Scaling - Caster")]
    public float currentProjectileSpeed { get; private set; }
    public float currentFireRate { get; private set; }
    public float currentSightRange { get; private set; }
    [Space]
    [SerializeField] private float baseProjectileSpeed = 15f;
    [SerializeField] private float projectileSpeedIncreasePerMinute = 1.5f;
    [SerializeField] private float baseFireRate = 2f;
    [SerializeField] private float fireRateDecreasePerMinute = 0.1f;
    [SerializeField] private float baseSightRange = 999f;

    public EnemyStats reaperStats { get; private set; }
    private bool bossSpawned = false;
    private int lastMinuteMark = 0;
    private int _pauseRequesters = 0;

    void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    void Start()
    {
        CurrentState = GameState.PreGame;
        currentTime = totalGameTime;
        currentProjectileSpeed = baseProjectileSpeed;
        currentFireRate = baseFireRate;
        currentSightRange = baseSightRange;
    }

    void Update()
    {
        if (CurrentState == GameState.Playing)
        {
            UpdateTimer();
            CheckForDifficultyIncrease();
            CheckForBossSpawn();
        }
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            if (uiManager != null) uiManager.ToggleStatsPanel();
        }
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (CurrentState == GameState.Playing)
            {
                RequestPause();
                if (uiManager != null) uiManager.ShowPauseMenu(true);
            }
            else if (CurrentState == GameState.Paused)
            {
                RequestResume();
                if (uiManager != null) uiManager.ShowPauseMenu(false);
            }
        }
    }
    
    #region Game Flow
    public void SetChosenPlayerPrefab(GameObject playerPrefab)
    {
        chosenPlayerPrefab = playerPrefab;
    }

    public void StartGame()
    {
        if (CurrentState == GameState.Playing) return;

        if (chosenPlayerPrefab == null)
        {
            Debug.LogError("FATAL: StartGame was called, but no player prefab was chosen! Call SetChosenPlayerPrefab() first.", this);
            return;
        }

        GameObject playerObject = null;
        if (playerSpawnPoint != null)
        {
            playerObject = Instantiate(chosenPlayerPrefab, playerSpawnPoint.position, playerSpawnPoint.rotation);
            player = playerObject.GetComponent<Movement>();
        }
        else
        {
            Debug.LogError("FATAL: Cannot start game! The 'Player Spawn Point' is not assigned in the GameManager Inspector.", this);
            return;
        }

        if (player == null)
        {
            Debug.LogError("FATAL: Player prefab was spawned but is missing a Movement script! Aborting start.", this);
            return;
        }

        CurrentState = GameState.Playing;
        Debug.Log("Game Started! Initializing all managers...");
        
        // Initialize all managers, passing the newly created player object to those who need it.
        if (playerExperience != null) playerExperience.Initialize();
        else Debug.LogWarning("GameManager is missing reference to the PlayerExperience manager object in the scene.");

        if (upgradeManager != null) upgradeManager.Initialize(playerObject);
        else Debug.LogWarning("GameManager is missing reference to UpgradeManager.");
        
        if (enemyDespawner != null) enemyDespawner.Initialize(playerObject);
        else Debug.LogWarning("GameManager is missing reference to EnemyDespawner.");
        
        if (enemySpawner != null) enemySpawner.StartSpawning();
        else Debug.LogError("GameManager: EnemySpawner reference is not set in the Inspector!");
        
        if (bossSpawnPoint == null) bossSpawnPoint = GameObject.FindGameObjectWithTag("BossSpawn")?.transform;
        
        isTimerRunning = true;
        bossSpawned = false;
        lastMinuteMark = 0;
        _pauseRequesters = 0;
        currentEnemyHealthMultiplier = 1f;
        currentEnemyDamageMultiplier = 1f;
    }

    public void RestartGame()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
    
    public void PlayerDied()
    {
        if (CurrentState == GameState.GameOver) return;
        CurrentState = GameState.GameOver;
        isTimerRunning = false;
        if (player != null) player.enabled = false;
        if (uiManager != null)
        {
            uiManager.ShowEndGamePanel(true);
            uiManager.SetInGameHudVisibility(false);
        }
    }

    private void EndGame()
    {
        isTimerRunning = false;
        CurrentState = GameState.GameOver;
        Debug.Log("Time's up! Game Over.");
        if (uiManager != null)
        {
            uiManager.ShowEndGamePanel(true);
            uiManager.SetInGameHudVisibility(false);
        }
    }
    #endregion

    #region Pause Management
    public void RequestPause()
    {
        _pauseRequesters++;
        if (_pauseRequesters == 1 && CurrentState == GameState.Playing)
        {
            CurrentState = GameState.Paused;
            Time.timeScale = 0f;
            if (player != null) player.enabled = false;
        }
    }

    public void RequestResume()
    {
        _pauseRequesters--;
        if (_pauseRequesters < 0) _pauseRequesters = 0;
        if (_pauseRequesters == 0 && CurrentState == GameState.Paused)
        {
            CurrentState = GameState.Playing;
            Time.timeScale = 1f;
            if (player != null) player.enabled = true;
        }
    }
    #endregion

    #region Timers and Spawning
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
            if (uiManager != null) uiManager.UpdateTimerText(currentTime);
        }
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
            GameObject bossObject = Instantiate(bossPrefab, bossSpawnPoint.position + Vector3.up * 10f, bossSpawnPoint.rotation);
            reaperStats = bossObject.GetComponent<EnemyStats>();
            Debug.Log("The Final Boss has appeared!");
        }
    }
    #endregion

    #region Difficulty Scaling
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
        currentProjectileSpeed += projectileSpeedIncreasePerMinute;
        currentFireRate = Mathf.Max(0.2f, baseFireRate - (fireRateDecreasePerMinute * lastMinuteMark));
    }
    #endregion
    
    #region Getters and Setters
    public float GetRemainingTime() { return currentTime; }
    public float GetTotalGameTime() { return totalGameTime; }

    public void SetGameDuration(float newDuration)
    {
        totalGameTime = newDuration;
        currentTime = newDuration;
    }
    #endregion
}