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

    [Header("SUB-MANAGERS (Arraste no Inspector)")]
    public PlayerSpawnManager spawnManager;
    public DifficultyManager difficultyManager;
    public ReviveManager reviveManager;
    public GameEventManager eventManager;
    public MapGenerator mapGenerator; // <--- ADICIONADO (IMPORTANTE)
    public UIManager uiManager;

    public NetworkVariable<float> networkCurrentTime = new NetworkVariable<float>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private float localTime;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else { Instance = this; DontDestroyOnLoad(gameObject); }
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
        HandleInput(); // <--- Adicionado verificação de Input

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
        if (Input.GetKeyDown(KeyCode.Tab))
            uiManager?.ToggleStatsPanel();

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (CurrentState == GameState.Playing)
            {
                // ESC: Pausa E Mostra Menu (true, true)
                RequestPause(true, true);
            }
            else if (CurrentState == GameState.Paused)
            {
                // ESC no Menu: Despausa (fecha menu)
                RequestPause(false);
            }
        }
    }

    // ========================================================================
    // PAUSE & STATE LOGIC (CORRIGIDA)
    // ========================================================================

    /// <summary>
    /// Pausa ou Despausa o jogo.
    /// </summary>
    /// <param name="pause">True para pausar, False para retomar.</param>
    /// <param name="showMenu">Se True, abre o Menu de Pausa (ESC). Se False, apenas para o tempo (LevelUp/Cinemática).</param>
    public void RequestPause(bool pause, bool showMenu = false)
    {
        // Em P2P, pausar é complexo. Geralmente só permitimos se não for P2P ou se for lógica específica.
        // Aqui assumimos que pausa localmente o tempo e input.

        Time.timeScale = pause ? 0f : 1f;

        // Atualiza estado se não for GameOver/Cinematic (para não quebrar fluxo)
        if (CurrentState != GameState.GameOver && CurrentState != GameState.Cinematic)
        {
            CurrentState = pause ? GameState.Paused : GameState.Playing;
        }

        // Lógica de UI
        if (uiManager != null)
        {
            if (pause && showMenu)
            {
                uiManager.ShowPauseMenu(true);
            }
            else
            {
                // Se despausar, ou se for pausa sem menu (LevelUp), garantimos que o menu fecha
                uiManager.ShowPauseMenu(false);
            }
        }

        // Bloqueia/Desbloqueia movimento do jogador local
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient?.PlayerObject != null)
        {
            var move = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<Movement>();
            if (move) move.enabled = !pause;
        }
    }

    public void RequestPauseForLevelUp()
    {
        if (!isP2P) RequestPause(true, false); // False = Não mostra menu
        else if (IsServer) SetPausedClientRpc(true, false);
    }

    public void ResumeAfterLevelUp()
    {
        if (!isP2P) RequestPause(false);
        else if (IsServer) SetPausedClientRpc(false, false);
    }

    [ClientRpc]
    private void SetPausedClientRpc(bool paused, bool showMenu)
    {
        RequestPause(paused, showMenu);
    }

    // ========================================================================
    // REDIRECTS (FACADE PATTERN)
    // ========================================================================

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
    public void ServerApplyPlayerDamage(ulong id, float amount, Vector3? p = null, float? i = null)
    {
        // Se for Multiplayer, usa o ReviveManager (como antes)
        if (isP2P)
        {
            if (reviveManager != null)
            {
                reviveManager.ServerApplyPlayerDamage(id, amount, p, i);
            }
        }
        // Se for Singleplayer, aplica o dano DIRETAMENTE no jogador local
        else
        {
            // Em SP, ignoramos o 'id' e procuramos o único PlayerStats que existe
            var playerStats = FindObjectOfType<PlayerStats>();
            if (playerStats != null)
            {
                playerStats.ApplyDamage(amount, p, i);
            }
        }
    }
    // ========================================================================
    // CORE METHODS
    // ========================================================================

    public void StartGame()
    {
        if (CurrentState == GameState.Playing) return;
        localTime = totalGameTime;
        CurrentState = GameState.Playing;

        if (spawnManager) spawnManager.StartSpawningProcess();
        if (difficultyManager) difficultyManager.ResetDifficulty();
        if (reviveManager) reviveManager.ResetReviveState();
        if (eventManager) eventManager.ResetEvents();

        // GERAÇÃO DO MAPA
        if (mapGenerator)
        {
            mapGenerator.ClearMap();
            mapGenerator.GenerateMap();
        }
        else
        {
            Debug.LogError("GameManager: MapGenerator não atribuído no Inspector!");
        }

        var enemySpawner = FindObjectOfType<EnemySpawner>();
        if (enemySpawner) enemySpawner.StartSpawning();

        if (uiManager) uiManager.OnGameStart();
    }

    void Start()
    {
        // --- CORREÇÃO CRÍTICA ---
        // Força o estado inicial para PreGame (Menu/Lobby)
        CurrentState = GameState.PreGame;

        // Garante que o tempo flui (para animações de UI)
        Time.timeScale = 1f;

        // Inicializa o tempo local para não ser 0 (evita Game Over imediato se algo falhar)
        localTime = totalGameTime;

        // Garante que o painel de fim de jogo está escondido
        if (uiManager != null)
        {
            uiManager.ShowEndGamePanel(false);
            uiManager.ShowPauseMenu(false);
        }

        Debug.Log("[GameManager] Estado inicial definido para PreGame. Aguardando StartGame().");
    }
    public float GetRemainingTime() => isP2P ? networkCurrentTime.Value : localTime;

    public void SetGameState(GameState newState) => CurrentState = newState;

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

   public void SoftResetSinglePlayerWorld()
    {
        Time.timeScale = 1f;
        uiManager?.ShowEndGamePanel(false);

        // Limpeza de objetos visuais
        foreach (var p in GameObject.FindGameObjectsWithTag("Player")) Destroy(p);
        foreach (var e in FindObjectsByType<EnemyStats>(FindObjectsSortMode.None)) Destroy(e.gameObject);
        foreach (var p in FindObjectsByType<ProjectileWeapon>(FindObjectsSortMode.None)) Destroy(p.gameObject);
        foreach (var o in FindObjectsByType<OrbitingWeapon>(FindObjectsSortMode.None)) Destroy(o.gameObject);
        foreach (var a in FindObjectsByType<AuraWeapon>(FindObjectsSortMode.None)) Destroy(a.gameObject);
        foreach (var x in FindObjectsByType<ExperienceOrb>(FindObjectsSortMode.None)) Destroy(x.gameObject);
        foreach (var d in FindObjectsByType<DamagePopup>(FindObjectsSortMode.None)) Destroy(d.gameObject);

        // Limpa Mapa
        if(mapGenerator) mapGenerator.ClearMap();
        else FindObjectOfType<MapGenerator>()?.ClearMap();
        
        // Reseta XP
        FindObjectOfType<PlayerExperience>()?.ResetState();

        // --- CORREÇÃO AQUI: RESETAR O ENEMY SPAWNER ---
        var spawner = FindObjectOfType<EnemySpawner>();
        if (spawner != null)
        {
            spawner.StopAndReset(); // Isto mete a wave a 0
        }
        // ----------------------------------------------

        // Garante que a dificuldade volta ao base (x1)
        if (difficultyManager != null) difficultyManager.ResetDifficulty();

        CurrentState = GameState.PreGame;
    }
    // ========================================================================
    // LEGACY / OVERLOAD SUPPORT
    // ========================================================================

    // Suporte para scripts antigos que chamam apenas RequestPause(bool)
    // Assume que se chamou de fora, quer mostrar o menu (true)
    public void RequestPause(bool pause) => RequestPause(pause, true);

    public void RequestResume() => RequestPause(false);
    public float GetTotalGameTime() => totalGameTime;
}