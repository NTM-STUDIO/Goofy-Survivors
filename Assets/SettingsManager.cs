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

    [Header("Soft Restart Hooks")]
    [Tooltip("Eventos opcionais de UI para correr antes de reiniciar (ex: limpar texto).")]
    public UnityEvent onSoftRestart;

    [Header("Debug")]
    public bool enableDebugLogs = false;

    private bool isPanelOpen = false;
    private readonly List<int> fpsOptions = new List<int> { 60, 144, 240 };

    void Start()
    {
        // Configurações de Vídeo
        PopulateMonitorDropdown();
        PopulateFpsDropdown();
        monitorDropdown.onValueChanged.AddListener(SetMonitor);
        fpsDropdown.onValueChanged.AddListener(SetFpsLimit);
        LoadSettings();

        // Configura Botões
        if (restartButton != null) restartButton.onClick.AddListener(UI_Restart);
        if (leaveButton != null) leaveButton.onClick.AddListener(UI_Leave);

        RefreshRoleUI();
    }

    void Update()
    {
        // Menu de Pausa (ESC)
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            ToggleSettingsPanel();
        }
    }

    public void ToggleSettingsPanel()
    {
        if (settingsPanel == null)
        {
            settingsPanel = TryFindSettingsPanel();
            if (settingsPanel == null) return;
        }

        isPanelOpen = !isPanelOpen;
        settingsPanel.SetActive(isPanelOpen);

        if (isPanelOpen)
        {
            // PAUSA: true = Parar tempo, true = Mostrar Menu (diz ao GM que é menu)
            if (GameManager.Instance) 
                GameManager.Instance.RequestPause(true, true);
            
            RefreshRoleUI(); 
        }
        else
        {
            // RESUME: false = Retomar jogo
            if (GameManager.Instance) 
                GameManager.Instance.RequestPause(false);
        }
    }

    // --- AÇÕES DOS BOTÕES (Delegam para o GameManager) ---

    public void UI_Restart()
    {
        Debug.Log("[SettingsManager] UI_Restart clicado.");

        // 1. Fecha o menu visualmente
        if (settingsPanel) settingsPanel.SetActive(false);
        isPanelOpen = false;

        // 2. Invoka eventos locais de UI se houver
        onSoftRestart?.Invoke();

        // 3. Manda o GameManager tratar de tudo (Limpar, Resetar, Recomeçar)
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ActionPlayAgain();
        }
    }

    public void UI_Leave()
    {
        Debug.Log("[SettingsManager] UI_Leave clicado.");

        // 1. Fecha o menu
        if (settingsPanel) settingsPanel.SetActive(false);
        isPanelOpen = false;

        // 2. Manda o GameManager sair para o Lobby/Menu
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ActionLeaveToLobby();
        }
    }

    // --- LÓGICA DE UI ---

    private void RefreshRoleUI()
    {
        // Botão Restart: Só visível se for Singleplayer OU se for o Host
        if (restartButton != null)
        {
            bool showRestart = true;
            
            // Se for Multiplayer e eu NÃO for o Host, esconde o botão
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
            {
                if (!NetworkManager.Singleton.IsServer) showRestart = false;
            }

            restartButton.gameObject.SetActive(showRestart);
        }

        // Botão Leave: Sempre visível
        if (leaveButton != null)
        {
            leaveButton.gameObject.SetActive(true);
        }
    }

    private GameObject TryFindSettingsPanel()
    {
        var child = transform.Find("SettingsPanel");
        if (child != null) return child.gameObject;
        return GameObject.Find("SettingsPanel"); // Fallback
    }

    // --- DEFINIÇÕES DE VÍDEO (IGUAL AO ANTERIOR) ---

    #region Video Settings
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
        if (monitorIndex < Display.displays.Length) monitorDropdown.value = monitorIndex;

        int fpsIndex = PlayerPrefs.GetInt("FpsLimitIndex", fpsOptions.Count - 1);
        fpsDropdown.value = fpsIndex;
        SetFpsLimit(fpsIndex);
    }
    #endregion
}