using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Core Manager References")]
    [SerializeField] private UIManager uiManager;
    [SerializeField] private EnemySpawner enemySpawner;
    [SerializeField] private EnemyDespawner enemyDespawner;
    [SerializeField] private UpgradeManager upgradeManager;
    [SerializeField] private PlayerExperience playerExperience;

    [Header("Player References (Internal)")]
    private Movement player;
    private GameObject chosenPlayerPrefab;

    [Header("Prefabs & Spawn Points")]
    [SerializeField] private Transform playerSpawnPoint;
    [SerializeField] private GameObject bossPrefab;
    [SerializeField] private Transform bossSpawnPoint;

    public enum GameState { PreGame, Playing, Paused, GameOver }
    public GameState CurrentState { get; private set; }

    [Header("Timer Settings")]
    [SerializeField] private float totalGameTime = 900f;
    private float currentTime;
    private bool isTimerRunning = false;
    private float timerUIAccumulator = 0f; // prevents UI update every frame

    [Header("General Difficulty Settings")]
    public float currentEnemyHealthMultiplier { get; private set; } = 1f;
    public float currentEnemyDamageMultiplier { get; private set; } = 1f;
    [SerializeField] private float difficultyIncreaseInterval = 30f;
    [SerializeField] private float generalStrengthMultiplier = 1.1f;

    [Header("Difficulty Scaling - Caster")]
    public float currentProjectileSpeed { get; private set; }
    public float currentFireRate { get; private set; }
    public float currentSightRange { get; private set; }
    [SerializeField] private float baseProjectileSpeed = 10f;
    [SerializeField] private float baseFireRate = 2f;
    [SerializeField] private float baseSightRange = 999f;
    [SerializeField] private float speedMultiplier = 1.05f;
    [SerializeField] private float fireRateMultiplier = 1.05f;

    public EnemyStats reaperStats { get; private set; }
    private bool bossSpawned = false;
    private int lastDifficultyIncreaseMark = 0;
    private int _pauseRequesters = 0;

    void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    void Start()
    {
        if (uiManager == null)
            uiManager = FindObjectOfType<UIManager>();

        CurrentState = GameState.PreGame;
        currentTime = totalGameTime;
    }

    void Update()
    {
        if (CurrentState == GameState.Playing)
        {
            UpdateTimer();
            CheckForDifficultyIncrease();
            CheckForBossSpawn();
        }

        HandleInput();
    }

    private void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
            uiManager?.ToggleStatsPanel();

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (CurrentState == GameState.Playing)
                RequestPause(true);
            else if (CurrentState == GameState.Paused)
                RequestResume();
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
            Debug.LogError("StartGame called but no player prefab chosen! Call SetChosenPlayerPrefab() first.", this);
            return;
        }

        if (playerSpawnPoint == null)
        {
            Debug.LogError("Cannot start game: Player Spawn Point not assigned in GameManager.", this);
            return;
        }

        // --- Player spawn ---
        GameObject playerObject = Instantiate(chosenPlayerPrefab, playerSpawnPoint.position, playerSpawnPoint.rotation);
        player = playerObject.GetComponent<Movement>();

        if (player == null)
        {
            Debug.LogError("Spawned player prefab missing Movement component.", this);
            return;
        }

        // --- Initialize all managers ---
        playerExperience?.Initialize(playerObject);
        upgradeManager?.Initialize(playerObject);
        enemyDespawner?.Initialize(playerObject);
        enemySpawner?.StartSpawning();

        if (bossSpawnPoint == null)
            bossSpawnPoint = GameObject.FindGameObjectWithTag("BossSpawn")?.transform;

        CurrentState = GameState.Playing;
        isTimerRunning = true;
        bossSpawned = false;
        _pauseRequesters = 0;
        lastDifficultyIncreaseMark = 0;

        // Reset difficulty
        currentEnemyHealthMultiplier = 1f;
        currentEnemyDamageMultiplier = 1f;
        currentProjectileSpeed = baseProjectileSpeed;
        currentFireRate = baseFireRate;
        currentSightRange = baseSightRange;

        Debug.Log("Game Started! All systems initialized.");
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

        if (player != null)
            player.enabled = false;

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
    public void RequestPause(bool showMenu = false)
    {
        if (CurrentState == GameState.Paused) return;

        CurrentState = GameState.Paused;
        Time.timeScale = 0f;
        if (player != null) player.enabled = false;
        _pauseRequesters = 1;

        if (showMenu)
            StartCoroutine(ShowPauseMenuNextFrame());
    }

    private IEnumerator ShowPauseMenuNextFrame()
    {
        yield return null;
        uiManager?.ShowPauseMenu(true);
    }

    public void RequestResume()
    {
        if (CurrentState != GameState.Paused) return;

        CurrentState = GameState.Playing;
        Time.timeScale = 1f;
        if (player != null) player.enabled = true;
        _pauseRequesters = 0;

        uiManager?.ShowPauseMenu(false);
    }
    #endregion

    #region Timers and Spawning
    private void UpdateTimer()
    {
        if (!isTimerRunning) return;

        currentTime -= Time.deltaTime;
        timerUIAccumulator += Time.deltaTime;

        // Only update the UI every 1 second to reduce text mesh updates
        if (timerUIAccumulator >= 1f)
        {
            timerUIAccumulator = 0f;
            uiManager?.UpdateTimerText(currentTime);
        }

        if (currentTime <= 0)
        {
            currentTime = 0;
            EndGame();
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
        if (bossPrefab == null || bossSpawnPoint == null) return;

        GameObject bossObject = Instantiate(bossPrefab, bossSpawnPoint.position + Vector3.up * 10f, bossSpawnPoint.rotation);
        reaperStats = bossObject.GetComponent<EnemyStats>();
        Debug.Log("The Final Boss has appeared!");
    }
    #endregion

    #region Difficulty Scaling
    private void CheckForDifficultyIncrease()
    {
        int currentInterval = Mathf.FloorToInt((totalGameTime - currentTime) / difficultyIncreaseInterval);

        if (currentInterval > lastDifficultyIncreaseMark)
        {
            lastDifficultyIncreaseMark = currentInterval;
            IncreaseDifficulty();
        }
    }

    private void IncreaseDifficulty()
    {
        currentEnemyHealthMultiplier *= generalStrengthMultiplier;
        currentEnemyDamageMultiplier *= generalStrengthMultiplier;
        currentProjectileSpeed *= speedMultiplier;

        currentFireRate /= fireRateMultiplier;
        currentFireRate = Mathf.Max(0.2f, currentFireRate);

        Debug.Log($"Difficulty Increased (x{lastDifficultyIncreaseMark}) | HP×{currentEnemyHealthMultiplier:F2}, DMG×{currentEnemyDamageMultiplier:F2}");
    }
    #endregion

    #region Getters / Setters
    public float GetRemainingTime() => currentTime;
    public float GetTotalGameTime() => totalGameTime;

    public void SetGameDuration(float newDuration)
    {
        totalGameTime = newDuration;
        currentTime = newDuration;
    }
    #endregion
}
