// Filename: UIManager.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using MyGame.ConnectionSystem.Connection;
using System.Collections;


public class UIManager : MonoBehaviour
{
    // Fade animation for new weapon panel
    private CanvasGroup newWeaponPanelCanvasGroup;
    private Coroutine newWeaponPanelFadeCoroutine;
    [Header("Lobby UI")]
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private LobbyUI lobbyUI;
    [SerializeField] private Button startGameButton; // --- ADD THIS LINE ---
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
    [SerializeField] private GameObject[] characterPrefabs;

    [Header("System References")]
    [SerializeField] private AdvancedCameraController advancedCameraController;

    private ConnectionManager connectionManager;
    private GameManager gameManager;
    private int selectedCharacterIndex = 0;

    #region Unity Lifecycle & Event Subscription
    void Awake()
    {
        connectionManager = ConnectionManager.Instance;
        if (connectionManager == null)
        {
            Debug.LogError("FATAL ERROR: UIManager could not find ConnectionManager.Instance!", this);
            this.enabled = false;
            return;
        }

        connectionManager.OnStartingHost += HandleStartingHost;
        connectionManager.OnHostCreated += HandleHostSuccess;
        connectionManager.OnStartingClient += HandleStartingClient;
        connectionManager.OnClientConnected += HandleClientSuccess;
        connectionManager.OnConnectionFailed += HandleConnectionFailure;
    }

    void Start()
    {
        painelPrincipal.SetActive(true);
        multiplayerPanel.SetActive(false);
        lobbyPanel.SetActive(false);
        inGameHudContainer.SetActive(false);
        endGamePanel.SetActive(false);
        pauseMenu.SetActive(false);

        // Setup CanvasGroup for newWeaponPanel (for fade in/out)
        if (newWeaponPanel != null)
        {
            newWeaponPanelCanvasGroup = newWeaponPanel.GetComponent<CanvasGroup>();
            if (newWeaponPanelCanvasGroup == null)
                newWeaponPanelCanvasGroup = newWeaponPanel.AddComponent<CanvasGroup>();
            newWeaponPanelCanvasGroup.alpha = 0f;
            newWeaponPanel.SetActive(false);
        }
    }
#endregion

    public void OnStartGameButtonClicked()
    {
        // We use the GameManager helper property to safely get the instance.
        if (GameManager != null && NetworkManager.Singleton.IsHost)
        {
            Debug.Log("[UIManager] Start Game button clicked by host. Telling GameManager to start.");

            // Call the correct public method on your GameManager.
            // Your GameManager's internal logic will handle the rest.
            GameManager.StartGame();

            // Hide the lobby panel immediately as the game is starting.
            lobbyPanel.SetActive(false);
        }
    }

    void OnDestroy()
    {
        if (connectionManager != null)
        {
            connectionManager.OnStartingHost -= HandleStartingHost;
            connectionManager.OnHostCreated -= HandleHostSuccess;
            connectionManager.OnStartingClient -= HandleStartingClient;
            connectionManager.OnClientConnected -= HandleClientSuccess;
            connectionManager.OnConnectionFailed -= HandleConnectionFailure;
        }
    }


    private GameManager GameManager
    {
        get
        {
            if (gameManager == null) gameManager = GameManager.Instance;
            return gameManager;
        }
    }

    #region Main Menu Navigation
    public void PlayButton()
    {
        if (GameManager != null)
        {
            GameManager.isP2P = false;
            GameManager.StartGame();
        }
        painelPrincipal.SetActive(false);
        OnGameStart();
    }

    public void MultiplayerButton()
    {
        if (GameManager != null) GameManager.isP2P = true;
        painelPrincipal.SetActive(false);
        multiplayerPanel.SetActive(true);
        hostButton.interactable = true;
        joinButton.interactable = true;
    }

    // --- THIS IS THE NEW METHOD FOR YOUR LOBBY'S BACK BUTTON ---
    /// <summary>
    /// A specific "Back" button action that also shuts down the network connection.
    /// Use this for the "Back" button inside your lobby panel.
    /// </summary>
    public void BackToMainMenuAndDisconnect()
    {
        // Tell the ConnectionManager to shut down the current session.
        // This will disconnect the client or shut down the host.
        connectionManager?.RequestShutdown();

        // Immediately switch the UI back to the main menu.
        multiplayerPanel.SetActive(false);
        lobbyPanel.SetActive(false);
        painelPrincipal.SetActive(true);
    }

    public void QuitButton() => Application.Quit();
    #endregion

    #region Generic Panel Switching (for non-disconnecting buttons)
    public void OpenPanel(GameObject panelToOpen)
    {
        if (panelToOpen != null)
        {
            panelToOpen.SetActive(true);
        }
    }

    public void ClosePanel(GameObject panelToClose)
    {
        if (panelToClose != null)
        {
            panelToClose.SetActive(false);
        }
    }
    #endregion

    #region Multiplayer Button Clicks
    public void OnHostButtonClicked()
    {
        hostButton.interactable = false;
        joinButton.interactable = false;
        connectionManager.StartHost("HostPlayer");
    }

    public void OnJoinButtonClicked()
    {
        string joinCode = joinCodeInput.text;
        if (string.IsNullOrWhiteSpace(joinCode))
        {
            HandleConnectionFailure("Join Code cannot be empty!");
            return;
        }
        hostButton.interactable = false;
        joinButton.interactable = false;
        connectionManager.StartClient("ClientPlayer", joinCode);
    }
    #endregion

    #region Event Handlers
    private void HandleStartingHost()
    {
        // OLD WAY: ShowLobbyPanel();
        // OLD WAY: SetConnectionStatus("Creating lobby...", Color.white);

        // --- NEW WAY ---
        SetupLobbyUI(); // This now handles showing the panel and the start button
        SetConnectionStatus("Creating lobby...", Color.white); // We can still set the initial status
    }

    // In UIManager.cs

    /// <summary>
    /// A centralized method to configure the lobby panel's visibility and elements.
    /// This is the new, robust way to show the lobby.
    /// </summary>
    private void SetupLobbyUI()
    {
        // Hide all other major panels
        painelPrincipal.SetActive(false);
        multiplayerPanel.SetActive(false);
        inGameHudContainer.SetActive(false);
        endGamePanel.SetActive(false);

        // Show the main lobby panel
        lobbyPanel.SetActive(true);

        // --- THIS IS THE CRITICAL LOGIC ---
        // Check if the current player is the host.
        bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
        bool isServer = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
        
        // IsHost = IsServer && IsClient, so if we're server we should be host in this context
        bool shouldShowButton = isHost || isServer;
        
        Debug.Log($"[UIManager.SetupLobbyUI] NetworkManager exists: {NetworkManager.Singleton != null}, IsHost: {isHost}, IsServer: {isServer}, Showing button: {shouldShowButton}");

        // Only show the "Start Game" button if the player is the host.
        if (startGameButton != null)
        {
            Debug.Log($"[UIManager.SetupLobbyUI] Setting startGameButton active to: {shouldShowButton}");
            startGameButton.gameObject.SetActive(shouldShowButton);
        }
        else
        {
            Debug.LogWarning("[UIManager.SetupLobbyUI] startGameButton is NULL! Check Inspector assignment.");
        }

        // You could also update status text here
        if (shouldShowButton)
        {
            SetConnectionStatus("You are the host. Waiting for players...", Color.white);
        }
        else
        {
            SetConnectionStatus("Connected to lobby. Waiting for host to start...", Color.white);
        }
    }

    private void HandleHostSuccess(string lobbyCode)
    {
        SetConnectionStatus("Lobby created! Waiting for players...", Color.green);
        if (lobbyUI != null) lobbyUI.DisplayLobbyCode(lobbyCode);
    }

    private void HandleStartingClient()
    {
        ShowLobbyPanel();
        SetConnectionStatus("Joining lobby...", Color.white);
    }

    private void HandleClientSuccess()
    {
        // The client successfully connected to the host.
        // The old SetConnectionStatus is now handled by SetupLobbyUI.

        // --- NEW WAY ---
        SetupLobbyUI();
    }

    private void HandleConnectionFailure(string errorMessage)
    {
        if (lobbyPanel.activeSelf)
        {
            SetConnectionStatus($"ERROR: {errorMessage}", Color.red);
            Invoke(nameof(ReturnToMultiplayerMenu), 3f);
        }
        else if (multiplayerPanel.activeSelf)
        {
            SetConnectionStatus($"ERROR: {errorMessage}", Color.red);
            hostButton.interactable = true;
            joinButton.interactable = true;
        }
    }

    public void ReturnToLobby()
    {
        Debug.Log("[UIManager] Returning to Lobby UI.");

        // Hide in-game panels
        inGameHudContainer.SetActive(false);
        endGamePanel.SetActive(false);
        pauseMenu.SetActive(false);

        // --- NEW WAY ---
        // Call the central setup method, which will correctly show/hide the start button.
        SetupLobbyUI();
    }
    #endregion

    #region UI Helper Methods
    private void ShowLobbyPanel()
    {
        multiplayerPanel.SetActive(false);
        lobbyPanel.SetActive(true);
        connectionStatusText.gameObject.SetActive(true);
        lobbyCodeText.gameObject.SetActive(false);
    }

    private void ReturnToMultiplayerMenu()
    {
        lobbyPanel.SetActive(false);
        multiplayerPanel.SetActive(true);
        hostButton.interactable = true;
        joinButton.interactable = true;
    }

    private void SetConnectionStatus(string message, Color color)
    {
        if (connectionStatusText == null) return;
        connectionStatusText.gameObject.SetActive(true);
        connectionStatusText.text = message;
        connectionStatusText.color = color;
    }
    #endregion

    #region In-Game and Gameplay UI
    public void OnGameStart()
    {
        painelPrincipal.SetActive(false);
        multiplayerPanel.SetActive(false);
        lobbyPanel.SetActive(false);
        inGameHudContainer.SetActive(true);
        if (advancedCameraController != null) advancedCameraController.enabled = true;
    }

    public void OpenNewWeaponPanel(WeaponData weaponData)
    {
        if (newWeaponPanelFadeCoroutine != null)
            StopCoroutine(newWeaponPanelFadeCoroutine);
        newWeaponPanel.SetActive(true);
        if (newWeaponPanelCanvasGroup == null)
            newWeaponPanelCanvasGroup = newWeaponPanel.GetComponent<CanvasGroup>();
        newWeaponPanelCanvasGroup.alpha = 0f;
        weaponNameText.text = weaponData.weaponName;
        weaponSpriteImage.sprite = weaponData.icon;
        if (GameManager != null) GameManager.RequestPauseForLevelUp();
        newWeaponPanelFadeCoroutine = StartCoroutine(FadeCanvasGroup(newWeaponPanelCanvasGroup, 0f, 1f, 1f));
    }

    public void CloseNewWeaponPanel()
    {
        if (newWeaponPanelFadeCoroutine != null)
            StopCoroutine(newWeaponPanelFadeCoroutine);
        if (newWeaponPanelCanvasGroup == null)
            newWeaponPanelCanvasGroup = newWeaponPanel.GetComponent<CanvasGroup>();
        newWeaponPanelFadeCoroutine = StartCoroutine(FadeOutAndDeactivateNewWeaponPanel());
        if (GameManager != null) GameManager.ResumeAfterLevelUp();
    }

    private IEnumerator FadeOutAndDeactivateNewWeaponPanel()
    {
        yield return FadeCanvasGroup(newWeaponPanelCanvasGroup, 1f, 0f, 1f);
        newWeaponPanel.SetActive(false);
    }

    private IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to, float duration)
    {
        float elapsed = 0f;
        cg.alpha = from;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            cg.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        cg.alpha = to;
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
        Debug.Log("[UIManager] PlayAgainButton() called - delegating to GameManager.HandlePlayAgain()");
        
        // This button now calls the GameManager's central handler.
        if (GameManager != null)
        {
            GameManager.HandlePlayAgain();
        }
        else
        {
            // Fallback for safety
            Debug.LogError("GameManager not found! Cannot handle 'Play Again'.");
        }
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
#endregion
}