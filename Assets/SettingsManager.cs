using UnityEngine;
using TMPro; // Required for TextMeshPro components
using System.Collections.Generic; // Required for Lists
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Collections;
using UnityEngine.Events;

public class SettingsManager : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("The parent panel for all settings UI elements.")]
    public GameObject settingsPanel;
    [Tooltip("The dropdown for selecting the display monitor.")]
    public TMP_Dropdown monitorDropdown;
    [Tooltip("The dropdown for selecting the FPS limit.")]
    public TMP_Dropdown fpsDropdown;

    [Header("Pause Actions (Buttons)")]
    [Tooltip("Restart round button (host-only in multiplayer).")]
    public UnityEngine.UI.Button restartButton;
    [Tooltip("Leave button (visible to all players).")]
    public UnityEngine.UI.Button leaveButton;

    [Header("Scene Names & Prefabs")]
    [Tooltip("Lobby scene name for multiplayer.")]
    public string lobbySceneName = "P2P";
    [Tooltip("Gameplay scene name used for restart.")]
    public string gameplaySceneName = "MainScene";
    [Tooltip("Main menu scene name for singleplayer leave.")]
    public string mainMenuSceneName = "Splash";
    [Tooltip("Networked LobbyManagerP2P prefab to spawn on lobby scene after Back to Lobby (host only).")]
    public GameObject lobbyManagerPrefab;

    [Header("Soft Restart Hooks")]
    [Tooltip("Optional hooks that run when performing a soft restart (no scene reload). Useful to clear UI, reset local-only systems, etc.")]
    public UnityEvent onSoftRestart;

    [Header("Singleplayer Restart Fallback")]
    [Tooltip("If enabled, and soft restart doesn't progress, reload the current scene after a short delay.")]
    public bool spReloadSceneIfSoftRestartStalls = false;
    [Tooltip("Delay (seconds) before applying the fallback reload in singleplayer.")]
    public float spReloadDelaySeconds = 1.0f;

    [Header("Singleplayer Selection (for Restart)")]
    [Tooltip("List of available unit prefabs for singleplayer soft restart. Must match your normal SP selection options.")]
    public List<GameObject> spUnitPrefabs = new List<GameObject>();
    [Tooltip("Index of the unit to use when restarting in singleplayer (acts like the unit you selected before entering the game).")]
    public int spDefaultUnitIndex = 0;

    [Header("Debug")]
    [Tooltip("If enabled, prints debug logs when ESC is detected and when the panel toggles.")]
    public bool enableDebugLogs = false;

    // --- Private State ---
    private bool isPanelOpen = false;
    // A list of common FPS options. 0 means "Unlimited" in the new system.
    private readonly List<int> fpsOptions = new List<int> { 60, 144, 240 };

    void Start()
    {
        // Populate the UI with available system settings
        PopulateMonitorDropdown();
        PopulateFpsDropdown();

        // Add listeners. These will call our methods whenever a new value is selected.
        monitorDropdown.onValueChanged.AddListener(SetMonitor);
        fpsDropdown.onValueChanged.AddListener(SetFpsLimit);

        // Load any previously saved settings
        LoadSettings();

        // Wire pause buttons (if assigned)
        if (restartButton != null)
        {
            restartButton.onClick.AddListener(UI_Restart);
        }
        else
        {
            Debug.LogWarning("[SettingsManager] restartButton is not assigned in the Inspector. Restart will do nothing when clicked.");
        }
        if (leaveButton != null)
        {
            leaveButton.onClick.AddListener(UI_Leave);
        }
        else
        {
            Debug.LogWarning("[SettingsManager] leaveButton is not assigned in the Inspector. Leave will do nothing when clicked.");
        }

        RefreshRoleUI();

        // Register soft-restart handler for multiplayer so host can restart without scene reload
        TryRegisterSoftRestartHandler();
    }

    void Update()
    {
        // Listen for the Escape key to toggle the menu
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (enableDebugLogs) Debug.Log("[SettingsManager] ESC pressed, toggling settings panel...");
            ToggleSettingsPanel();
        }
    }

    public void ToggleSettingsPanel()
    {
        if (settingsPanel == null)
        {
            // Fallback search to avoid misconfiguration
            settingsPanel = TryFindSettingsPanel();
            if (settingsPanel == null)
            {
                Debug.LogWarning("[SettingsManager] settingsPanel is not assigned and could not be found in scene.");
                return;
            }
        }
        isPanelOpen = !isPanelOpen;
        settingsPanel.SetActive(isPanelOpen);

        // Use the new, robust pause request system
        if (isPanelOpen)
        {
            GameManager.Instance.RequestPause();
            RefreshRoleUI(); // Update button visibility every time panel opens
            if (enableDebugLogs) Debug.Log("[SettingsManager] Panel opened (paused).");
        }
        else
        {
            GameManager.Instance.RequestResume();
            if (enableDebugLogs) Debug.Log("[SettingsManager] Panel closed (resumed)");
        }
    }

    private GameObject TryFindSettingsPanel()
    {
        // Try common patterns: child named "SettingsPanel" or a panel tagged "SettingsPanel"
        var child = transform.Find("SettingsPanel");
        if (child != null) return child.gameObject;
        var tagged = GameObject.FindWithTag("SettingsPanel");
        if (tagged != null) return tagged;
        // Broad name search (last resort)
        var go = GameObject.Find("SettingsPanel");
        return go;
    }

    private bool IsMultiplayerActive()
    {
        var nm = NetworkManager.Singleton;
        return nm != null && (nm.IsServer || nm.IsClient) && nm.IsListening;
    }

    private bool IsHost()
    {
        var nm = NetworkManager.Singleton;
        return nm != null && nm.IsServer;
    }

    private bool IsClientOnly()
    {
        var nm = NetworkManager.Singleton;
        return nm != null && nm.IsClient && !nm.IsServer;
    }

    private void RefreshRoleUI()
    {
        // In multiplayer, the restart button is only for the host. In singleplayer, it's always available.
        if (restartButton != null)
        {
            restartButton.gameObject.SetActive(!IsMultiplayerActive() || IsHost());
        }

        // As requested, the leave button should now be always visible for all players.
        if (leaveButton != null)
        {
            leaveButton.gameObject.SetActive(true);
        }
    }

    // Host-only in MP; SP allowed
    public void UI_Restart()
    {
        Debug.Log("[SettingsManager] UI_Restart() started");
        
        // Ensure game isn't left paused
        try { GameManager.Instance.RequestResume(); } catch { }
        Time.timeScale = 1f;
        if (settingsPanel) settingsPanel.SetActive(false);
        isPanelOpen = false;
        HideEndGameOverlays();
        if (enableDebugLogs) Debug.Log("[SettingsManager] UI_Restart invoked. MP=" + IsMultiplayerActive() + " Host=" + IsHost());
        if (IsMultiplayerActive())
        {
            if (!IsHost())
            {
                Debug.LogWarning("[SettingsManager] Restart is host-only in multiplayer.");
                return;
            }
            
            // In multiplayer, return to lobby instead of soft restart
            Debug.Log("[SettingsManager] Multiplayer restart: Returning to lobby for new match setup.");
            var nm = NetworkManager.Singleton;
            var nsm = nm?.SceneManager;
            if (nsm != null)
            {
                Debug.Log("[SettingsManager] Loading lobby scene via NGO for all players.");
                nsm.OnLoadEventCompleted += HandleLobbySceneLoaded;
                nsm.LoadScene(lobbySceneName, LoadSceneMode.Single);
            }
            else
            {
                Debug.LogError("[SettingsManager] NetworkSceneManager unavailable. Cannot return to lobby.");
            }
        }
        else
        {
            // Singleplayer: reload current or configured gameplay scene
            // Prefer a soft restart if GameManager supports it
            try
            {
                if (enableDebugLogs) Debug.Log("[SettingsManager] Singleplayer soft restart: StartGame()");
                // Clean current run state so StartGame behaves like first run
                try { GameManager.Instance.SoftResetSinglePlayerWorld(); } catch { }
                TryApplySinglePlayerSelection();
                GameManager.Instance.StartGame();
                onSoftRestart?.Invoke();
                HideEndGameOverlays();
                if (spReloadSceneIfSoftRestartStalls)
                    StartCoroutine(SpFallbackReloadAfterDelay());
            }
            catch (System.Exception ex)
            {
                if (enableDebugLogs) Debug.Log($"[SettingsManager] Singleplayer StartGame threw: {ex.Message}. Reloading scene.");
                var scene = SceneManager.GetActiveScene().name;
                SceneManager.LoadScene(scene, LoadSceneMode.Single);
            }
        }
    }

    // Host: back to lobby for all. Client: disconnect and go to lobby locally. SP: go to main menu.
    public void UI_Leave()
    {
        // Ensure game isn't left paused
        try { GameManager.Instance.RequestResume(); } catch { }
        Time.timeScale = 1f;
        if (settingsPanel) settingsPanel.SetActive(false);
        isPanelOpen = false;
        if (IsMultiplayerActive())
        {
            if (IsHost())
            {
                var nsm = NetworkManager.Singleton.SceneManager;
                if (nsm == null)
                {
                    Debug.LogError("[SettingsManager] NetworkSceneManager missing.");
                    return;
                }
                nsm.OnLoadEventCompleted += HandleLobbySceneLoaded;
                nsm.LoadScene(lobbySceneName, LoadSceneMode.Single);
            }
            else
            {
                // Client leaves session and returns to lobby locally
                if (NetworkManager.Singleton)
                    NetworkManager.Singleton.Shutdown();
                if (!string.IsNullOrEmpty(lobbySceneName))
                    SceneManager.LoadScene(lobbySceneName, LoadSceneMode.Single);
            }
        }
        else
        {
            // Singleplayer: leave to main menu
            if (!string.IsNullOrEmpty(mainMenuSceneName))
                SceneManager.LoadScene(mainMenuSceneName, LoadSceneMode.Single);
        }
    }

    private void HandleLobbySceneLoaded(string sceneName, LoadSceneMode mode, System.Collections.Generic.List<ulong> completed, System.Collections.Generic.List<ulong> timedOut)
    {
        if (!string.Equals(sceneName, lobbySceneName)) return;
        var nm = NetworkManager.Singleton;
        var nsm = nm?.SceneManager;
        if (nsm != null) nsm.OnLoadEventCompleted -= HandleLobbySceneLoaded;

        if (nm == null || !nm.IsServer) return;
        if (lobbyManagerPrefab == null)
        {
            Debug.LogWarning("[SettingsManager] LobbyManager prefab not assigned; lobby UI won't auto-spawn.");
            return;
        }
        var go = Instantiate(lobbyManagerPrefab);
        var no = go.GetComponent<NetworkObject>();
        if (no == null)
        {
            Debug.LogError("[SettingsManager] LobbyManager prefab missing NetworkObject.");
            Destroy(go);
            return;
        }
        no.Spawn();
    }

    private void TryRegisterSoftRestartHandler()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || nm.CustomMessagingManager == null) return;
        // Register once; idempotent based on name
        nm.CustomMessagingManager.RegisterNamedMessageHandler("SOFT_RESTART", (sender, reader) =>
        {
            // Ensure unpaused locally and restart the round without reloading the scene
            if (enableDebugLogs) Debug.Log("[SettingsManager] SOFT_RESTART received from sender=" + sender + ". Starting game.");
            try { GameManager.Instance.RequestResume(); } catch { }
            Time.timeScale = 1f;
            if (settingsPanel) settingsPanel.SetActive(false);
            isPanelOpen = false;
            try { GameManager.Instance.StartGame(); onSoftRestart?.Invoke(); HideEndGameOverlays(); }
            catch (System.Exception ex) { Debug.LogWarning($"[SettingsManager] Soft restart failed: {ex.Message}"); }
        });
    }

    private void HideEndGameOverlays()
    {
        // Hide any active EndGamePanel to avoid UI blocking after restart
        try
        {
            var egp = Object.FindFirstObjectByType<EndGamePanel>(FindObjectsInactive.Include);
            if (egp != null && egp.gameObject.activeInHierarchy)
            {
                if (enableDebugLogs) Debug.Log("[SettingsManager] Hiding EndGamePanel after restart.");
                egp.gameObject.SetActive(false);
            }
        }
        catch { }
    }

    private System.Collections.IEnumerator SpFallbackReloadAfterDelay()
    {
        // Optional fallback if StartGame() doesn't bring the game back after a short delay
        float delay = Mathf.Max(0.1f, spReloadDelaySeconds);
        yield return new WaitForSecondsRealtime(delay);
        // Heuristic: if EndGamePanel still exists and is active, assume restart stalled and reload scene
        var egp = Object.FindFirstObjectByType<EndGamePanel>(FindObjectsInactive.Include);
        if (egp != null && egp.gameObject.activeInHierarchy)
        {
            if (enableDebugLogs) Debug.Log("[SettingsManager] SP soft restart appears stalled. Reloading scene as fallback.");
            var scene = SceneManager.GetActiveScene().name;
            SceneManager.LoadScene(scene, LoadSceneMode.Single);
        }
    }

    private void TryApplySinglePlayerSelection()
    {
        // Garante que sempre temos defaults válidos (importante para restarts)
        LoadoutSelections.EnsureValidDefaults();
        
        // If LoadoutSelections has an explicit character prefab, prefer it
        try
        {
            if (LoadoutSelections.SelectedCharacterPrefab != null)
            {
                GameManager.Instance.SetChosenPlayerPrefab(LoadoutSelections.SelectedCharacterPrefab);
                if (enableDebugLogs) Debug.Log("[SettingsManager] SP selection applied from LoadoutSelections.");
                return;
            }
        }
        catch { }

        // Prefer the last selection stored by UnitCarouselSelector; else use serialized list
        int idx = PlayerPrefs.GetInt("SP_SelectedUnitIndex", spDefaultUnitIndex);
        GameObject prefab = null;

        // Try reading from an existing UnitCarouselSelector in scene (even if inactive)
        try
        {
            var selector = Object.FindFirstObjectByType<UnitCarouselSelector>(FindObjectsInactive.Include);
            if (selector != null && selector.unitPrefabs != null && selector.unitPrefabs.Count > 0)
            {
                idx = Mathf.Clamp(idx, 0, selector.unitPrefabs.Count - 1);
                prefab = selector.unitPrefabs[idx];
            }
        }
        catch { }

        // Fallback to inspector list
        if (prefab == null)
        {
            if (spUnitPrefabs == null || spUnitPrefabs.Count == 0)
            {
                if (enableDebugLogs) Debug.Log("[SettingsManager] No UnitCarouselSelector found and spUnitPrefabs is empty; skipping SP selection.");
            }
            else
            {
                idx = Mathf.Clamp(idx, 0, spUnitPrefabs.Count - 1);
                prefab = spUnitPrefabs[idx];
            }
        }

        if (prefab != null)
        {
            try
            {
                // In singleplayer we must set the chosen prefab directly on the GameManager
                GameManager.Instance.SetChosenPlayerPrefab(prefab);
                if (enableDebugLogs) Debug.Log($"[SettingsManager] SP selection applied with prefab '{prefab.name}' at index {idx}.");
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[SettingsManager] Failed to apply SP selection: {ex.Message}");
            }
        }
    }

    #region Monitor Settings

    private void PopulateMonitorDropdown()
    {
        monitorDropdown.ClearOptions();
        List<string> options = new List<string>();

        // Unity's Display class gives us access to all connected monitors
        for (int i = 0; i < Display.displays.Length; i++)
        {
            options.Add("Display " + (i + 1));
        }

        monitorDropdown.AddOptions(options);
    }

    public void SetMonitor(int monitorIndex)
    {
        if (monitorIndex < 0 || monitorIndex >= Display.displays.Length)
        {
            Debug.LogWarning("Invalid monitor index selected.");
            return;
        }

        // IMPORTANT: This works best in a standalone build. Behavior in the Unity Editor can be unpredictable.
        Display.displays[monitorIndex].Activate();

        // Save the chosen monitor index
        PlayerPrefs.SetInt("MonitorIndex", monitorIndex);
        Debug.Log("Switched to Display " + (monitorIndex + 1));
    }

    #endregion

    #region FPS Settings

    private void PopulateFpsDropdown()
    {
        fpsDropdown.ClearOptions();
        List<string> options = new List<string>();
        foreach (int fps in fpsOptions)
        {
            if (fps == 0)
            {
                options.Add("Unlimited");
            }
            else
            {
                options.Add(fps.ToString());
            }
        }
        fpsDropdown.AddOptions(options);
    }

    public void SetFpsLimit(int fpsIndex)
    {
        int fpsLimit = fpsOptions[fpsIndex];

        // In modern Unity, a targetFrameRate of 0 or -1 means unlimited. We'll use 0.
        if (fpsLimit == 0)
        {
            Application.targetFrameRate = -1; // Or QualitySettings.vSyncCount = 0;
        }
        else
        {
            Application.targetFrameRate = fpsLimit;
        }

        // Save the setting so it persists
        PlayerPrefs.SetInt("FpsLimitIndex", fpsIndex);
    }

    #endregion

    // --- SAVING AND LOADING ---

    private void LoadSettings()
    {
        // Load and apply the saved monitor, defaulting to the primary display (index 0)
        int monitorIndex = PlayerPrefs.GetInt("MonitorIndex", 0);
        if (monitorIndex >= Display.displays.Length) monitorIndex = 0; // Sanity check
        monitorDropdown.value = monitorIndex;
        // NOTE: We don't call SetMonitor() here on startup to avoid unexpected screen changes.
        // The game will launch on the monitor the OS deems primary or where it was last placed.
        // The dropdown will correctly reflect the *saved preference* for the user to apply.

        // Load and apply the saved FPS limit, defaulting to Unlimited
        int fpsIndex = PlayerPrefs.GetInt("FpsLimitIndex", fpsOptions.Count - 1); // Default to last entry (Unlimited)
        fpsDropdown.value = fpsIndex;
        SetFpsLimit(fpsIndex);

        // Update the displayed text in the dropdowns
        monitorDropdown.RefreshShownValue();
        fpsDropdown.RefreshShownValue();
    }
}