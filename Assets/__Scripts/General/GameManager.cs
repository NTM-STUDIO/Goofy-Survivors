using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;

public class GameManager : NetworkBehaviour // Must be a NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Mode Settings")]
    public bool isP2P = false;

    [Header("Core Manager References")]
    [SerializeField] private UIManager uiManager;
    [SerializeField] private EnemySpawner enemySpawner;
    [SerializeField] private EnemyDespawner enemyDespawner;
    [SerializeField] private UpgradeManager upgradeManager;
    [SerializeField] private PlayerExperience playerExperience;
    [SerializeField] private MapGenerator mapGenerator;

    private Movement localPlayer;
    private GameObject chosenPlayerPrefab;

    [Header("Prefabs & Spawn Points")]
    [SerializeField] private Transform playerSpawnPoint;
    [SerializeField] private GameObject bossPrefab;
    [SerializeField] private Transform bossSpawnPoint;

    private Dictionary<ulong, GameObject> playerUnitSelections = new Dictionary<ulong, GameObject>();

    public enum GameState { PreGame, Playing, Paused, GameOver }
    public GameState CurrentState { get; private set; }

    [Header("Timer Settings")]
    [SerializeField] private float totalGameTime = 900f;
    private float currentTime;
    private NetworkVariable<float> networkCurrentTime = new NetworkVariable<float>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private float timerUIAccumulator = 0f;

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

    // =========================
    // SHARED EXPERIENCE SYSTEM
    // =========================
    public void DistributeSharedXP(float amount)
    {
        // Server entrypoint: broadcast XP to everyone
        if (isP2P)
        {
            if (IsServer)
            {
                // Scale once on server using shared team multiplier
                float scaled = amount * Mathf.Max(0f, sharedXpMultiplier.Value);
                ApplySharedXPClientRpc(scaled);
            }
        }
        else
        {
            // Single player local
            var pxp = playerExperience != null ? playerExperience : FindObjectOfType<PlayerExperience>();
            pxp?.AddXP(amount);
        }
    }

    [ClientRpc]
    private void ApplySharedXPClientRpc(float amount)
    {
        var pxp = playerExperience != null ? playerExperience : FindObjectOfType<PlayerExperience>();
        // This amount is already scaled on the server by the shared team multiplier
        pxp?.AddXPFromServerScaled(amount);
    }

    // =========================
    // SHARED XP MULTIPLIER CONTROL
    // =========================
    public void RequestModifySharedXpMultiplier(float delta)
    {
        if (!isP2P)
        {
            // Single-player: mirror local stat only (PlayerStats handles its own field)
            return;
        }
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
            // Single-player: apply directly
            var ps = FindObjectOfType<PlayerStats>();
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

    #region Starting Weapon Logic
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += HandleClientConnected;
            // Handle the host player who is already connected when the server starts
            HandleClientConnected(NetworkManager.Singleton.LocalClientId);
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        if (IsServer)
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
            }
        }
    }

    private void HandleClientConnected(ulong clientId)
    {
        Debug.Log($"[GameManager] Handling connection for new Client ID: {clientId}");
        // In P2P, track newly connected players as alive on the server
        if (isP2P && IsServer)
        {
            if (!playerAlive.ContainsKey(clientId)) playerAlive[clientId] = true;
            if (!reviveProgress.ContainsKey(clientId)) reviveProgress[clientId] = 0f;
        }
        StartCoroutine(GiveStartingWeaponAfterPlayerSpawns(clientId));
    }

    private IEnumerator GiveStartingWeaponAfterPlayerSpawns(ulong clientId)
    {
        NetworkObject playerNetworkObject = null;
        // Keep looping until the player's object is officially spawned and registered.
        while (playerNetworkObject == null)
        {
            yield return null; // Wait one frame before checking again
            if (NetworkManager.Singleton.SpawnManager != null)
            {
                playerNetworkObject = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId);
            }
        }

        Debug.Log($"[GameManager] Found player object for Client ID: {clientId}. Getting PlayerWeaponManager.");

        PlayerWeaponManager pwm = playerNetworkObject.GetComponent<PlayerWeaponManager>();
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
        // The IsOwner check is only needed for scripts on player objects.
        // For a singleton GameManager, we just need to check if we are the host/server.
        // We can leave HandleInput to run on all clients for responsiveness.
        HandleInput();

        if (isP2P && !IsServer) // Changed from IsHost to IsServer for clarity
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

        // Also give starting weapon in single-player
        PlayerWeaponManager pwm = playerObject.GetComponent<PlayerWeaponManager>();
        if (pwm != null)
        {
            // Since this is single-player, the PWM's Start() method will handle this.
            // No extra call is needed here.
        }
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
            // Track alive state on server for revive/gameover
            playerAlive[entry.Key] = true;
        }
        InitializeGameClientRpc();
    }
    
    [ClientRpc]
    private void InitializeGameClientRpc()
    {
        // This check is important because a client might receive this RPC before their player object is ready.
        if (NetworkManager.Singleton.LocalClient.PlayerObject != null)
        {
            GameObject localPlayerObject = NetworkManager.Singleton.LocalClient.PlayerObject.gameObject;
            InitializeAllSystems(localPlayerObject);
        }
    }

    private void InitializeAllSystems(GameObject playerObject)
    {
        localPlayer = playerObject.GetComponent<Movement>();
        playerExperience?.Initialize(playerObject);
        upgradeManager?.Initialize(playerObject);
        enemyDespawner?.Initialize(playerObject);

        if (!isP2P)
        {
            // Single-player: explicitly generate map only when game starts
            mapGenerator?.GenerateLocal();
            enemySpawner?.StartSpawning();
        }
        else if (IsHost)
        {
            // Multiplayer host: generate shared map assets and start spawns
            enemySpawner?.StartSpawning();
            mapGenerator?.GenerateNetworked();
            EnsureAllMapConsumablesSpawned();
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

    // Ensure all interactable map consumables (including chests) are network-spawned so clients can interact and observe despawns
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
            PlayerDiedServerRpc(); // legacy path; use PlayerDowned in P2P via PlayerStats
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
        // Ensure the server is tracking all connected players. Missing entries default to alive=true
        if (NetworkManager.Singleton != null)
        {
            foreach (var c in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (!playerAlive.ContainsKey(c.ClientId)) playerAlive[c.ClientId] = true;
            }
        }

        playerAlive[clientId] = false;
        reviveProgress[clientId] = 0f;
        // If everyone is downed at this moment, end the game
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
            // Proper shutdown logic
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.Shutdown();
            }
            if(Instance != null) Destroy(Instance.gameObject);
            SceneManager.LoadScene(0); // Assuming scene 0 is your main menu
            return;
        }
        
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
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

    // Global synchronized pause for level up (works in P2P)
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

    // Server-side revive logic in P2P
    private void ServerUpdateRevives()
    {
        if (NetworkManager.Singleton == null) return;
        var nm = NetworkManager.Singleton;
        foreach (var kvp in playerAlive)
        {
            if (kvp.Value) continue; // only check downed players
            ulong downedId = kvp.Key;
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
                        ps.ServerReviveToPercent(0.5f);
                        playerAlive[downedId] = true;
                        reviveProgress.Remove(downedId);
                        // Play revive VFX on all clients
                        PlayReviveVFXClientRpc(downedClient.PlayerObject.NetworkObjectId);
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
            if (reviveSprite == null) return;
            var pos = netObj.transform.position;
            ReviveVFX.Spawn(pos, reviveSprite, reviveColor, reviveVfxDuration, 0.8f, 1.3f, reviveVfxYOffset);
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
    // Call this on the server to apply damage to a specific player's character,
    // and mirror it to that player's client so their local HUD/state stays in sync.
    public void ServerApplyPlayerDamage(ulong targetClientId, float amount, Vector3? hitFromWorldPos = null, float? customIFrameDuration = null)
    {
        if (!isP2P)
        {
            // Single-player: just find local PlayerStats and apply directly
            var ps = FindObjectOfType<PlayerStats>();
            ps?.ApplyDamage(amount, hitFromWorldPos, customIFrameDuration);
            return;
        }

        if (!IsServer)
        {
            // Safety: only server should call the authoritative method in P2P
            return;
        }

        if (NetworkManager.Singleton == null) return;
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(targetClientId, out var client) || client.PlayerObject == null) return;

        var targetStats = client.PlayerObject.GetComponent<PlayerStats>();
        if (targetStats != null)
        {
            // Apply damage on server for authority
            targetStats.ApplyDamage(amount, hitFromWorldPos, customIFrameDuration);

            // Now mirror to the owning client so their local PlayerStats/HUD stay consistent
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
        // Runs only on the targeted client specified by ServerApplyPlayerDamage
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