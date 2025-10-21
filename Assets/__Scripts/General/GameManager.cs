using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Core References")]
    public UIManager uiManager;
    public Movement player;
    public EnemySpawner enemySpawner;
    public EnemyDespawner enemyDespawner;

    public enum GameState { Playing, Paused, GameOver }
    public GameState currentState;

    [Header("Timer Settings")]
    public float totalGameTime = 900f; // 15 minutes in seconds
    private float currentTime;
    private bool isTimerRunning = false;

    [Header("General Difficulty Settings")]
    [Tooltip("The multiplier for enemy health.")]
    public float currentEnemyHealthMultiplier = 1f;
    [Tooltip("The multiplier for enemy damage.")]
    public float currentEnemyDamageMultiplier = 1f;

    [Space]
    [Tooltip("How much the health multiplier increases each minute.")]
    public float healthIncreasePerMinute = 5f;
    [Tooltip("How much the damage multiplier increases each minute.")]
    public float damageIncreasePerMinute = 2f;

    [Header("Difficulty Scaling - Caster")]
    [Tooltip("The base speed of caster projectiles at minute 0.")]
    public float baseProjectileSpeed = 15f;
    [Tooltip("How much faster projectiles get each minute.")]
    public float projectileSpeedIncreasePerMinute = 1.5f;
    [Tooltip("The base time between shots for casters at minute 0.")]
    public float baseFireRate = 2f;
    [Tooltip("How much the time between shots decreases each minute (faster shooting).")]
    public float fireRateDecreasePerMinute = 0.1f;
    [Tooltip("The base sight range for casters at minute 0.")]
    public float baseSightRange = 999f;
    
    // Public properties for other scripts to read the current scaled values
    public float currentProjectileSpeed { get; private set; }
    public float currentFireRate { get; private set; }
    public float currentSightRange { get; private set; }
    
    private int lastMinuteMark = 0;

    [Header("Boss Settings")]
    public GameObject bossPrefab;
    public Transform bossSpawnPoint;
    private bool bossSpawned = false;
    
    // --- Pause Management System ---
    private int _pauseRequesters = 0; // Counts how many sources are requesting a pause

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

        if (player == null)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                player = playerObject.GetComponent<Movement>();
            }
            else
            {
                Debug.LogError("GameManager Error: Player with tag 'Player' not found!");
            }
        }
    }

    void Start()
    {
        StartGame();
    }

    void Update()
    {
        if (currentState == GameState.Playing)
        {
            UpdateTimer();
            CheckForDifficultyIncrease();
            CheckForBossSpawn();
        }

        // This can still be used for a non-pausing stats panel
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            uiManager.ShowStatsPanel(!uiManager.statsPanel.activeSelf);
        }
        
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (currentState == GameState.Playing)
            {
                RequestPause();
                uiManager.ShowPauseMenu(true);
            }
            else if (currentState == GameState.Paused)
            {
                RequestResume();
                uiManager.ShowPauseMenu(false);
            }
        }
    }
    
    #region Game Flow
    
    public void StartGame()
    {
        currentState = GameState.Playing;
        currentTime = totalGameTime;
        isTimerRunning = true;
        bossSpawned = false;
        lastMinuteMark = 0;
        _pauseRequesters = 0; // Reset pause requests on new game

        // Reset all difficulty multipliers and stats to their base values
        currentEnemyHealthMultiplier = 1f;
        currentEnemyDamageMultiplier = 1f;
        currentProjectileSpeed = baseProjectileSpeed;
        currentFireRate = baseFireRate;
        currentSightRange = baseSightRange;

        Time.timeScale = 1f;

        if (player != null) player.enabled = true;
        if (enemySpawner != null) enemySpawner.StartSpawning();
    }
    
    public void RestartGame()
    {
        // Important: Reset timescale before reloading scene in case game was paused
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
    
    private void EndGame()
    {
        isTimerRunning = false;
        currentState = GameState.GameOver;
        Debug.Log("Time's up! Game Over.");
    }
    
    public void PlayerDied()
    {
        if (currentState == GameState.GameOver) return; // Prevent multiple calls
        
        currentState = GameState.GameOver;
        isTimerRunning = false;
        if (player != null) player.enabled = false;
    }
    
    #endregion

    #region Pause Management

    /// <summary>
    /// A UI panel or system calls this to request a game pause.
    /// The game will only pause if it's the first request.
    /// </summary>
    public void RequestPause()
    {
        _pauseRequesters++;

        // If this is the first pause request, and we are currently playing, pause the game
        if (_pauseRequesters == 1 && currentState == GameState.Playing)
        {
            currentState = GameState.Paused;
            Time.timeScale = 0f;
            if (player != null) player.enabled = false;
            Debug.Log("Game Paused. Requests: " + _pauseRequesters);
        }
    }

    /// <summary>
    /// A UI panel or system calls this when it no longer needs the game to be paused.
    /// The game will only resume if all requests have been lifted.
    /// </summary>
    public void RequestResume()
    {
        _pauseRequesters--;

        // Ensure the count doesn't go below zero
        if (_pauseRequesters < 0) _pauseRequesters = 0;

        // If this was the last pause request, and the game is currently paused, resume it
        if (_pauseRequesters == 0 && currentState == GameState.Paused)
        {
            currentState = GameState.Playing;
            Time.timeScale = 1f;
            if (player != null) player.enabled = true;
            Debug.Log("Game Resumed. Requests: ".ToString() + _pauseRequesters);
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
            uiManager.UpdateTimerText(currentTime);
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
            Instantiate(bossPrefab, bossSpawnPoint.position + Vector3.up * 3.5f, bossSpawnPoint.rotation);
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
        // General scaling
        currentEnemyHealthMultiplier += healthIncreasePerMinute;
        currentEnemyDamageMultiplier += damageIncreasePerMinute;

        // Caster-specific scaling
        currentProjectileSpeed += projectileSpeedIncreasePerMinute;
        // Ensure fire rate doesn't become impossibly fast
        currentFireRate = Mathf.Max(0.2f, baseFireRate - (fireRateDecreasePerMinute * lastMinuteMark));

        Debug.Log($"DIFFICULTY INCREASED! Minute {lastMinuteMark}. " +
                  $"Health: {currentEnemyHealthMultiplier:F2}x, " +
                  $"Damage: {currentEnemyDamageMultiplier:F2}x, " +
                  $"Proj. Speed: {currentProjectileSpeed:F2}, " +
                  $"Fire Rate: {currentFireRate:F2}s");
    }
    
    #endregion
    
    #region Data and Scoring
    
    
    #endregion
    
    #region Getters and Setters

    public float GetRemainingTime() { return currentTime; }
    
    public void SetGameDuration(float newDuration)
    {
        totalGameTime = newDuration;
        currentTime = newDuration;
        bossSpawned = false;
        lastMinuteMark = 0;
    }
    
    #endregion
}