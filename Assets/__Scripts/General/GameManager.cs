using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Mode Settings")]
    [Tooltip("Controlado pelo UIManager. Ative para o modo P2P.")]
    public bool isP2P = false;

    [Header("Core Manager References")]
    [SerializeField] private UIManager uiManager;
    [SerializeField] private EnemySpawner enemySpawner;
    [SerializeField] private EnemyDespawner enemyDespawner;
    [SerializeField] private UpgradeManager upgradeManager;
    [SerializeField] private PlayerExperience playerExperience;

    [Header("Player References (Internal)")]
    private Movement localPlayer;
    private GameObject chosenPlayerPrefab;

    [Header("Prefabs & Spawn Points")]
    [SerializeField] private Transform playerSpawnPoint;
    [SerializeField] private GameObject bossPrefab;
    [SerializeField] private Transform bossSpawnPoint;

    // Usado em P2P para guardar a seleção de todos os jogadores
    private Dictionary<ulong, GameObject> playerUnitSelections = new Dictionary<ulong, GameObject>();

    public enum GameState { PreGame, Playing, Paused, GameOver }
    public GameState CurrentState { get; private set; }

    [Header("Timer Settings")]
    [SerializeField] private float totalGameTime = 900f;
    private float currentTime; // Usado para a contagem decrescente em Single-Player
    private NetworkVariable<float> networkCurrentTime = new NetworkVariable<float>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private float timerUIAccumulator = 0f;

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

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
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
        if (IsOwner)
        {
            HandleInput();
        }

        if (isP2P && !IsHost)
        {
            UpdateTimerUI(networkCurrentTime.Value);
            return;
        }
        
        if (CurrentState == GameState.Playing)
        {
            UpdateTimer();
            CheckForDifficultyIncrease();
            CheckForBossSpawn();
        }
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
        GameObject localPlayerObject = NetworkManager.Singleton.LocalClient.PlayerObject.gameObject;
        InitializeAllSystems(localPlayerObject);
    }

    private void InitializeAllSystems(GameObject playerObject)
    {
        localPlayer = playerObject.GetComponent<Movement>();
        playerExperience?.Initialize(playerObject);
        upgradeManager?.Initialize(playerObject);
        enemyDespawner?.Initialize(playerObject);

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
        // Lógica de morte P2P (ex: verificar se é o último jogador vivo)
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
            NetworkManager.Singleton.Shutdown();
            if(Instance != null) Destroy(Instance.gameObject);
            SceneManager.LoadScene(0);
            return;
        }
        
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
    #endregion

    #region Pause Management
    public void RequestPause(bool showMenu = false)
    {
        if (isP2P || CurrentState != GameState.Playing) return;

        CurrentState = GameState.Paused;
        Time.timeScale = 0f;
        if (localPlayer != null) localPlayer.enabled = false;

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
        if (isP2P || CurrentState != GameState.Paused) return;

        CurrentState = GameState.Playing;
        Time.timeScale = 1f;
        if (localPlayer != null) localPlayer.enabled = true;
        uiManager?.ShowPauseMenu(false);
    }
    #endregion

    #region Timers and Spawning
    private void UpdateTimer()
    {
        float newTime;
        if (isP2P)
        {
            newTime = networkCurrentTime.Value - Time.deltaTime;
            networkCurrentTime.Value = newTime;
        }
        else
        {
            newTime = currentTime - Time.deltaTime;
            currentTime = newTime;
        }

        UpdateTimerUI(newTime);

        if (newTime <= 0)
        {
            if (isP2P) { GameOverClientRpc(); }
            else { GameOver(); }
        }
    }

    private void UpdateTimerUI(float timeToDisplay)
    {
        timerUIAccumulator += Time.deltaTime;
        if (timerUIAccumulator >= 1f)
        {
            timerUIAccumulator = 0f;
            uiManager?.UpdateTimerText(timeToDisplay);
        }
    }

    private void CheckForBossSpawn()
    {
        float relevantTime = isP2P ? networkCurrentTime.Value : currentTime;
        if (!bossSpawned && relevantTime <= 10.0f)
        {
            SpawnBoss();
            bossSpawned = true;
        }
    }

    private void SpawnBoss()
    {
        if (bossPrefab == null || bossSpawnPoint == null) return;
        GameObject bossObject = Instantiate(bossPrefab, bossSpawnPoint.position + Vector3.up * 10f, bossSpawnPoint.rotation);
        
        if (isP2P && IsHost)
        {
            bossObject.GetComponent<NetworkObject>().Spawn(true);
        }
        
        reaperStats = bossObject.GetComponent<EnemyStats>();
        Debug.Log("O Boss Final apareceu!");
    }
    #endregion

    #region Difficulty Scaling
    private void CheckForDifficultyIncrease()
    {
        float relevantTime = isP2P ? networkCurrentTime.Value : currentTime;
        int currentInterval = Mathf.FloorToInt((totalGameTime - relevantTime) / difficultyIncreaseInterval);

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
        currentFireRate = Mathf.Max(0.2f, currentFireRate / fireRateMultiplier);
        Debug.Log($"Dificuldade Aumentada (x{lastDifficultyIncreaseMark}) | HP×{currentEnemyHealthMultiplier:F2}, DMG×{currentEnemyDamageMultiplier:F2}");
    }
    #endregion

    #region Getters / Setters
    public float GetRemainingTime()
    {
        return isP2P ? networkCurrentTime.Value : currentTime;
    }

    public float GetTotalGameTime()
    {
        return totalGameTime;
    }
    #endregion
}