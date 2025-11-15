// Filename: GameManager.cs
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using System.Linq;


public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Mode Settings")]
    public bool isP2P = false;

    // === VContainer Injection (mantido da versão 2) ===
    [Header("Core Manager References")]


    [SerializeField] private UIManager uiManager;
    [SerializeField] private EnemySpawner enemySpawner;
    [SerializeField] private EnemyDespawner enemyDespawner;
    [SerializeField] private UpgradeManager upgradeManager;
    [SerializeField] private PlayerExperience playerExperience;
    [SerializeField] private MapGenerator mapGenerator;

    [Header("Network Prefab Registry (MP)")]
    [Tooltip("Assign any runtime-spawned prefabs here (e.g., Experience Orbs, Projectiles, Auras, Shields, special Enemies) so both host and clients register them with Netcode before they spawn.")]
    [SerializeField] private List<GameObject> runtimeNetworkPrefabs = new List<GameObject>();

    private Movement localPlayer;
    private GameObject chosenPlayerPrefab;

    [Header("Prefabs & Spawn Points")]
    [SerializeField] private Transform playerSpawnPoint;
    [SerializeField] private GameObject bossPrefab;
    [SerializeField] private Transform bossSpawnPoint;
    [SerializeField] private GameObject playerCameraPrefab;

    private Dictionary<ulong, GameObject> playerUnitSelections = new Dictionary<ulong, GameObject>();

    public enum GameState { PreGame, Playing, Paused, GameOver }
    public GameState CurrentState { get; private set; }

    [Header("Timer Settings")]
    [SerializeField] private float totalGameTime = 900f;
    private float currentTime;
    private NetworkVariable<float> networkCurrentTime = new NetworkVariable<float>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private float timerUIAccumulator = 0f;

    [Header("Session Seed (P2P)")]
    private NetworkVariable<int> sessionSeed = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public int SessionSeed => sessionSeed.Value;

    [Header("Shared Team Stats (P2P)")]
    private NetworkVariable<float> sharedXpMultiplier = new NetworkVariable<float>(1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public float SharedXpMultiplier => sharedXpMultiplier.Value;

    [Header("Revive Settings (P2P)")]
    [SerializeField] private float reviveRadius = 2.0f;
    [SerializeField] private float reviveTime = 5.0f;
    private Dictionary<ulong, bool> playerAlive = new Dictionary<ulong, bool>();
    private Dictionary<ulong, float> reviveProgress = new Dictionary<ulong, float>();

    [Header("Revive VFX")]
    [SerializeField] private Sprite reviveSprite;
    [SerializeField] private Color reviveColor = Color.white;
    [SerializeField] private float reviveVfxDuration = 0.8f;
    [SerializeField] private float reviveVfxYOffset = 2.0f;
    [SerializeField] private string reviveVfxSortingLayer = "VFX";

    [Header("General Difficulty Settings")]
    [SerializeField] private float mpPerPlayerMultiplier = 1.5f;
    private NetworkVariable<float> mpDifficultyMultiplier = new NetworkVariable<float>(1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public float MultiplayerDifficultyMultiplier => mpDifficultyMultiplier.Value;

    private float baseEnemyHealthMultiplier = 1f;
    private float baseEnemyDamageMultiplier = 1f;
    public float currentEnemyHealthMultiplier => baseEnemyHealthMultiplier * mpDifficultyMultiplier.Value;
    public float currentEnemyDamageMultiplier => baseEnemyDamageMultiplier * mpDifficultyMultiplier.Value;
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

    public void HandlePlayAgain()
    {
        Debug.Log($"[GameManager] HandlePlayAgain() called - isP2P: {isP2P}, IsServer: {(isP2P ? IsServer : "N/A")}");
        
        if (!isP2P)
        {
            // Single-player: use soft restart instead of scene reload
            Debug.Log("[GameManager] Single-player 'Play Again' clicked. Performing soft restart.");
            Time.timeScale = 1f;
            
            try
            {
                uiManager?.ShowEndGamePanel(false);
                SoftResetSinglePlayerWorld();
                
                // Ensure loadout selections are valid
                LoadoutSelections.EnsureValidDefaults();
                if (LoadoutSelections.SelectedCharacterPrefab != null)
                {
                    SetChosenPlayerPrefab(LoadoutSelections.SelectedCharacterPrefab);
                }
                
                StartGame();
                Debug.Log("[GameManager] Soft restart completed successfully.");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[GameManager] Soft restart failed: {ex.Message}. Falling back to scene reload.");
                SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
            }
            return;
        }

        // In multiplayer, use soft reset to avoid scene transitions (which destroy NetworkManager)
        if (IsServer)
        {
            Debug.Log("[GameManager] Host 'Play Again' clicked. Performing soft reset and returning to lobby UI.");
            Server_SoftResetGame();
        }
        else
        {
            Debug.Log("[GameManager] Client 'Play Again' clicked. Notifying host for restart...");
            // Show feedback to client that they're waiting for host
            if (uiManager != null)
            {
                uiManager.ShowEndGamePanel(false);
            }
            // Send notification to host that client wants to restart
            NotifyHostRestartRequestServerRpc();
        }
    }

    // New method to handle returning to lobby in multiplayer
    private void ReturnToLobby_Host()
    {
        if (!IsServer)
        {
            Debug.LogWarning("[GameManager] ReturnToLobby_Host called on non-server!");
            return;
        }

        var nsm = NetworkManager.Singleton?.SceneManager;
        if (nsm == null)
        {
            Debug.LogError("[GameManager] NetworkSceneManager missing. Cannot return to lobby.");
            return;
        }

        Debug.Log("[GameManager] Host returning all players to lobby...");
        
        // Find SettingsManager to get lobby scene name
        var settings = FindObjectOfType<SettingsManager>();
        string lobbyScene = settings?.lobbySceneName ?? "P2P";
        
        Debug.Log($"[GameManager] SettingsManager found: {settings != null}, Lobby scene name: {lobbyScene}");
        
        // Verify scene exists in build
        int sceneIndex = SceneManager.GetSceneByName(lobbyScene).buildIndex;
        if (sceneIndex < 0)
        {
            Debug.LogError($"[GameManager] Lobby scene '{lobbyScene}' not found in build settings! Please add it to File > Build Settings > Scenes in Build");
            return;
        }
        
        // Subscribe to scene load event to spawn lobby manager
        nsm.OnLoadEventCompleted += HandleLobbySceneLoadedForRestart;
        
        // Load the lobby scene for all clients
        nsm.LoadScene(lobbyScene, LoadSceneMode.Single);
        
        Debug.Log($"[GameManager] Loading lobby scene: {lobbyScene}");
    }

    [ServerRpc(RequireOwnership = false)]
    private void NotifyHostRestartRequestServerRpc(ServerRpcParams rpcParams = default)
    {
        ulong clientId = rpcParams.Receive.SenderClientId;
        Debug.Log($"[GameManager] Client {clientId} requested a restart. Host can decide when to return to lobby.");
        // Host could show a notification or count ready players
        // For now, we'll just log it. Could extend to show "X players want to restart" UI
    }

    private void HandleLobbySceneLoadedForRestart(string sceneName, LoadSceneMode mode, System.Collections.Generic.List<ulong> completed, System.Collections.Generic.List<ulong> timedOut)
    {
        Debug.Log($"[GameManager] Lobby scene loaded after restart: {sceneName}");
        
        // Unsubscribe to avoid multiple calls
        var nsm = NetworkManager.Singleton?.SceneManager;
        if (nsm != null)
        {
            nsm.OnLoadEventCompleted -= HandleLobbySceneLoadedForRestart;
        }
        
        // Check if we need to spawn lobby manager
        var settings = FindObjectOfType<SettingsManager>();
        if (settings != null && settings.lobbyManagerPrefab != null && IsServer)
        {
            Debug.Log("[GameManager] Spawning lobby manager after restart...");
            var lobbyManager = Instantiate(settings.lobbyManagerPrefab);
            var netObj = lobbyManager.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.Spawn();
                Debug.Log("[GameManager] Lobby manager spawned successfully");
            }
            else
            {
                Debug.LogError("[GameManager] Lobby manager prefab missing NetworkObject!");
                Destroy(lobbyManager);
            }
        }
    }

    private void Server_SoftResetGame()
    {
        if (!IsServer) return;

        Debug.Log("[GameManager] Server is performing a soft reset.");

        // Reset ability damage tracker for new run
        AbilityDamageTracker.Reset();

        // --- 1. STOP GAMEPLAY SYSTEMS ---
        if (enemySpawner != null)
        {
            enemySpawner.StopAndReset();
        }
        else
        {
            Debug.LogWarning("[GameManager] EnemySpawner reference is null. Cannot reset it.");
        }

        // --- 2. RESET THE MAP GENERATOR ---
        if (mapGenerator != null)
        {
            mapGenerator.ClearMap();
        }
        else
        {
            Debug.LogWarning("[GameManager] MapGenerator reference is null. Cannot reset it.");
        }

        // --- 3. DESTROY ALL PLAYER OBJECTS (cameras are automatically destroyed as children) ---
        if (NetworkManager.Singleton != null)
        {
            var connectedClientIds = NetworkManager.Singleton.ConnectedClientsIds.ToList();
            foreach (var clientId in connectedClientIds)
            {
                var playerObject = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId);
                if (playerObject != null)
                {
                    Debug.Log($"[GameManager] Despawning player object for client {clientId}");
                    playerObject.Despawn(true);
                }
            }
        }

        // --- 4. CLEAN UP OTHER NETWORKED OBJECTS ---
        var allNetObjects = FindObjectsOfType<NetworkObject>();
        foreach (var netObj in allNetObjects)
        {
            if (netObj.gameObject != this.gameObject && netObj.IsSpawned && !netObj.IsPlayerObject)
            {
                if (netObj.GetComponent<ProjectileWeapon>() != null || netObj.CompareTag("Boss"))
                {
                    netObj.Despawn(true);
                }
            }
        }

        // --- 5. RESET GAME STATE VARIABLES ON THE SERVER ---
        CurrentState = GameState.PreGame;
        bossSpawned = false;
        lastDifficultyIncreaseMark = 0;
        networkCurrentTime.Value = totalGameTime;
        playerAlive.Clear();
        reviveProgress.Clear();

        // --- 6. TELL ALL CLIENTS TO PERFORM THEIR LOCAL UI RESET ---
        Client_ResetToLobbyClientRpc();
        
        // --- 7. HOST RESETS UI IMMEDIATELY (while still IsHost/IsServer) ---
        Debug.Log("[GameManager] Host performing local UI reset immediately.");
        ResetLocalUIToLobby();
    }

    [ClientRpc]
    private void Client_ResetToLobbyClientRpc()
    {
        // Skip execution on host since host already executed ResetLocalUIToLobby
        if (IsHost)
        {
            Debug.Log("[GameManager] Host skipping Client_ResetToLobbyClientRpc (already executed locally).");
            return;
        }

        Debug.Log("[GameManager] Client received RPC to reset to lobby state.");
        ResetLocalUIToLobby();
    }

    private void ResetLocalUIToLobby()
    {
        CurrentState = GameState.PreGame;
        Time.timeScale = 1f;

        // Wait a frame for NetworkManager to stabilize IsHost/IsServer state after reset
        StartCoroutine(ReturnToLobbyAfterDelay());
    }

    private IEnumerator ReturnToLobbyAfterDelay()
    {
        // Wait for network state to stabilize
        yield return new WaitForEndOfFrame();
        yield return null; // Wait one more frame
        yield return new WaitForSeconds(0.5f); // Extra delay for network state
        
        Debug.Log($"[GameManager] ReturnToLobbyAfterDelay - IsHost: {IsHost}, IsServer: {IsServer}, IsClient: {IsClient}");
        
        // Tell the UIManager to switch back to the lobby/character selection UI
        if (uiManager != null)
        {
            uiManager.ShowEndGamePanel(false);
            uiManager.ReturnToLobby();
        }
        
        // Also manually refresh the LobbyUI button visibility after delay
        var lobbyUI = FindObjectOfType<LobbyUI>();
        if (lobbyUI != null && lobbyUI.gameObject.activeInHierarchy)
        {
            Debug.Log("[GameManager] Manually triggering LobbyUI.UpdateStartButtonVisibility()");
            lobbyUI.UpdateStartButtonVisibility();
        }
    }
    void Start()
    {
        if (uiManager == null) uiManager = FindObjectOfType<UIManager>();
        if (enemySpawner == null) enemySpawner = FindObjectOfType<EnemySpawner>();
        if (enemyDespawner == null) enemyDespawner = FindObjectOfType<EnemyDespawner>();
        if (upgradeManager == null) upgradeManager = FindObjectOfType<UpgradeManager>();
        if (playerExperience == null) playerExperience = FindObjectOfType<PlayerExperience>();
        if (mapGenerator == null) mapGenerator = FindObjectOfType<MapGenerator>();

        CurrentState = GameState.PreGame;
        currentTime = totalGameTime;
    }

    // =========================
    // GUARANTEED RARITY SYSTEM
    // =========================
    public void PresentGuaranteedRarityToAll(string rarityName)
    {
        if (!isP2P)
        {
            var um = upgradeManager != null ? upgradeManager : Object.FindFirstObjectByType<UpgradeManager>();
            if (um == null) return;
            var rm = um.GetRarityTiers();
            var target = rm.FirstOrDefault(r => r != null && r.name == rarityName);
            if (target != null) um.PresentGuaranteedRarityChoices(target);
            return;
        }

        if (IsServer)
        {
            PresentGuaranteedRarityToAllClientRpc(rarityName);
        }
        else
        {
            PresentGuaranteedRarityToAllServerRpc(rarityName);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void PresentGuaranteedRarityToAllServerRpc(string rarityName)
    {
        PresentGuaranteedRarityToAllClientRpc(rarityName);
    }

    [ClientRpc]
    private void PresentGuaranteedRarityToAllClientRpc(string rarityName)
    {
        var um = upgradeManager != null ? upgradeManager : Object.FindFirstObjectByType<UpgradeManager>();
        if (um == null) return;
        var tiers = um.GetRarityTiers();
        var target = tiers.FirstOrDefault(r => r != null && r.name == rarityName);
        if (target != null) { um.PresentGuaranteedRarityChoices(target); }
    }

    // =========================
    // SHARED EXPERIENCE SYSTEM
    // =========================
    public void DistributeSharedXP(float amount)
    {
        if (isP2P)
        {
            if (IsServer)
            {
                float scaled = amount * Mathf.Max(0f, sharedXpMultiplier.Value);
                ApplySharedXPClientRpc(scaled);
            }
        }
        else
        {
            var pxp = playerExperience != null ? playerExperience : Object.FindFirstObjectByType<PlayerExperience>();
            pxp?.AddXP(amount);
        }
    }

    [ClientRpc]
    private void ApplySharedXPClientRpc(float amount)
    {
        var pxp = playerExperience != null ? playerExperience : Object.FindFirstObjectByType<PlayerExperience>();
        pxp?.AddXPFromServerScaled(amount);
    }

    // =========================
    // SHARED XP MULTIPLIER CONTROL
    // =========================
    public void RequestModifySharedXpMultiplier(float delta)
    {
        if (!isP2P) return;
        if (IsServer) { ModifySharedXpMultiplierInternal(delta); }
        else { ModifySharedXpMultiplierServerRpc(delta); }
    }

    [ServerRpc(RequireOwnership = false)]
    private void ModifySharedXpMultiplierServerRpc(float delta)
    {
        ModifySharedXpMultiplierInternal(delta);
    }

    private void ModifySharedXpMultiplierInternal(float delta)
    {
        sharedXpMultiplier.Value = Mathf.Max(0f, sharedXpMultiplier.Value + delta);
    }

    // =========================
    // TEAM-WIDE AREA (PROJECTILE SIZE) SHARING
    // =========================
    public void TeamApplyAreaUpgrade(float delta)
    {
        if (!isP2P)
        {
            var ps = Object.FindFirstObjectByType<PlayerStats>();
            ps?.IncreaseProjectileSizeMultiplier(delta);
            return;
        }

        if (IsServer)
        {
            ApplyAreaToAllPlayersOnServer(delta);
            ApplyTeamAreaDeltaClientRpc(delta);
        }
        else
        {
            TeamApplyAreaUpgradeServerRpc(delta);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void TeamApplyAreaUpgradeServerRpc(float delta)
    {
        ApplyAreaToAllPlayersOnServer(delta);
        ApplyTeamAreaDeltaClientRpc(delta);
    }

    private void ApplyAreaToAllPlayersOnServer(float delta)
    {
        if (NetworkManager.Singleton == null) return;
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client?.PlayerObject == null) continue;
            var stats = client.PlayerObject.GetComponent<PlayerStats>();
            if (stats != null)
            {
                stats.IncreaseProjectileSizeMultiplier(delta);
            }
        }
    }

    [ClientRpc]
    private void ApplyTeamAreaDeltaClientRpc(float delta)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient?.PlayerObject != null)
        {
            var stats = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerStats>();
            stats?.IncreaseProjectileSizeMultiplier(delta);
        }
    }

    #region Network Spawn/Despawn & Starting Weapon Logic
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        Debug.Log("[GameManager] OnNetworkSpawn has been called!");

        if (IsServer)
        {
            Debug.Log("[GameManager] This instance is the SERVER. Initializing server-side logic.");

            // Gerar session seed se ainda não existir
            if (sessionSeed.Value == 0)
            {
                var rnd = new System.Random(System.Environment.TickCount ^ UnityEngine.Random.Range(int.MinValue, int.MaxValue));
                sessionSeed.Value = rnd.Next(int.MinValue, int.MaxValue);
            }

            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnected;

            // Handle host player que já está conectado quando o servidor inicia
            HandleClientConnected(NetworkManager.Singleton.LocalClientId);
        }

        // Subscribe to timer changes (para clientes sincronizarem)
        networkCurrentTime.OnValueChanged += (prev, next) => UpdateTimerUI(next);
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        if (IsServer)
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnected;
            }
        }
    }

    private void HandleClientConnected(ulong clientId)
    {
        Debug.Log($"[GameManager] Handling connection for new Client ID: {clientId}");

        if (isP2P && IsServer)
        {
            if (!playerAlive.ContainsKey(clientId)) playerAlive[clientId] = true;
            if (!reviveProgress.ContainsKey(clientId)) reviveProgress[clientId] = 0f;
            RecomputeMultiplayerDifficulty();
        }

        StartCoroutine(GiveStartingWeaponAfterPlayerSpawns(clientId));
    }

    private void HandleClientDisconnected(ulong clientId)
    {
        if (isP2P && IsServer)
        {
            if (playerAlive.ContainsKey(clientId)) playerAlive.Remove(clientId);
            if (reviveProgress.ContainsKey(clientId)) reviveProgress.Remove(clientId);
            RecomputeMultiplayerDifficulty();
        }
    }

    private IEnumerator GiveStartingWeaponAfterPlayerSpawns(ulong clientId)
    {
        NetworkObject playerNetworkObject = null;

        while (playerNetworkObject == null)
        {
            yield return null;
            if (NetworkManager.Singleton.SpawnManager != null)
            {
                playerNetworkObject = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId);
            }
        }

        Debug.Log($"[GameManager] Found player object for Client ID: {clientId}. Getting PlayerWeaponManager.");

        PlayerWeaponManager pwm = playerNetworkObject.GetComponent<PlayerWeaponManager>();
        var sync = playerNetworkObject.GetComponent<LoadoutSync>();
        if (sync == null) playerNetworkObject.gameObject.AddComponent<LoadoutSync>();

        if (pwm != null)
        {
            Debug.Log($"[GameManager] Calling Server_GiveStartingWeapon on player for Client ID: {clientId}.");
            pwm.Server_GiveStartingWeapon();
        }
        else
        {
            Debug.LogError($"[GameManager] FATAL: Player object for Client {clientId} is missing a PlayerWeaponManager component!");
        }
    }
    #endregion

    void Update()
    {
        HandleInput();

        if (isP2P && !IsServer)
        {
            // Clientes só atualizam a UI, não o timer
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

    public void SetChosenPlayerPrefab(GameObject playerPrefab)
    {
        if (isP2P) return;
        chosenPlayerPrefab = playerPrefab;
    }

    public void SetPlayerSelections_P2P(Dictionary<ulong, GameObject> selections)
    {
        if (!isP2P) return;
        playerUnitSelections = selections;
    }

    // === MÉTODO PÚBLICO PARA INICIAR O JOGO (chamado pelo ConnectionManager/UI) ===
    public void StartGame()
    {
        if (CurrentState == GameState.Playing) return;

        if (isP2P)
        {
            if (IsHost) StartGame_P2P_Host();
        }
        else
        {
            StartGame_SinglePlayer();
        }
    }

    // Performs an in-place cleanup of the singleplayer world so StartGame() behaves like a fresh run
    public void SoftResetSinglePlayerWorld()
    {
        if (isP2P) return;

        Time.timeScale = 1f;
        uiManager?.ShowEndGamePanel(false);

        // Reset ability damage tracker for new run
        AbilityDamageTracker.Reset();

        // --- 1. Clear all players (cameras are automatically destroyed as children) ---
        try { 
            var players = GameObject.FindGameObjectsWithTag("Player"); 
            foreach (var p in players) 
                if (p != null) Destroy(p); 
        } catch { }

        // --- 2. Clear all enemies ---
        try { 
            var enemies = FindObjectsByType<EnemyStats>(FindObjectsSortMode.None); 
            foreach (var e in enemies) 
                if (e != null && e.gameObject != null) Destroy(e.gameObject); 
        } catch { }

        // --- 3. Clear all projectiles and weapons ---
        try { 
            var projs = FindObjectsByType<ProjectileWeapon>(FindObjectsSortMode.None); 
            foreach (var p in projs) 
                if (p != null && p.gameObject != null) Destroy(p.gameObject); 
        } catch { }
        try { 
            var orbiters = FindObjectsByType<OrbitingWeapon>(FindObjectsSortMode.None); 
            foreach (var ow in orbiters) 
                if (ow != null && ow.gameObject != null) Destroy(ow.gameObject); 
        } catch { }
        try { 
            var auras = FindObjectsByType<AuraWeapon>(FindObjectsSortMode.None); 
            foreach (var a in auras) 
                if (a != null && a.gameObject != null) Destroy(a.gameObject); 
        } catch { }

        // --- 4. Clear all pickups and UI elements ---
        try { 
            var pops = FindObjectsByType<DamagePopup>(FindObjectsSortMode.None); 
            foreach (var dp in pops) 
                if (dp != null && dp.gameObject != null) Destroy(dp.gameObject); 
        } catch { }
        try { 
            var orbs = FindObjectsByType<ExperienceOrb>(FindObjectsSortMode.None); 
            foreach (var o in orbs) 
                if (o != null && o.gameObject != null) Destroy(o.gameObject); 
        } catch { }

        // --- 5. Clear the generated map ---
        try { 
            if (mapGenerator != null) 
                mapGenerator.ClearMap(); 
        } catch { }

        // --- 6. Stop coroutines ---
        try { enemyDespawner?.StopAllCoroutines(); } catch { }

        // Reset internal state
        CurrentState = GameState.PreGame;
        bossSpawned = false;
        lastDifficultyIncreaseMark = 0;

        try { playerExperience?.ResetState(); } catch { }
    }

    private void StartGame_SinglePlayer()
    {
        if (chosenPlayerPrefab == null)
        {
            // Primeiro tenta usar LoadoutSelections (garante defaults válidos)
            LoadoutSelections.EnsureValidDefaults();
            if (LoadoutSelections.SelectedCharacterPrefab != null)
            {
                chosenPlayerPrefab = LoadoutSelections.SelectedCharacterPrefab;
                Debug.Log("[GameManager] Using character from LoadoutSelections: " + chosenPlayerPrefab.name);
            }
            // Try to get first valid player prefab from LoadoutPanel.characters
            else
            {
                var loadoutPanel = FindObjectOfType<LoadoutPanel>();
                if (loadoutPanel != null && loadoutPanel.characters != null && loadoutPanel.characters.Count > 0)
                {
                    var firstValid = loadoutPanel.characters.FirstOrDefault(c => c != null && c.playerPrefab != null);
                    if (firstValid != null && firstValid.playerPrefab != null)
                    {
                        chosenPlayerPrefab = firstValid.playerPrefab;
                        Debug.LogWarning("[GameManager] No player prefab selected, using first character prefab from LoadoutPanel: " + chosenPlayerPrefab.name);
                    }
                }
            }
            // If still null, fallback to runtimeNetworkPrefabs
            if (chosenPlayerPrefab == null && runtimeNetworkPrefabs != null && runtimeNetworkPrefabs.Count > 0)
            {
                chosenPlayerPrefab = runtimeNetworkPrefabs[0];
                Debug.LogWarning("[GameManager] No player prefab selected, using default from runtimeNetworkPrefabs: " + chosenPlayerPrefab.name);
            }
            if (chosenPlayerPrefab == null)
            {
                Debug.LogError("StartGame SP: Nenhum prefab de jogador foi escolhido e nenhum default está disponível!");
                return;
            }
        }
        GameObject playerObject = Instantiate(chosenPlayerPrefab, playerSpawnPoint.position, playerSpawnPoint.rotation);
        InitializeAllSystems(playerObject);
        // AdvancedCameraController is now permanently disabled - both SP and MP use prefab camera
    }

    private void StartGame_P2P_Host()
    {

        // If no selections, fallback: assign default prefab to all connected players
        if (playerUnitSelections.Count == 0)
        {
            Debug.LogWarning("StartGame P2P: Dicionário de seleções de jogadores está vazio! Usando prefab padrão para todos os jogadores conectados.");
            if (NetworkManager.Singleton != null)
            {
                foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
                {
                    GameObject defaultPrefab = null;
                    if (LoadoutSelections.CharacterPrefabsContext != null && LoadoutSelections.CharacterPrefabsContext.Count > 0)
                    {
                        defaultPrefab = LoadoutSelections.CharacterPrefabsContext[0];
                    }
                    else if (runtimeNetworkPrefabs != null && runtimeNetworkPrefabs.Count > 0)
                    {
                        defaultPrefab = runtimeNetworkPrefabs[0];
                    }
                    if (defaultPrefab == null)
                    {
                        Debug.LogError($"[GameManager] Nenhum prefab padrão disponível para o cliente {client.ClientId}!");
                        continue;
                    }
                    GameObject playerObject = Instantiate(defaultPrefab, playerSpawnPoint.position, playerSpawnPoint.rotation);
                    playerObject.GetComponent<NetworkObject>().SpawnAsPlayerObject(client.ClientId);
                    playerAlive[client.ClientId] = true;
                }
                InitializeGameClientRpc();
            }
            return;
        }

        foreach (var entry in playerUnitSelections)
        {
            GameObject prefabToUse = null;
            // Always try to get the synced character index for this player
            if (LoadoutSelections.CharacterPrefabsContext != null && LoadoutSync.TryGetSelectionFor(entry.Key, out var sel) && sel.characterIndex >= 0)
            {
                if (sel.characterIndex < LoadoutSelections.CharacterPrefabsContext.Count && LoadoutSelections.CharacterPrefabsContext[sel.characterIndex] != null)
                {
                    prefabToUse = LoadoutSelections.CharacterPrefabsContext[sel.characterIndex];
                }
            }
            // Fallback: use entry.Value (legacy), or first prefab in context
            if (prefabToUse == null)
            {
                prefabToUse = entry.Value;
                if ((prefabToUse == null || (prefabToUse != null && prefabToUse.name == "")) && LoadoutSelections.CharacterPrefabsContext != null && LoadoutSelections.CharacterPrefabsContext.Count > 0)
                {
                    prefabToUse = LoadoutSelections.CharacterPrefabsContext[0];
                }
            }
            if (prefabToUse == null)
            {
                Debug.LogError($"[GameManager] No valid prefab found for client {entry.Key}! Skipping spawn.");
                continue;
            }
            GameObject playerObject = Instantiate(prefabToUse, playerSpawnPoint.position, playerSpawnPoint.rotation);
            playerObject.GetComponent<NetworkObject>().SpawnAsPlayerObject(entry.Key);
            playerAlive[entry.Key] = true;
        }

        InitializeGameClientRpc();
    }

    [ClientRpc]
    private void InitializeGameClientRpc()
    {
        if (NetworkManager.Singleton.LocalClient.PlayerObject != null)
        {
            GameObject localPlayerObject = NetworkManager.Singleton.LocalClient.PlayerObject.gameObject;
            InitializeAllSystems(localPlayerObject);
        }
    }

    // In GameManager.cs
    // Replace your entire InitializeAllSystems method with this one.

    // In GameManager.cs

    private void InitializeAllSystems(GameObject playerObject)
    {
        // This method runs on EVERY client and the host after their player object is ready.

        // --- Your existing initializations ---
        if (playerObject != null)
        {
            localPlayer = playerObject.GetComponent<Movement>();
            playerExperience?.Initialize(playerObject);
            upgradeManager?.Initialize(playerObject);
            enemyDespawner?.Initialize(playerObject);

            if (playerObject.GetComponent<ApplyRunesOnSpawn>() == null)
            {
                playerObject.AddComponent<ApplyRunesOnSpawn>();
            }

            // Camera is now part of the player prefab, no need to instantiate separately
            // Singleplayer: Uses AdvancedCameraController in the scene (if needed)
            // Multiplayer: Each player prefab has its own camera as a child
        }

        // --- CORRECTED MAP GENERATION LOGIC ---
        if (mapGenerator != null)
        {
            if (isP2P)
            {
                // In multiplayer, the server is the authority that tells everyone to generate the map.
                if (IsServer)
                {
                    GenerateMapClientRpc();
                }
            }
            else
            {
                // In single-player, we just generate the map directly.
                // It calls the single, unified GenerateMap() method.
                 mapGenerator.GenerateMap();
            }
        }
        else
        {
            Debug.LogError("[GameManager] MapGenerator reference is null!");
        }

        // --- The rest of your initialization logic ---
        RegisterRuntimePrefabs();

        // Spawning logic: Always spawn in singleplayer, only server spawns in multiplayer
        if (!isP2P || IsServer)
        {
            enemySpawner?.StartSpawning();
            Debug.Log($"[GameManager] StartSpawning called (isP2P={isP2P}, IsServer={IsServer})");
        }

        CurrentState = GameState.Playing;
        bossSpawned = false;
        lastDifficultyIncreaseMark = 0;

        if (IsServer)
        {
            networkCurrentTime.Value = totalGameTime;
            RecomputeMultiplayerDifficulty();
        }

        baseEnemyHealthMultiplier = 1f;
        baseEnemyDamageMultiplier = 1f;
        currentProjectileSpeed = baseProjectileSpeed;
        currentFireRate = baseFireRate;
        currentSightRange = baseSightRange;

        if (uiManager) uiManager.OnGameStart();
    }



    [ClientRpc]
    private void GenerateMapClientRpc()
    {
        Debug.Log($"[GameManager] Client {NetworkManager.Singleton.LocalClientId} received RPC to generate map.");
        if (mapGenerator != null)
        {
            // Everyone calls the same, unified function.
            // The MapGenerator's internal logic will handle what to spawn for server vs client.
            mapGenerator.GenerateMap();
        }
        else
        {
            Debug.LogError($"[GameManager] MapGenerator is null on this client. Cannot generate map.");
        }
    }


    // === REGISTRO DE PREFABS (método da versão 1 - mais robusto) ===
    private void RegisterRuntimePrefabs()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening) return;
        if (runtimeNetworkPrefabs == null || runtimeNetworkPrefabs.Count == 0) return;

        foreach (var prefab in runtimeNetworkPrefabs)
        {
            RuntimeNetworkPrefabRegistry.TryRegister(prefab);
        }
    }

    private void EnsureAllMapConsumablesSpawned()
    {
        if (!isP2P || !IsServer) return;
        var all = Object.FindObjectsByType<MapConsumable>(FindObjectsSortMode.None);
        int spawned = 0;
        foreach (var mc in all)
        {
            if (mc == null) continue;
            var no = mc.GetComponent<NetworkObject>() ?? mc.gameObject.AddComponent<NetworkObject>();
            if (!no.IsSpawned)
            {
                no.Spawn(true);
                spawned++;
            }
        }
        if (spawned > 0)
        {
            Debug.Log($"[GameManager] Network-spawned {spawned} MapConsumable(s) so clients can interact and observe state.");
        }
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
        GameOverClientRpc();
    }

    // New P2P downed flow entrypoint used by PlayerStats
    public void PlayerDowned(ulong clientId)
    {
        if (!isP2P)
        {
            GameOver();
            return;
        }
        if (IsServer)
        {
            SetPlayerDownedInternal(clientId);
        }
        else
        {
            PlayerDownedServerRpc(clientId);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void PlayerDownedServerRpc(ulong clientId)
    {
        SetPlayerDownedInternal(clientId);
    }

    private void SetPlayerDownedInternal(ulong clientId)
    {
        // Ensure the server is tracking all connected players
        if (NetworkManager.Singleton != null)
        {
            foreach (var c in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (!playerAlive.ContainsKey(c.ClientId)) playerAlive[c.ClientId] = true;
            }
        }

        playerAlive[clientId] = false;
        reviveProgress[clientId] = 0f;

        // Broadcast downed visuals to all clients
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var downedClient) && downedClient.PlayerObject != null)
        {
            SetDownedVisualClientRpc(downedClient.PlayerObject.NetworkObjectId, true);
        }

        // If everyone is downed, end the game
        bool anyAlive = false;
        foreach (var kv in playerAlive)
        {
            if (kv.Value) { anyAlive = true; break; }
        }
        if (!anyAlive)
        {
            GameOverClientRpc();
        }
    }

    private void GameOver()
    {
        if (CurrentState == GameState.GameOver) return;
        CurrentState = GameState.GameOver;
        if (localPlayer != null) localPlayer.enabled = false;
        Time.timeScale = 0f; // Stop the game completely

        if (!isP2P || IsServer)
        {
            AbilityDamageTracker.LogTotals();
        }

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
        Debug.Log("[GameManager] RestartGame() called. Redirecting to HandlePlayAgain() for unified behavior.");
        HandlePlayAgain();
    }

    public void RequestPause(bool showMenu = false)
    {
        if (isP2P || CurrentState != GameState.Playing) return;

        CurrentState = GameState.Paused;
        Time.timeScale = 0f;
        if (localPlayer != null) localPlayer.enabled = false;

        if (showMenu)
            StartCoroutine(ShowPauseMenuNextFrame());
    }

    public void RequestPauseForLevelUp()
    {
        if (!isP2P)
        {
            RequestPause(false);
            return;
        }
        if (IsServer)
        {
            SetPausedClientRpc(true);
        }
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

    public void ResumeAfterLevelUp()
    {
        if (!isP2P)
        {
            RequestResume();
            return;
        }
        if (IsServer)
        {
            SetPausedClientRpc(false);
        }
    }

    [ClientRpc]
    private void SetPausedClientRpc(bool paused)
    {
        if (paused)
        {
            CurrentState = GameState.Paused;
            Time.timeScale = 0f;
            if (localPlayer != null) localPlayer.enabled = false;
        }
        else
        {
            CurrentState = GameState.Playing;
            Time.timeScale = 1f;
            if (localPlayer != null) localPlayer.enabled = true;
            uiManager?.ShowPauseMenu(false);
        }
    }

    private void UpdateTimer()
    {
        // Server-side revive tracking while playing (P2P only)
        if (isP2P && IsServer && CurrentState == GameState.Playing)
        {
            ServerUpdateRevives();
        }

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

    private void ServerUpdateRevives()
    {
        if (NetworkManager.Singleton == null) return;
        var nm = NetworkManager.Singleton;

        var downedIds = new List<ulong>();
        foreach (var kvp in playerAlive)
        {
            if (!kvp.Value)
            {
                downedIds.Add(kvp.Key);
            }
        }

        foreach (var downedId in downedIds)
        {
            if (!nm.ConnectedClients.TryGetValue(downedId, out var downedClient) || downedClient.PlayerObject == null) continue;
            Transform downedTransform = downedClient.PlayerObject.transform;

            bool rescuerPresent = false;
            foreach (var rescuer in nm.ConnectedClientsList)
            {
                if (rescuer.ClientId == downedId) continue;
                if (!playerAlive.ContainsKey(rescuer.ClientId) || !playerAlive[rescuer.ClientId]) continue;
                if (rescuer.PlayerObject == null) continue;
                float dist = Vector3.Distance(downedTransform.position, rescuer.PlayerObject.transform.position);
                if (dist <= reviveRadius)
                {
                    rescuerPresent = true;
                    break;
                }
            }

            if (rescuerPresent)
            {
                float prog = 0f;
                reviveProgress.TryGetValue(downedId, out prog);
                prog += Time.deltaTime;
                if (prog >= reviveTime)
                {
                    var ps = downedClient.PlayerObject.GetComponent<PlayerStats>();
                    if (ps != null)
                    {
                        ps.ServerReviveToFixedHp(10);
                        playerAlive[downedId] = true;
                        reviveProgress.Remove(downedId);
                        PlayReviveVFXClientRpc(downedClient.PlayerObject.NetworkObjectId);
                        SetDownedVisualClientRpc(downedClient.PlayerObject.NetworkObjectId, false);
                    }
                }
                else
                {
                    reviveProgress[downedId] = prog;
                }
            }
            else
            {
                reviveProgress[downedId] = 0f;
            }
        }

        // If after revives all are down, trigger game over
        bool anyAlive = false;
        foreach (var kv in playerAlive)
        {
            if (kv.Value) { anyAlive = true; break; }
        }
        if (!anyAlive)
        {
            GameOverClientRpc();
        }
    }

    [ClientRpc]
    private void PlayReviveVFXClientRpc(ulong playerNetId)
    {
        if (NetworkManager.Singleton == null) return;
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerNetId, out var netObj))
        {
            var pos = netObj.transform.position;
            Sprite spriteToUse = reviveSprite;
            string sortingLayer = reviveVfxSortingLayer;
            var sr = netObj.GetComponentInChildren<SpriteRenderer>();
            if (spriteToUse == null && sr != null)
            {
                spriteToUse = sr.sprite;
            }
            if (string.IsNullOrEmpty(sortingLayer) && sr != null)
            {
                sortingLayer = sr.sortingLayerName;
            }
            if (spriteToUse == null) return;
            ReviveVFX.Spawn(pos, spriteToUse, reviveColor, reviveVfxDuration, 0.8f, 1.3f, reviveVfxYOffset, sortingLayer);
        }
    }

    public void TriggerReviveVFXForPlayer(ulong playerNetId)
    {
        if (!isP2P) return;
        if (IsServer)
        {
            PlayReviveVFXClientRpc(playerNetId);
        }
    }

    [ClientRpc]
    private void SetDownedVisualClientRpc(ulong playerNetId, bool isDowned)
    {
        if (NetworkManager.Singleton == null) return;
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(playerNetId, out var netObj) || netObj == null) return;
        var ps = netObj.GetComponent<PlayerStats>();
        var sr = netObj.GetComponentInChildren<SpriteRenderer>();
        if (sr == null || ps == null) return;

        if (isDowned)
        {
            ps.ClientApplyDownedState();
            var downed = ps.DownedSprite;
            if (downed != null) sr.sprite = downed;
            sr.sortingLayerName = "MAPCOSMETIC";
            var auras = netObj.GetComponentsInChildren<AuraWeapon>(true);
            foreach (var aura in auras)
            {
                if (aura != null) aura.SetVisualsActive(false);
            }
        }
        else
        {
            ps.ClientApplyRevivedState();
            var orig = ps.OriginalSprite;
            if (orig != null) sr.sprite = orig;
            var origLayer = ps.OriginalSortingLayer;
            if (!string.IsNullOrEmpty(origLayer)) sr.sortingLayerName = origLayer;
            var auras = netObj.GetComponentsInChildren<AuraWeapon>(true);
            foreach (var aura in auras)
            {
                if (aura != null) aura.SetVisualsActive(true);
            }
        }
    }

    // === ATUALIZAÇÃO DE UI DO TIMER (mantida frequência original de 1 segundo) ===
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

        if (isP2P && IsServer)
        {
            ServerTeleportPlayersToBossPoint();
        }

        GameObject bossObject = Instantiate(bossPrefab, bossSpawnPoint.position + Vector3.up * 10f, bossSpawnPoint.rotation);

        if (isP2P && IsHost)
        {
            bossObject.GetComponent<NetworkObject>().Spawn(true);
        }

        reaperStats = bossObject.GetComponent<EnemyStats>();
        Debug.Log("O Boss Final apareceu!");
    }

    private void ServerTeleportPlayersToBossPoint()
    {
        if (!isP2P || !IsServer) return;
        var nm = NetworkManager.Singleton;
        if (nm == null || bossSpawnPoint == null) return;

        var clients = nm.ConnectedClientsList;
        int count = Mathf.Max(1, clients.Count);
        float radius = 4.0f;
        Vector3 center = bossSpawnPoint.position;

        for (int i = 0; i < clients.Count; i++)
        {
            var client = clients[i];
            float angle = (count > 1) ? (i * Mathf.PI * 2f / count) : 0f;
            Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
            Vector3 dest = center + offset;

            var rpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { client.ClientId } }
            };
            TeleportToPositionClientRpc(dest, rpcParams);
        }
    }

    [ClientRpc]
    private void TeleportToPositionClientRpc(Vector3 destination, ClientRpcParams clientRpcParams = default)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || nm.LocalClient == null || nm.LocalClient.PlayerObject == null) return;
        var playerObj = nm.LocalClient.PlayerObject;

        var rb = playerObj.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 current = rb.position;
            rb.position = new Vector3(destination.x, current.y, destination.z);
            rb.linearVelocity = Vector3.zero;
        }
        else
        {
            Vector3 current = playerObj.transform.position;
            playerObj.transform.position = new Vector3(destination.x, current.y, destination.z);
        }
    }

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
        baseEnemyHealthMultiplier *= generalStrengthMultiplier;
        baseEnemyDamageMultiplier *= generalStrengthMultiplier;
        currentProjectileSpeed *= speedMultiplier;
        currentFireRate = Mathf.Max(0.2f, currentFireRate / fireRateMultiplier);
        Debug.Log($"Dificuldade Aumentada (x{lastDifficultyIncreaseMark}) | HP×{currentEnemyHealthMultiplier:F2}, DMG×{currentEnemyDamageMultiplier:F2}");
    }

    // =========================
    // MULTIPLAYER DIFFICULTY SCALING (P2P)
    // =========================
    private void RecomputeMultiplayerDifficulty()
    {
        if (!IsServer)
            return;

        if (!isP2P)
        {
            mpDifficultyMultiplier.Value = 1f;
            return;
        }

        int playerCount = 1;
        if (NetworkManager.Singleton != null)
        {
            playerCount = Mathf.Max(1, NetworkManager.Singleton.ConnectedClients.Count);
        }

        float factor = Mathf.Pow(Mathf.Max(1.0f, mpPerPlayerMultiplier), playerCount - 1);
        mpDifficultyMultiplier.Value = factor;
        Debug.Log($"[GameManager] MP Difficulty recalculated: players={playerCount}, factor={factor:F2}");
    }

    public float GetRemainingTime()
    {
        return isP2P ? networkCurrentTime.Value : currentTime;
    }

    public float GetTotalGameTime()
    {
        return totalGameTime;
    }

    // =========================
    // SERVER-AUTHORITATIVE PLAYER DAMAGE (P2P)
    // =========================
    public void ServerApplyPlayerDamage(ulong targetClientId, float amount, Vector3? hitFromWorldPos = null, float? customIFrameDuration = null)
    {
        if (!isP2P)
        {
            var ps = Object.FindFirstObjectByType<PlayerStats>();
            ps?.ApplyDamage(amount, hitFromWorldPos, customIFrameDuration);
            return;
        }

        if (!IsServer) return;

        if (NetworkManager.Singleton == null) return;
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(targetClientId, out var client) || client.PlayerObject == null) return;

        var targetStats = client.PlayerObject.GetComponent<PlayerStats>();
        if (targetStats != null)
        {
            targetStats.ApplyDamage(amount, hitFromWorldPos, customIFrameDuration);

            var rpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { targetClientId } }
            };
            ApplyDamageToLocalPlayerClientRpc(amount, hitFromWorldPos.HasValue ? hitFromWorldPos.Value : Vector3.zero, customIFrameDuration.HasValue ? customIFrameDuration.Value : -1f, rpcParams);
        }
    }

    [ClientRpc]
    private void ApplyDamageToLocalPlayerClientRpc(float amount, Vector3 hitFromWorldPos, float iFrameDuration, ClientRpcParams clientRpcParams = default)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClient != null && NetworkManager.Singleton.LocalClient.PlayerObject != null)
        {
            var stats = NetworkManager.Singleton.LocalClient.PlayerObject.GetComponent<PlayerStats>();
            if (stats != null)
            {
                float? iframeOpt = (iFrameDuration >= 0f) ? iFrameDuration : (float?)null;
                stats.ApplyDamage(amount, hitFromWorldPos, iframeOpt);
            }
        }
    }
}