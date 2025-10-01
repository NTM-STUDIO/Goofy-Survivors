using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;



public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState { Playing, Paused, GameOver }
    public GameState currentState;

    [Header("Timer Settings")]
    public float totalGameTime = 900f; // 15 minutos em segundos (pode ser 450 = 7.5min)
    private float currentTime;
    private bool isTimerRunning = false;

    [Header("Difficulty Settings")]
    public float difficultyMultiplier = 1.2f;
    private int lastMinuteMark = 0;

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
    }

    // Inicia Jogo
    public void StartGame()
    {
        currentState = GameState.Playing;
        currentTime = totalGameTime;
        isTimerRunning = true;
        bossSpawned = false;
        lastMinuteMark = 0;

        Time.timeScale = 1f;

        if (player != null) player.enabled = true;
        enemySpawner.StartSpawning();
    }

    // Atualiza Timer
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

            UIManager.Instance.UpdateTimerText(currentTime);
        }
    }

    // Escala dificuldade a cada minuto
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
        // enemySpawner.IncreaseEnemyStats(difficultyMultiplier);
        Debug.Log($"Dificuldade aumentada em {difficultyMultiplier}x no minuto {lastMinuteMark}");
    }

    // Spawn do Boss 10s antes do fim
    private void CheckForBossSpawn()
    {
        if (!bossSpawned && currentTime <= 10.0f)
        {
            //SpawnBoss();
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

    // Pausa/Resume
    public void TogglePause()
    {
        if (currentState == GameState.Playing)
        {
            currentState = GameState.Paused;
            Time.timeScale = 0f;
            if (player != null) player.enabled = false;
            UIManager.Instance.ShowPauseMenu(true);
        }
        else if (currentState == GameState.Paused)
        {
            currentState = GameState.Playing;
            Time.timeScale = 1f;
            if (player != null) player.enabled = true;
            UIManager.Instance.ShowPauseMenu(false);
        }
    }

    // Quando o jogador morre
    public void PlayerDied()
    {
        currentState = GameState.GameOver;
        isTimerRunning = false;

        if (player != null) player.enabled = false;
        // enemySpawner.StopSpawning();

        UIManager.Instance.ShowUsernameInput();
    }

    // Submeter username (placeholder BD)
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
        // Pode mostrar ecrã de vitória aqui
    }

    // Getter público para o tempo restante
    public float GetRemainingTime()
    {
        return currentTime;
    }

    // Setter para ajustar duração (ex: 15m → 7.5m)
    public void SetGameDuration(float newDuration)
    {
        totalGameTime = newDuration;
        currentTime = newDuration;
        bossSpawned = false;
        lastMinuteMark = 0;
    }
}
