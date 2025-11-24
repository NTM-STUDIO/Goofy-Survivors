using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using System.Linq;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    public enum GameState { PreGame, Playing, Paused, GameOver, Cinematic }
    public GameState CurrentState { get; private set; }

    [Header("Core Settings")]
    public bool isP2P = false;
    public float totalGameTime = 900f;

    public NetworkVariable<int> sessionSeed = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public int SessionSeed => sessionSeed.Value;

    [Header("SUB-MANAGERS")]
    public PlayerSpawnManager spawnManager;
    public DifficultyManager difficultyManager;
    public ReviveManager reviveManager;
    public GameEventManager eventManager;
    public MapGenerator mapGenerator;
    public UIManager uiManager;

    public NetworkVariable<float> networkCurrentTime = new NetworkVariable<float>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private float localTime;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else { Instance = this; DontDestroyOnLoad(gameObject); }
    }

    void Start()
    {
        CurrentState = GameState.PreGame;
        Time.timeScale = 1f;
        localTime = totalGameTime;
        if (uiManager != null) { uiManager.ShowEndGamePanel(false); uiManager.ShowPauseMenu(false); }
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            networkCurrentTime.Value = totalGameTime;
            if (sessionSeed.Value == 0) sessionSeed.Value = Random.Range(int.MinValue, int.MaxValue);
        }
    }

    private void Update()
    {
        HandleInput();

        if (CurrentState != GameState.Playing) return;

        float dt = Time.deltaTime;
        if (isP2P) { if (IsServer) networkCurrentTime.Value -= dt; }
        else { localTime -= dt; }

        if (uiManager) uiManager.UpdateTimerText(GetRemainingTime());

        if (GetRemainingTime() <= 0)
        {
            if (isP2P) GameOverClientRpc();
            else GameOver();
        }
    }

    private void HandleInput()
    {
        if (Input.GetKeyDown(KeyCode.Tab)) uiManager?.ToggleStatsPanel();

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (CurrentState == GameState.Playing) RequestPause(true, true);
            else if (CurrentState == GameState.Paused) RequestPause(false);
        }
    }

    // ========================================================================
    // CORE METHODS (START GAME CORRIGIDO)
    // ========================================================================

    public void StartGame()
    {
        if (CurrentState == GameState.Playing) return;

        // 1. Configuração Inicial (Só o Host/Server decide isto)
        if (isP2P && !IsServer) return; // Segurança para cliente não iniciar jogo

        localTime = totalGameTime;

        // 2. Resetar Managers (Lógica de Servidor)
        if (spawnManager) spawnManager.StartSpawningProcess(); // Spawna Players
        if (difficultyManager) difficultyManager.ResetDifficulty();
        if (reviveManager) reviveManager.ResetReviveState();
        if (eventManager) eventManager.ResetEvents();

        var enemySpawner = FindObjectOfType<EnemySpawner>();
        if (enemySpawner) enemySpawner.StartSpawning();

        // 3. AVISAR TODOS OS CLIENTES (incluindo o Host) PARA MUDAR UI
        if (isP2P)
        {
            StartGameClientRpc();
        }
        else
        {
            // Singleplayer chama direto
            StartGameLocalLogic();
        }
    }

    [ClientRpc]
    private void StartGameClientRpc()
    {
        StartGameLocalLogic();
    }

    private void StartGameLocalLogic()
    {
        // Esta lógica corre em TODOS (Host e Clientes)
        Debug.Log("[GameManager] StartGameLocalLogic: Iniciando jogo localmente (UI, Mapa, Estado).");

        CurrentState = GameState.Playing;

        // Gera o Mapa (Visuals locais + Networked objects se for server)
        if (mapGenerator)
        {
            mapGenerator.ClearMap();
            mapGenerator.GenerateMap();
        }
        else
        {
            Debug.LogError("GameManager: MapGenerator não atribuído!");
        }

        // Muda a UI (Esconde Lobby, Mostra HUD)
        if (uiManager) uiManager.OnGameStart();
    }

    // ========================================================================
    // REDIRECTS (FACADE PATTERN)
    // ========================================================================
    // ... (O resto do código de redirects mantém-se IGUAL, copiei para baixo para facilitar) ...

    public float currentEnemyHealthMultiplier => difficultyManager.CurrentHealthMult;
    public float currentEnemyDamageMultiplier => difficultyManager.CurrentDamageMult;
    public float currentProjectileSpeed => difficultyManager.CurrentProjectileSpeed;
    public float currentFireRate => difficultyManager.CurrentFireRate;
    public float currentSightRange => difficultyManager.CurrentSightRange;
    public float MultiplayerDifficultyMultiplier => difficultyManager.MpDifficultyMultiplier;

    public MutationType GetGlobalMidgameMutation() => difficultyManager.GlobalMutation;
    public void ApplyMidgameMutationToEnemy(EnemyStats enemy) => difficultyManager.ApplyMutationToEnemy(enemy);

    public float SharedXpMultiplier => difficultyManager.SharedXpMultiplier;
    public void RequestModifySharedXpMultiplier(float amount) => difficultyManager.RequestModifySharedXp(amount);

    public void DistributeSharedXP(float amount)
    {
        if (isP2P) { if (IsServer) difficultyManager.DistributeXpServer(amount); }
        else { FindObjectOfType<PlayerExperience>()?.AddXP(amount); }
    }

    public void PresentGuaranteedRarityToAll(string rarityName) => difficultyManager.PresentRarity(rarityName);

    public EnemyStats reaperStats => eventManager.bossStats;
    public void CacheReaperDamage(float dmg) => eventManager.CacheBossDamage(dmg);
    public float GetReaperDamage() => eventManager.GetBossDamage();
    public void ClearReaperDamageCache() => eventManager.ClearBossCache();

    public void SetChosenPlayerPrefab(GameObject prefab) => spawnManager.SetChosenPrefab(prefab);
    public void SetPlayerSelections_P2P(Dictionary<ulong, GameObject> selections) => spawnManager.SetPlayerSelections(selections);
    public void PlayerDowned(ulong clientId) => reviveManager.NotifyPlayerDowned(clientId);
    public void ServerApplyPlayerDamage(ulong id, float a, Vector3? p = null, float? i = null) => reviveManager.ServerApplyPlayerDamage(id, a, p, i);

    // ========================================================================
    // UI & STATE & LEGACY
    // ========================================================================

    public float GetRemainingTime() => isP2P ? networkCurrentTime.Value : localTime;
    public void SetGameState(GameState newState) => CurrentState = newState;
    public float GetTotalGameTime() => totalGameTime;

    public void GameOver()
    {
        if (CurrentState == GameState.GameOver) return;
        CurrentState = GameState.GameOver;
        Time.timeScale = 0f;
        if (uiManager) uiManager.ShowEndGamePanel(true);
    }

    [ClientRpc]
    private void GameOverClientRpc() => GameOver();

    public void HandlePlayAgain()
    {
        if (!isP2P)
        {
            uiManager?.ShowEndGamePanel(false);
            SoftResetSinglePlayerWorld();
            LoadoutSelections.EnsureValidDefaults();
            if (LoadoutSelections.SelectedCharacterPrefab != null) SetChosenPlayerPrefab(LoadoutSelections.SelectedCharacterPrefab);
            StartGame();
        }
        else
        {
            if (IsServer) NetworkManager.Singleton.SceneManager.LoadScene(SceneManager.GetActiveScene().name, LoadSceneMode.Single);
        }
    }

    // ========================================================================
    // PAUSE LOGIC (CORRIGIDA PARA CLIENTES)
    // ========================================================================

    public void RequestPause(bool pause, bool showMenu = false)
    {
        // 1. LÓGICA DE TEMPO (Só o Server ou Singleplayer para o tempo)
        if (!isP2P || IsServer)
        {
            Time.timeScale = pause ? 0f : 1f;
        }
        // Clientes: O tempo NUNCA para (TimeScale fica 1), mas o estado muda para permitir UI

        // 2. MUDANÇA DE ESTADO
        // Se despausar, voltamos sempre a Playing.
        // Se pausar, vamos para Paused (mesmo que o tempo corra no cliente, o estado UI é Pausado)
        if (CurrentState != GameState.GameOver && CurrentState != GameState.Cinematic)
        {
            CurrentState = pause ? GameState.Paused : GameState.Playing;
        }

        // 3. LÓGICA DE UI (Menu ESC)
        if (uiManager != null)
        {
            if (pause && showMenu)
            {
                uiManager.ShowPauseMenu(true);
            }
            else
            {
                // Se despausar, ou se for LevelUp (showMenu=false), fecha o menu ESC
                uiManager.ShowPauseMenu(false);
            }
        }

        // 4. BLOQUEIO DE INPUT
        // Queremos bloquear o boneco do cliente local sempre que ele estiver num menu
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient?.PlayerObject != null)
        {
            var move = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<Movement>();
            // Se pausou (seja menu ou level up), desativa movimento
            if (move) move.enabled = !pause;
        }
    }

    public void RequestPauseForLevelUp()
    {
        if (!isP2P) RequestPause(true, false);
        else if (IsServer) SetPausedClientRpc(true, false);
    }

    public void ResumeAfterLevelUp()
    {
        if (!isP2P) RequestPause(false);
        else if (IsServer) SetPausedClientRpc(false, false);
    }

    [ClientRpc]
    private void SetPausedClientRpc(bool paused, bool showMenu) => RequestPause(paused, showMenu);

    public void RequestPause(bool pause) => RequestPause(pause, true);
    public void RequestResume() => RequestPause(false);

    public void SoftResetSinglePlayerWorld()
    {
        Time.timeScale = 1f;
        uiManager?.ShowEndGamePanel(false);

        foreach (var p in GameObject.FindGameObjectsWithTag("Player")) Destroy(p);
        foreach (var e in FindObjectsByType<EnemyStats>(FindObjectsSortMode.None)) Destroy(e.gameObject);
        foreach (var p in FindObjectsByType<ProjectileWeapon>(FindObjectsSortMode.None)) Destroy(p.gameObject);
        foreach (var o in FindObjectsByType<OrbitingWeapon>(FindObjectsSortMode.None)) Destroy(o.gameObject);
        foreach (var a in FindObjectsByType<AuraWeapon>(FindObjectsSortMode.None)) Destroy(a.gameObject);
        foreach (var x in FindObjectsByType<ExperienceOrb>(FindObjectsSortMode.None)) Destroy(x.gameObject);
        foreach (var d in FindObjectsByType<DamagePopup>(FindObjectsSortMode.None)) Destroy(d.gameObject);

        if (mapGenerator) mapGenerator.ClearMap();
        else FindObjectOfType<MapGenerator>()?.ClearMap();

        FindObjectOfType<PlayerExperience>()?.ResetState();

        // Reset Spawner
        var spawner = FindObjectOfType<EnemySpawner>();
        if (spawner != null) spawner.StopAndReset();

        if (difficultyManager != null) difficultyManager.ResetDifficulty();

        CurrentState = GameState.PreGame;
    }
}