// Filename: UIManager.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using System.Collections;
using MyGame.ConnectionSystem.Connection;
using MyGame.ConnectionSystem.Services;
// VContainer namespace is no longer needed

public class UIManager : MonoBehaviour
{
    [Header("Core UI Panels")]
    [SerializeField] private GameObject painelPrincipal;
    [SerializeField] private GameObject multiplayerPanel;
    [SerializeField] private GameObject pauseMenu;
    [SerializeField] private GameObject statsPanel;
    [SerializeField] private GameObject endGamePanel;

    [Header("In-Game HUD Elements")]
    [SerializeField] private GameObject inGameHudContainer;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private Slider xpSlider;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private Slider healthBar;
    
    [Header("New Weapon Panel")]
    [SerializeField] private GameObject newWeaponPanel;
    [SerializeField] private TextMeshProUGUI weaponNameText;
    [SerializeField] private Image weaponSpriteImage;
    
    [Header("Multiplayer Elements")]
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private TextMeshProUGUI lobbyCodeText;
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private TextMeshProUGUI connectionStatusText;

    [Header("Character Selection Elements")]
    // This was missing from your provided script, but is needed for the logic
    [SerializeField] private GameObject[] characterPrefabs;

    [Header("System References")]
    [SerializeField] private AdvancedCameraController advancedCameraController;

    // --- CORRECTION: Dependencies are no longer injected ---
    private ConnectionManager connectionManager;
    private MultiplayerServicesFacade servicesFacade;
    private GameManager gameManager;

    private int selectedCharacterIndex = 0;
    private bool isHost = false;
    private bool connectionEstablished = false;

    void Start()
    {
        // --- CORRECTION: Find the instances of our managers manually ---
        connectionManager = ConnectionManager.Instance;
        servicesFacade = MultiplayerServicesFacade.Instance;
        // The GameManager is found later via the helper property

        if (connectionManager == null) Debug.LogError("FATAL ERROR: UIManager could not find ConnectionManager.Instance!");
        if (servicesFacade == null) Debug.LogError("FATAL ERROR: UIManager could not find MultiplayerServicesFacade.Instance!");

        // Initial UI state
        painelPrincipal.SetActive(true);
        multiplayerPanel.SetActive(false);

        inGameHudContainer.SetActive(false);
        endGamePanel.SetActive(false);
        pauseMenu.SetActive(false);
        
        if (lobbyCodeText != null) lobbyCodeText.gameObject.SetActive(false);
        if (connectionStatusText != null) connectionStatusText.gameObject.SetActive(false);

        // This part is fine, but we add a null check for safety
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    void OnDestroy()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
    }

    // A helper property to safely get the GameManager instance whenever needed
    private GameManager GameManager
    {
        get
        {
            if (gameManager == null)
            {
                gameManager = GameManager.Instance;
            }
            return gameManager;
        }
    }

    // ===============================================================
    // FLUXO DO MENU PRINCIPAL
    // ===============================================================
    public void PlayButton()
    {
        Debug.Log("[UIManager] Modo Single-Player selecionado");
        if (GameManager != null) GameManager.isP2P = false;
        painelPrincipal.SetActive(false);
        // Start the game and show HUD if prefab is selected
        if (GameManager != null && !GameManager.isP2P)
        {
            GameManager.StartGame();
            OnGameStart();
        }
    }

    public void MultiplayerButton()
    {
        Debug.Log("[UIManager] Modo Multiplayer selecionado");
        if (GameManager != null) GameManager.isP2P = true;
        painelPrincipal.SetActive(false);
        multiplayerPanel.SetActive(true);
        
        if (connectionStatusText != null) connectionStatusText.gameObject.SetActive(false);
        if (lobbyCodeText != null) lobbyCodeText.gameObject.SetActive(false);
    }

    public void BackToMainMenu()
    {
        Debug.Log("[UIManager] Voltando ao menu principal");
        if (GameManager != null) GameManager.isP2P = false;
        
        multiplayerPanel.SetActive(false);
        painelPrincipal.SetActive(true);
        
        connectionEstablished = false;
        isHost = false;
    }

    public void QuitButton()
    {
        Debug.Log("[UIManager] Saindo do jogo");
        Application.Quit();
    }
    
    // ===============================================================
    // FLUXO DE CONEXÃO P2P
    // ===============================================================
    public void StartHost()
    {
        Debug.Log("[UIManager] Iniciando como Host...");
        isHost = true;
        
        if (hostButton != null) hostButton.interactable = false;
        if (joinButton != null) joinButton.interactable = false;
        
        if (connectionStatusText != null)
        {
            connectionStatusText.gameObject.SetActive(true);
            connectionStatusText.text = "Criando sessão...";
        }
        
        connectionManager.StartHost("HostPlayer");
        StartCoroutine(WaitForLobbyCode());
    }

    public void StartClient()
    {
        Debug.Log("[UIManager] Iniciando como Cliente...");
        isHost = false;

        if (string.IsNullOrEmpty(joinCodeInput.text))
        {
            Debug.LogError("[UIManager] O Join Code não pode estar vazio!");
            if (connectionStatusText != null)
            {
                connectionStatusText.gameObject.SetActive(true);
                connectionStatusText.text = "ERRO: Insira um código válido!";
                connectionStatusText.color = Color.red;
            }
            return;
        }
        
        if (hostButton != null) hostButton.interactable = false;
        if (joinButton != null) joinButton.interactable = false;
        
        if (connectionStatusText != null)
        {
            connectionStatusText.gameObject.SetActive(true);
            connectionStatusText.text = "Conectando...";
            connectionStatusText.color = Color.white;
        }
        
        connectionManager.StartClient("ClientPlayer", joinCodeInput.text);
    }

    private IEnumerator WaitForLobbyCode()
    {
        float timeout = 10f;
        float elapsed = 0f;
        
        while (string.IsNullOrEmpty(servicesFacade.LobbyJoinCode) && elapsed < timeout)
        {
            yield return new WaitForSeconds(0.1f);
            elapsed += 0.1f;
        }

        if (!string.IsNullOrEmpty(servicesFacade.LobbyJoinCode))
        {
            Debug.Log($"[UIManager] Lobby criado com código: {servicesFacade.LobbyJoinCode}");
            UpdateLobbyCodeText(servicesFacade.LobbyJoinCode);
            
            if (connectionStatusText != null)
            {
                connectionStatusText.text = "Lobby criado! Aguardando jogadores...";
                connectionStatusText.color = Color.green;
            }
            
            multiplayerPanel.SetActive(false);
        }
        else
        {
            Debug.LogError("[UIManager] Timeout ao aguardar código do lobby!");
            if (connectionStatusText != null)
            {
                connectionStatusText.text = "ERRO: Falha ao criar lobby!";
                connectionStatusText.color = Color.red;
            }
            
            if (hostButton != null) hostButton.interactable = true;
            if (joinButton != null) joinButton.interactable = true;
        }
    }

    public void UpdateLobbyCodeText(string lobbyCode)
    {
        if (lobbyCodeText != null)
        {
            lobbyCodeText.gameObject.SetActive(true);
            lobbyCodeText.text = $"CÓDIGO DO LOBBY:\n{lobbyCode}";
        }
    }

    // ===============================================================
    // CALLBACKS DE CONEXÃO
    // ===============================================================
    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"[UIManager] Cliente {clientId} conectado");
        
        if (NetworkManager.Singleton.IsHost && clientId == NetworkManager.Singleton.LocalClientId)
        {
            connectionEstablished = true;
            Debug.Log("[UIManager] Host conectado com sucesso!");
        }
        else if (NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsHost && clientId == NetworkManager.Singleton.LocalClientId)
        {
            connectionEstablished = true;
            Debug.Log("[UIManager] Cliente conectado com sucesso!");
            
            if (connectionStatusText != null)
            {
                connectionStatusText.text = "Conectado!";
                connectionStatusText.color = Color.green;
            }
            
            multiplayerPanel.SetActive(false);
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.Log($"[UIManager] Cliente {clientId} desconectado");
        
        if (clientId == NetworkManager.Singleton.LocalClientId)
        {
            connectionEstablished = false;
            BackToMainMenu();
        }
    }

    // ===============================================================
    // INÍCIO DO JOGO
    // ===============================================================
    private void StartSinglePlayerGame()
    {
        Debug.Log("[UIManager] Iniciando jogo single-player");
        if (GameManager != null) GameManager.StartGame();
    }

    private void StartMultiplayerGame()
    {
        if (!connectionEstablished)
        {
            Debug.LogError("[UIManager] Tentativa de iniciar jogo sem conexão estabelecida!");
            return;
        }

        Debug.Log("[UIManager] Iniciando jogo multiplayer");
        
        if (NetworkManager.Singleton.IsHost)
        {
            var selections = new System.Collections.Generic.Dictionary<ulong, GameObject>();
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                selections[client.ClientId] = characterPrefabs[selectedCharacterIndex];
            }
            if (GameManager != null)
            {
                GameManager.SetPlayerSelections_P2P(selections);
                GameManager.StartGame();
            }
        }
    }

    // ===============================================================
    // UI DURANTE O JOGO
    // ===============================================================
    public void OnGameStart()
    {
        Debug.Log("[UIManager] OnGameStart chamado - escondendo menus e mostrando HUD");
        painelPrincipal.SetActive(false);
        multiplayerPanel.SetActive(false);
        inGameHudContainer.SetActive(true);
        
        if (advancedCameraController != null) 
            advancedCameraController.enabled = true;
    }

    public void OpenNewWeaponPanel(WeaponData weaponData)
    {
        newWeaponPanel.SetActive(true);
        weaponNameText.text = weaponData.weaponName;
        weaponSpriteImage.sprite = weaponData.icon;
        if (GameManager != null) GameManager.RequestPauseForLevelUp();
    }

    public void CloseNewWeaponPanel()
    {
        newWeaponPanel.SetActive(false);
        if (GameManager != null) GameManager.ResumeAfterLevelUp();
    }

    public void UpdateTimerText(float time)
    {
        int minutes = Mathf.FloorToInt(time / 60);
        int seconds = Mathf.FloorToInt(time % 60);
        timerText.text = $"{minutes:00}:{seconds:00}";
    }

    public void UpdateHealthBar(float currentHealth, float maxHealth)
    {
        if (healthBar != null)
        {
            healthBar.maxValue = maxHealth;
            healthBar.value = currentHealth;
        }
    }

    public void ShowEndGamePanel(bool show)
    {
        endGamePanel?.SetActive(show);
        if (show) inGameHudContainer.SetActive(false);
    }

    public void PlayAgainButton()
    {
        if (GameManager != null) GameManager.RestartGame();
    }

    public void ShowPauseMenu(bool show)
    {
        pauseMenu?.SetActive(show);
    }

    public void ToggleStatsPanel()
    {
        if (statsPanel != null)
            statsPanel.SetActive(!statsPanel.activeSelf);
    }

    // ===============================================================
    // XP & LEVEL UI
    // ===============================================================
    public void UpdateXPBar(float currentXP, float requiredXP)
    {
        if (xpSlider != null)
        {
            xpSlider.maxValue = requiredXP;
            xpSlider.value = currentXP;
        }
    }

    public void UpdateLevelText(int level)
    {
        if (levelText != null)
        {
            levelText.text = $"Nível {level}";
        }
    }
}