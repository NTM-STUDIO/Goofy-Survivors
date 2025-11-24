using UnityEngine;
using TMPro;
using System.Collections.Generic;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using UnityEngine.Events;

public class SettingsManager : MonoBehaviour
{
    [Header("UI References")]
    public GameObject settingsPanel;
    public TMP_Dropdown monitorDropdown;
    public TMP_Dropdown fpsDropdown;

    [Header("Pause Actions (Buttons)")]
    public UnityEngine.UI.Button restartButton;
    public UnityEngine.UI.Button leaveButton;

    [Header("Scene Names & Prefabs")]
    public string lobbySceneName = "P2P";
    public string gameplaySceneName = "MainScene";
    public string mainMenuSceneName = "Splash";
    public GameObject lobbyManagerPrefab;

    [Header("Soft Restart Hooks")]
    public UnityEvent onSoftRestart;

    [Header("Singleplayer Restart Fallback")]
    public bool spReloadSceneIfSoftRestartStalls = false;
    public float spReloadDelaySeconds = 1.0f;

    [Header("Singleplayer Selection")]
    public List<GameObject> spUnitPrefabs = new List<GameObject>();
    public int spDefaultUnitIndex = 0;

    [Header("Debug")]
    public bool enableDebugLogs = false;

    private bool isPanelOpen = false;
    private readonly List<int> fpsOptions = new List<int> { 60, 144, 240 };

    void Start()
    {
        PopulateMonitorDropdown();
        PopulateFpsDropdown();

        monitorDropdown.onValueChanged.AddListener(SetMonitor);
        fpsDropdown.onValueChanged.AddListener(SetFpsLimit);

        LoadSettings();

        if (restartButton != null) restartButton.onClick.AddListener(UI_Restart);
        else Debug.LogWarning("[SettingsManager] restartButton is not assigned.");
        
        if (leaveButton != null) leaveButton.onClick.AddListener(UI_Leave);
        else Debug.LogWarning("[SettingsManager] leaveButton is not assigned.");

        RefreshRoleUI();
        TryRegisterSoftRestartHandler();
    }

    void Update()
    {
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
            settingsPanel = TryFindSettingsPanel();
            if (settingsPanel == null)
            {
                Debug.LogWarning("[SettingsManager] settingsPanel not found.");
                return;
            }
        }
        isPanelOpen = !isPanelOpen;
        settingsPanel.SetActive(isPanelOpen);

        if (isPanelOpen)
        {
            // CORREÇÃO 1: Pausar e indicar que é Menu (true, true)
            GameManager.Instance.RequestPause(true, true);
            RefreshRoleUI(); 
            if (enableDebugLogs) Debug.Log("[SettingsManager] Panel opened (paused).");
        }
        else
        {
            // CORREÇÃO 2: Despausar (false)
            GameManager.Instance.RequestPause(false);
            if (enableDebugLogs) Debug.Log("[SettingsManager] Panel closed (resumed)");
        }
    }

    private GameObject TryFindSettingsPanel()
    {
        var child = transform.Find("SettingsPanel");
        if (child != null) return child.gameObject;
        var tagged = GameObject.FindWithTag("SettingsPanel");
        if (tagged != null) return tagged;
        return GameObject.Find("SettingsPanel");
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

    private void RefreshRoleUI()
    {
        if (restartButton != null)
        {
            restartButton.gameObject.SetActive(!IsMultiplayerActive() || IsHost());
        }
        if (leaveButton != null)
        {
            leaveButton.gameObject.SetActive(true);
        }
    }

    public void UI_Restart()
    {
        Debug.Log("[SettingsManager] UI_Restart() started");
        
        // CORREÇÃO 3: Garantir despausa antes de reiniciar
        try { GameManager.Instance.RequestPause(false); } catch { }
        
        Time.timeScale = 1f;
        if (settingsPanel) settingsPanel.SetActive(false);
        isPanelOpen = false;
        HideEndGameOverlays();

        if (IsMultiplayerActive())
        {
            if (!IsHost()) return;
            
            var nm = NetworkManager.Singleton;
            var nsm = nm?.SceneManager;
            if (nsm != null)
            {
                nsm.OnLoadEventCompleted += HandleLobbySceneLoaded;
                nsm.LoadScene(lobbySceneName, LoadSceneMode.Single);
            }
        }
        else
        {
            try
            {
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
                var scene = SceneManager.GetActiveScene().name;
                SceneManager.LoadScene(scene, LoadSceneMode.Single);
            }
        }
    }

    public void UI_Leave()
    {
        // CORREÇÃO 4: Garantir despausa antes de sair
        try { GameManager.Instance.RequestPause(false); } catch { }
        
        Time.timeScale = 1f;
        if (settingsPanel) settingsPanel.SetActive(false);
        isPanelOpen = false;
        if (IsMultiplayerActive())
        {
            if (IsHost())
            {
                var nsm = NetworkManager.Singleton.SceneManager;
                if (nsm != null)
                {
                    nsm.OnLoadEventCompleted += HandleLobbySceneLoaded;
                    nsm.LoadScene(lobbySceneName, LoadSceneMode.Single);
                }
            }
            else
            {
                if (NetworkManager.Singleton) NetworkManager.Singleton.Shutdown();
                if (!string.IsNullOrEmpty(lobbySceneName)) SceneManager.LoadScene(lobbySceneName, LoadSceneMode.Single);
            }
        }
        else
        {
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
        if (lobbyManagerPrefab != null)
        {
            var go = Instantiate(lobbyManagerPrefab);
            go.GetComponent<NetworkObject>()?.Spawn();
        }
    }

    private void TryRegisterSoftRestartHandler()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || nm.CustomMessagingManager == null) return;
        
        nm.CustomMessagingManager.RegisterNamedMessageHandler("SOFT_RESTART", (sender, reader) =>
        {
            // CORREÇÃO 5: Despausa no Soft Restart
            try { GameManager.Instance.RequestPause(false); } catch { }
            Time.timeScale = 1f;
            if (settingsPanel) settingsPanel.SetActive(false);
            isPanelOpen = false;
            try { GameManager.Instance.StartGame(); onSoftRestart?.Invoke(); HideEndGameOverlays(); }
            catch { }
        });
    }

    private void HideEndGameOverlays()
    {
        try
        {
            var egp = Object.FindFirstObjectByType<EndGamePanel>(FindObjectsInactive.Include);
            if (egp != null) egp.gameObject.SetActive(false);
        }
        catch { }
    }

    private System.Collections.IEnumerator SpFallbackReloadAfterDelay()
    {
        float delay = Mathf.Max(0.1f, spReloadDelaySeconds);
        yield return new WaitForSecondsRealtime(delay);
        var egp = Object.FindFirstObjectByType<EndGamePanel>(FindObjectsInactive.Include);
        if (egp != null && egp.gameObject.activeInHierarchy)
        {
            var scene = SceneManager.GetActiveScene().name;
            SceneManager.LoadScene(scene, LoadSceneMode.Single);
        }
    }

    private void TryApplySinglePlayerSelection()
    {
        LoadoutSelections.EnsureValidDefaults();
        try
        {
            if (LoadoutSelections.SelectedCharacterPrefab != null)
            {
                GameManager.Instance.SetChosenPlayerPrefab(LoadoutSelections.SelectedCharacterPrefab);
                return;
            }
        }
        catch { }

        int idx = PlayerPrefs.GetInt("SP_SelectedUnitIndex", spDefaultUnitIndex);
        GameObject prefab = null;

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

        if (prefab == null && spUnitPrefabs != null && spUnitPrefabs.Count > 0)
        {
            idx = Mathf.Clamp(idx, 0, spUnitPrefabs.Count - 1);
            prefab = spUnitPrefabs[idx];
        }

        if (prefab != null)
        {
            try { GameManager.Instance.SetChosenPlayerPrefab(prefab); } catch { }
        }
    }

    private void PopulateMonitorDropdown()
    {
        monitorDropdown.ClearOptions();
        List<string> options = new List<string>();
        for (int i = 0; i < Display.displays.Length; i++) options.Add("Display " + (i + 1));
        monitorDropdown.AddOptions(options);
    }

    public void SetMonitor(int monitorIndex)
    {
        if (monitorIndex < 0 || monitorIndex >= Display.displays.Length) return;
        Display.displays[monitorIndex].Activate();
        PlayerPrefs.SetInt("MonitorIndex", monitorIndex);
    }

    private void PopulateFpsDropdown()
    {
        fpsDropdown.ClearOptions();
        List<string> options = new List<string>();
        foreach (int fps in fpsOptions) options.Add(fps == 0 ? "Unlimited" : fps.ToString());
        fpsDropdown.AddOptions(options);
    }

    public void SetFpsLimit(int fpsIndex)
    {
        int fpsLimit = fpsOptions[fpsIndex];
        Application.targetFrameRate = (fpsLimit == 0) ? -1 : fpsLimit;
        PlayerPrefs.SetInt("FpsLimitIndex", fpsIndex);
    }

    private void LoadSettings()
    {
        int monitorIndex = PlayerPrefs.GetInt("MonitorIndex", 0);
        if (monitorIndex >= Display.displays.Length) monitorIndex = 0;
        monitorDropdown.value = monitorIndex;

        int fpsIndex = PlayerPrefs.GetInt("FpsLimitIndex", fpsOptions.Count - 1);
        fpsDropdown.value = fpsIndex;
        SetFpsLimit(fpsIndex);

        monitorDropdown.RefreshShownValue();
        fpsDropdown.RefreshShownValue();
    }
}