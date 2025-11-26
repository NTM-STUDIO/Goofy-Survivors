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
    // XP & LEVEL UP (CORRIGIDO PARA USAR O SISTEMA GLOBAL)
    // ========================================================================

    // Chamado pela ExperienceOrb quando é apanhada
    public void DistributeSharedXP(float amount)
    {
        // O PlayerExperience agora é Global (Singleton).
        // Ele sabe lidar com SP e MP internamente.
        if (PlayerExperience.Instance != null)
        {
            // Se for MP, só o Servidor deve chamar isto (o orbe já garante isso).
            // Se for SP, chama direto.
            PlayerExperience.Instance.AddGlobalXP(amount);
        }
        else
        {
            Debug.LogError("[GameManager] PlayerExperience Instance é NULL! Verifique se o script está no objeto Managers.");
        }
    }

    // 1. Chamado pelo PlayerExperience (No Servidor)
    public void TriggerTeamLevelUp()
    {
        if (!IsServer) return;
        if (isLevelingUp) return;

        isLevelingUp = true;

        playersPendingUpgrade = NetworkManager.Singleton.ConnectedClients.Count;
        Debug.Log($"[GameManager] Level Up de Equipa! À espera de {playersPendingUpgrade} jogadores.");

        SetPausedClientRpc(true, false);
        ShowLevelUpUIClientRpc();
    }

    [ClientRpc]
    private void ShowLevelUpUIClientRpc()
    {
        var upgradeManager = FindObjectOfType<UpgradeManager>();
        if (upgradeManager != null) upgradeManager.GenerateAndShowOptions();
    }

    [ServerRpc(RequireOwnership = false)]
    public void ConfirmUpgradeSelectionServerRpc()
    {
        playersPendingUpgrade--;
        Debug.Log($"[GameManager] Um jogador escolheu. Faltam: {playersPendingUpgrade}");

        if (playersPendingUpgrade <= 0)
        {
            // Só entra aqui quando O ÚLTIMO jogador escolher
            Debug.Log("[GameManager] Todos prontos. Resumindo jogo.");
            CloseLevelUpUIClientRpc();
            SetPausedClientRpc(false, false); // Isto despausa o jogo
            isLevelingUp = false;
        }
    }

    [ClientRpc]
    private void CloseLevelUpUIClientRpc()
    {
        var upgradeManager = FindObjectOfType<UpgradeManager>();
        if (upgradeManager != null) upgradeManager.ClosePanel();
    }

    // ========================================================================
    // PAUSE LOGIC
    // ========================================================================

    public void RequestPause(bool pause, bool showMenu = false)
    {
        // 1. Singleplayer
        if (!isP2P)
        {
            LocalPauseLogic(pause, showMenu);
            return;
        }

        // 2. Multiplayer
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            if (IsServer) SetPausedClientRpc(pause, showMenu);
            else LocalPauseLogic(pause, showMenu);
        }
        else
        {
            // Fallback (Rede caiu)
            LocalPauseLogic(pause, showMenu);
        }
    }

    private void LocalPauseLogic(bool pause, bool showMenu)
    {
        Time.timeScale = pause ? 0f : 1f;

        if (CurrentState != GameState.GameOver && CurrentState != GameState.Cinematic)
        {
            CurrentState = pause ? GameState.Paused : GameState.Playing;
        }

        if (uiManager != null)
        {
            if (pause && showMenu) uiManager.ShowPauseMenu(true);
            else uiManager.ShowPauseMenu(false);
        }

        // Input Block
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient?.PlayerObject != null)
        {
            var move = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<Movement>();
            if (move) move.enabled = !pause;
        }
        else
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                var move = player.GetComponent<Movement>();
                if (move) move.enabled = !pause;
            }
        }
    }

    [ClientRpc]
    private void SetPausedClientRpc(bool paused, bool showMenu) => LocalPauseLogic(paused, showMenu);

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

    // ========================================================================
    // START GAME & CORE
    // ========================================================================

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

        // Reset XP Global
        if (PlayerExperience.Instance != null) PlayerExperience.Instance.ResetState();
    }

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
        if (PlayerExperience.Instance) PlayerExperience.Instance.ResetState();

        FindObjectOfType<EnemySpawner>()?.StopAndReset();
        if (difficultyManager) difficultyManager.ResetDifficulty();
        CurrentState = GameState.PreGame;
    }

    // --- REDIRECTS ---
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
    public float GetRemainingTime() => isP2P ? networkCurrentTime.Value : localTime;
    public void SetGameState(GameState newState) => CurrentState = newState;
    public float GetTotalGameTime() => totalGameTime;
    public void RequestPause(bool pause) => RequestPause(pause, true);
    public void RequestResume() => RequestPause(false);
}