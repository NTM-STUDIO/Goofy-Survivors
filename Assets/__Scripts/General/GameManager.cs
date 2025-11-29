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
            TriggerGameOver();
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

    // ========================================================================
    // AÇÕES DE FIM DE JOGO
    // ========================================================================

    // OPÇÃO 1: Começar de novo imediatamente (Soft Restart)
    public void ActionPlayAgain()
    {
        // Segurança: Apenas o Host pode decidir reiniciar o jogo em MP
        if (isP2P && !IsServer) return;

        Debug.Log("[GameManager] Play Again acionado. Limpando e Reiniciando...");

        // 1. Limpa o mapa e objetos
        CleanupGameWorld();

        // 2. Garante que os Loadouts estão prontos (para SP)
        if (!isP2P)
        {
            LoadoutSelections.EnsureValidDefaults();
            if (LoadoutSelections.SelectedCharacterPrefab != null)
                SetChosenPlayerPrefab(LoadoutSelections.SelectedCharacterPrefab);
        }

        // 3. Chama o StartGame imediatamente
        StartGame();
    }

    // OPÇÃO 2: Voltar ao Lobby/Menu (Resetar mas ficar à espera)
    public void ActionLeaveToLobby()
    {
        // Se for Cliente em MP, apenas se desconecta e sai
        if (isP2P && !IsServer)
        {
            NetworkManager.Singleton.Shutdown();
            SceneManager.LoadScene("Splash"); // Ou o nome da tua cena de menu
            return;
        }

        Debug.Log("[GameManager] Leave to Lobby acionado. Limpando e aguardando.");

        // 1. Limpa o mapa e objetos
        CleanupGameWorld();

        // 2. Define o estado para PreGame (Lobby)
        CurrentState = GameState.PreGame;

        // 3. Diz ao UI Manager para mostrar o Lobby
        if (uiManager != null)
        {
            uiManager.ReturnToLobby(); // Certifica-te que tens este método no UIManager
        }

        // Em Multiplayer Host, isto mantém a sala aberta mas volta ao ecrã de seleção
        if (isP2P && IsServer)
        {
            // Opcional: Mandar RPC para clientes voltarem ao lobby visualmente
            ReturnToLobbyClientRpc();
        }
    }

    [ClientRpc]
    private void ReturnToLobbyClientRpc()
    {
        if (IsHost) return; // O Host já fez a limpeza localmente

        // Limpa lixo visual local que não é NetworkObject (ex: Damage Popups)
        foreach (var d in FindObjectsByType<DamagePopup>(FindObjectsSortMode.None))
            Destroy(d.gameObject);

        // Atualiza a UI
        if (uiManager != null) uiManager.ReturnToLobby();

        // Atualiza o estado local
        CurrentState = GameState.PreGame;
    }

    // --- FUNÇÃO AUXILIAR DE LIMPEZA (USADA PELOS DOIS) ---
    private void CleanupGameWorld()
    {
        // 1. Parar Spawners e Corrotinas
        if (uiManager) uiManager.ShowEndGamePanel(false);
        var enemySpawner = FindObjectOfType<EnemySpawner>();
        if (enemySpawner) enemySpawner.StopAndReset();

        // 2. Destruir Inimigos e Jogadores
        // Se for Server, usa Despawn. Se for SP, usa Destroy.
        bool isServer = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

        // Limpar Jogadores (Para serem spawnados de novo no StartGame)
        if (isP2P && isServer)
        {
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client.PlayerObject != null) client.PlayerObject.Despawn(true);
            }
        }
        else
        {
            foreach (var p in GameObject.FindGameObjectsWithTag("Player")) Destroy(p);
        }

        // Limpar Inimigos e Objetos
        foreach (var e in FindObjectsByType<EnemyStats>(FindObjectsSortMode.None))
            DestroyNetworkOrLocal(e.gameObject);

        foreach (var p in FindObjectsByType<ProjectileWeapon>(FindObjectsSortMode.None))
            DestroyNetworkOrLocal(p.gameObject);

        foreach (var o in FindObjectsByType<OrbitingWeapon>(FindObjectsSortMode.None))
            DestroyNetworkOrLocal(o.gameObject);

        foreach (var a in FindObjectsByType<AuraWeapon>(FindObjectsSortMode.None))
            DestroyNetworkOrLocal(a.gameObject);

        foreach (var x in FindObjectsByType<ExperienceOrb>(FindObjectsSortMode.None))
            DestroyNetworkOrLocal(x.gameObject);

        foreach (var d in FindObjectsByType<DamagePopup>(FindObjectsSortMode.None))
            Destroy(d.gameObject); // Popups são sempre locais

        // 3. Resetar Managers
        if (mapGenerator) mapGenerator.ClearMap();
        else FindObjectOfType<MapGenerator>()?.ClearMap();

        if (difficultyManager) difficultyManager.ResetDifficulty();
        if (reviveManager) reviveManager.ResetReviveState();
        if (eventManager) eventManager.ResetEvents();

        // Reset XP
        FindObjectOfType<PlayerExperience>()?.ResetState();

        Time.timeScale = 1f; // Garante que o tempo volta ao normal
    }

    // Helper para destruir corretamente em MP ou SP
    private void DestroyNetworkOrLocal(GameObject obj)
    {
        if (obj == null) return;
        var netObj = obj.GetComponent<NetworkObject>();
        if (netObj != null && netObj.IsSpawned && NetworkManager.Singleton.IsServer)
        {
            netObj.Despawn(true);
        }
        else
        {
            Destroy(obj);
        }
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

    public void TriggerGameOver()
    {
        // Se for Multiplayer
        if (isP2P)
        {
            // Só o Servidor pode decretar o fim do jogo
            if (IsServer)
            {
                GameOverClientRpc(); // Avisa toda a gente (incluindo a si próprio)
            }
        }
        // Se for Singleplayer
        else
        {
            PerformGameOverLocal();
        }
    }



    // 3. LÓGICA LOCAL (UI e Parar Tempo)
    private void PerformGameOverLocal()
    {
        if (CurrentState == GameState.GameOver) return;

        Debug.Log("[GameManager] GAME OVER!");
        CurrentState = GameState.GameOver;

        // Pára o tempo
        Time.timeScale = 0f;

        // Mostra Painel
        if (uiManager) uiManager.ShowEndGamePanel(true);

        // Opcional: Desativa controlos locais
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient?.PlayerObject != null)
        {
            var move = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<Movement>();
            if (move) move.enabled = false;
        }
    }

    // MANTÉM ESTE PARA COMPATIBILIDADE (Se algum script antigo ainda o chamar)
    public void GameOver() => TriggerGameOver();
    public void SoftResetSinglePlayerWorld()
    {
        // 1. GARANTIR QUE O TEMPO VOLTA AO NORMAL
        Time.timeScale = 1f;

        // 2. LIMPEZA DE UI (Fecha painéis de armas, upgrades, etc)
        if (uiManager != null)
        {
            uiManager.ForceCloseGameplayPanels(); // <--- CHAMA O NOVO MÉTODO
            uiManager.ShowEndGamePanel(false);
        }

        // 3. LIMPEZA DO UPGRADE MANAGER (Limpa fila de níveis)
        var upgradeManager = FindObjectOfType<UpgradeManager>();
        if (upgradeManager != null)
        {
            upgradeManager.ForceReset(); // <--- CHAMA O NOVO MÉTODO
        }

        // ... (O resto do teu código de destruir inimigos e mapa continua aqui) ...

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
        FindObjectOfType<EnemySpawner>()?.StopAndReset();

        if (difficultyManager) difficultyManager.ResetDifficulty();
        if (eventManager) eventManager.ResetEvents(); // Não esqueças de resetar os eventos do Boss

        CurrentState = GameState.PreGame;
        Debug.Log("[GameManager] Soft Reset concluído. UI e Estados limpos.");
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