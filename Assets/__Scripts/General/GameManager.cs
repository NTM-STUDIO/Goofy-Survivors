using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

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

    void Update()
    {
        if (currentState == GameState.Playing)
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

    public void RestartGame()
    {
        // Reload the current scene
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void StartGame()
    {
        currentState = GameState.Playing;
        currentTime = totalGameTime;
        isTimerRunning = true;
        bossSpawned = false;
        lastMinuteMark = 0;
        

        currentEnemyHealthMultiplier = 1f;
        currentEnemyDamageMultiplier = 1f;

        Time.timeScale = 1f;

        if (player != null) player.enabled = true;
        enemySpawner.StartSpawning();
    }

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
            Instantiate(bossPrefab, bossSpawnPoint.position + Vector3.up * 3.5f, bossSpawnPoint.rotation);
            Debug.Log("🔥 O Chefe Final apareceu!");
        }
    }

    public void TogglePause()
    {
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