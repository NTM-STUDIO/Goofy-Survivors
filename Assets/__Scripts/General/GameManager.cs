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

    public NetworkVariable<int> sessionSeed = new NetworkVariable<int>(0);
    public int SessionSeed => sessionSeed.Value;

    [Header("SUB-MANAGERS")]
    public PlayerSpawnManager spawnManager;
    public DifficultyManager difficultyManager;
    public ReviveManager reviveManager;
    public GameEventManager eventManager;
    public MapGenerator mapGenerator;
    public UIManager uiManager;

    public NetworkVariable<float> networkCurrentTime = new NetworkVariable<float>(0);
    private float localTime;

    private int playersPendingUpgrade = 0;
    private bool isLevelingUp = false;

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
            else if (CurrentState == GameState.Paused && !isLevelingUp) RequestPause(false);
        }
    }

    // ========================================================================
    // PAUSE LOGIC (CORRIGIDA PARA SINGLEPLAYER)
    // ========================================================================
public void RequestPause(bool pause, bool showMenu = false)
    {
        // SE FOR SINGLEPLAYER:
        if (!isP2P)
        {
            // Pausa tudo (Tempo + Menu)
            LocalPauseLogic(pause, showMenu, true); 
            return;
        }

        // SE FOR MULTIPLAYER:
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            if (IsServer)
            {
                // O HOST manda: Pausa o tempo para todos e abre o menu
                // true = modificar TimeScale
                SetPausedClientRpc(pause, showMenu);
            }
            else
            {
                // O CLIENTE pede: Abre apenas o menu local, NÃO para o tempo
                // false = NÃO modificar TimeScale (o jogo continua a correr)
                LocalPauseLogic(pause, showMenu, false); 
            }
        }
    }

    
   private void LocalPauseLogic(bool pause, bool showMenu, bool modifyTimeScale)
    {
        // 1. TEMPO: Só altera o tempo se for autorizado (Host ou SP)
        if (modifyTimeScale)
        {
            Time.timeScale = pause ? 0f : 1f;
        }

        // 2. ESTADO LÓGICO
        if (CurrentState != GameState.GameOver && CurrentState != GameState.Cinematic)
        {
            // Mesmo que o tempo corra, logicamente o jogador está "em menu"
            CurrentState = pause ? GameState.Paused : GameState.Playing;
        }

        // 3. UI DO MENU
        if (uiManager != null)
        {
            if (pause && showMenu) uiManager.ShowPauseMenu(true);
            else uiManager.ShowPauseMenu(false);
        }

        // 4. BLOQUEIO DE INPUT (Movimento)
        // Queremos bloquear o boneco do cliente local sempre que ele estiver num menu
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient?.PlayerObject != null)
        {
            var move = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<Movement>();
            if (move) move.enabled = !pause;
        }
        // Fallback SP
        else
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null && player.GetComponent<Movement>())
                player.GetComponent<Movement>().enabled = !pause;
        }
    }

    [ClientRpc]
    private void SetPausedClientRpc(bool paused, bool showMenu)
    {
        // Quando o servidor manda via RPC, é para pausar o tempo em toda a gente
        LocalPauseLogic(paused, showMenu, true);
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

    // ... Resto do Script (StartGame, Redirects, etc.) Mantém igual ...
    // (Vou incluir o resto essencial para garantir que compila sem erros)

    public void StartGame()
    {
        if (CurrentState == GameState.Playing) return;
        if (isP2P && !IsServer) return;

        localTime = totalGameTime;

        if (spawnManager) spawnManager.StartSpawningProcess();
        if (difficultyManager) difficultyManager.ResetDifficulty();
        if (reviveManager) reviveManager.ResetReviveState();
        if (eventManager) eventManager.ResetEvents();

        var enemySpawner = FindObjectOfType<EnemySpawner>();
        if (enemySpawner) enemySpawner.StartSpawning();

        if (isP2P) StartGameClientRpc();
        else StartGameLocalLogic();
    }

    [ClientRpc] private void StartGameClientRpc() => StartGameLocalLogic();

    private void StartGameLocalLogic()
    {
        CurrentState = GameState.Playing;
        if (mapGenerator) { mapGenerator.ClearMap(); mapGenerator.GenerateMap(); }
        if (uiManager) uiManager.OnGameStart();
    }

    // Level Up Sync
    public void TriggerTeamLevelUp()
    {
        if (!IsServer) return;
        if (isLevelingUp) return;
        isLevelingUp = true;
        playersPendingUpgrade = NetworkManager.Singleton.ConnectedClients.Count;
        SetPausedClientRpc(true, false);
        ShowLevelUpUIClientRpc();
    }

    [ClientRpc] private void ShowLevelUpUIClientRpc() { var um = FindObjectOfType<UpgradeManager>(); if (um) um.GenerateAndShowOptions(); }

    [ServerRpc(RequireOwnership = false)]
    public void ConfirmUpgradeSelectionServerRpc()
    {
        playersPendingUpgrade--;
        if (playersPendingUpgrade <= 0)
        {
            CloseLevelUpUIClientRpc();
            SetPausedClientRpc(false, false);
            isLevelingUp = false;
        }
    }

    [ClientRpc] private void CloseLevelUpUIClientRpc() { var um = FindObjectOfType<UpgradeManager>(); if (um) um.ClosePanel(); }

    // Redirects e Helpers
    public void RequestPause(bool pause) => RequestPause(pause, true);
    public void RequestResume() => RequestPause(false);
    public float GetTotalGameTime() => totalGameTime;
    public float GetRemainingTime() => isP2P ? networkCurrentTime.Value : localTime;
    public void SetGameState(GameState newState) => CurrentState = newState;

    public void GameOver()
    {
        if (CurrentState == GameState.GameOver) return;
        CurrentState = GameState.GameOver;
        Time.timeScale = 0f;
        if (uiManager) uiManager.ShowEndGamePanel(true);
    }
    [ClientRpc] private void GameOverClientRpc() => GameOver();

    public void HandlePlayAgain()
    {
        if (!isP2P)
        {
            uiManager?.ShowEndGamePanel(false); SoftResetSinglePlayerWorld();
            LoadoutSelections.EnsureValidDefaults(); if (LoadoutSelections.SelectedCharacterPrefab) SetChosenPlayerPrefab(LoadoutSelections.SelectedCharacterPrefab);
            StartGame();
        }
        else if (IsServer) NetworkManager.Singleton.SceneManager.LoadScene(SceneManager.GetActiveScene().name, LoadSceneMode.Single);
    }

    public void SoftResetSinglePlayerWorld()
    {
        Time.timeScale = 1f; uiManager?.ShowEndGamePanel(false);
        foreach (var p in GameObject.FindGameObjectsWithTag("Player")) Destroy(p);
        foreach (var e in FindObjectsByType<EnemyStats>(FindObjectsSortMode.None)) Destroy(e.gameObject);
        foreach (var p in FindObjectsByType<ProjectileWeapon>(FindObjectsSortMode.None)) Destroy(p.gameObject);
        foreach (var o in FindObjectsByType<OrbitingWeapon>(FindObjectsSortMode.None)) Destroy(o.gameObject);
        foreach (var x in FindObjectsByType<ExperienceOrb>(FindObjectsSortMode.None)) Destroy(x.gameObject);
        foreach (var d in FindObjectsByType<DamagePopup>(FindObjectsSortMode.None)) Destroy(d.gameObject);
        if (mapGenerator) mapGenerator.ClearMap(); else FindObjectOfType<MapGenerator>()?.ClearMap();
        FindObjectOfType<PlayerExperience>()?.ResetState();
        FindObjectOfType<EnemySpawner>()?.StopAndReset();
        if (difficultyManager) difficultyManager.ResetDifficulty();
        CurrentState = GameState.PreGame;
    }

    // Facade Redirects
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
    // Chamado pela ExperienceOrb quando é apanhada
    // --- ADICIONA ISTO AO GAMEMANAGER.CS ---

    // Chamado pela ExperienceOrb (no Servidor)
    public void DistributeSharedXP(float amount)
    {
        // SE FOR MULTIPLAYER
        if (isP2P)
        {
            if (IsServer)
            {
                // 1. Calcula o valor final (com multiplicador de equipa se houver)
                // Se não tiveres DifficultyManager, usa 1.0f como fallback
                float multiplier = (difficultyManager != null) ? difficultyManager.SharedXpMultiplier : 1.0f;
                if (multiplier <= 0.01f) multiplier = 1.0f; // Prevenção de erro

                float finalAmount = amount * multiplier;

                // 2. Manda a mensagem para TODOS (Host + Clientes)
                DistributeXpClientRpc(finalAmount);
            }
        }
        // SE FOR SINGLEPLAYER
        else
        {
            var px = FindObjectOfType<PlayerExperience>();
            if (px != null) px.AddXP(amount);
        }
    }

    // Novo RPC: Este código corre em TODOS os computadores
    [ClientRpc]
    private void DistributeXpClientRpc(float amount)
    {
        // Encontra o jogador local deste computador específico
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient?.PlayerObject != null)
        {
            var px = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerExperience>();

            if (px != null)
            {
                // Adiciona o XP e ATUALIZA A BARRA AZUL (UI)
                px.AddXPFromServerScaled(amount);
            }
        }
    }

    public void PresentGuaranteedRarityToAll(string rarityName) => difficultyManager.PresentRarity(rarityName);
    public EnemyStats reaperStats => eventManager.bossStats;
    public void CacheReaperDamage(float dmg) => eventManager.CacheBossDamage(dmg);
    public float GetReaperDamage() => eventManager.GetBossDamage();
    public void ClearReaperDamageCache() => eventManager.ClearBossCache();
    public void SetChosenPlayerPrefab(GameObject prefab) => spawnManager.SetChosenPrefab(prefab);
    public void SetPlayerSelections_P2P(Dictionary<ulong, GameObject> selections) => spawnManager.SetPlayerSelections(selections);
    public void PlayerDowned(ulong clientId) => reviveManager.NotifyPlayerDowned(clientId);
    public void ServerApplyPlayerDamage(ulong id, float a, Vector3? p = null, float? i = null)
    {
        if (isP2P) reviveManager?.ServerApplyPlayerDamage(id, a, p, i);
        else FindObjectOfType<PlayerStats>()?.ApplyDamage(a, p, i);
    }
}