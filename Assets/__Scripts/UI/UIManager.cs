// Filename: UIManager.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using MyGame.ConnectionSystem.Connection; // Make sure you have access to this namespace

// This script now listens to events from the ConnectionManager to update the UI.
// This makes the UI logic cleaner and more reliable.

public class UIManager : MonoBehaviour
{
    [Header("Lobby UI")]
    [SerializeField] private GameObject lobbyPanel;
    [SerializeField] private LobbyUI lobbyUI; // Optional: If you have specific logic in this component

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

    // --- System Dependencies ---
    private ConnectionManager connectionManager;
    private GameManager gameManager;

    private int selectedCharacterIndex = 0;

    #region Unity Lifecycle & Event Subscription

    void Awake()
    {
        // Find the ConnectionManager instance as soon as the script awakens.
        connectionManager = ConnectionManager.Instance;
        if (connectionManager == null)
        {
            Debug.LogError("FATAL ERROR: UIManager could not find ConnectionManager.Instance! Is it in the scene?", this);
            this.enabled = false; // Disable this script if the manager is missing.
            return;
        }

        // Subscribe to events from the ConnectionManager. This is the core of the new logic.
        // The UIManager will now REACT to connection events instead of trying to manage the state itself.
        connectionManager.OnStartingHost += HandleStartingHost;
        connectionManager.OnHostCreated += HandleHostSuccess;
        connectionManager.OnStartingClient += HandleStartingClient;
        connectionManager.OnClientConnected += HandleClientSuccess;
        connectionManager.OnConnectionFailed += HandleConnectionFailure;
    }

    void Start()
    {
        // Set the initial state of all UI panels when the game starts.
        painelPrincipal.SetActive(true);
        multiplayerPanel.SetActive(false);
        lobbyPanel.SetActive(false);
        inGameHudContainer.SetActive(false);
        endGamePanel.SetActive(false);
        pauseMenu.SetActive(false);
    }

    void OnDestroy()
    {
        // IMPORTANT: Always unsubscribe from events when the object is destroyed to prevent memory leaks.
        if (connectionManager != null)
        {
            connectionManager.OnStartingHost -= HandleStartingHost;
            connectionManager.OnHostCreated -= HandleHostSuccess;
            connectionManager.OnStartingClient -= HandleStartingClient;
            connectionManager.OnClientConnected -= HandleClientSuccess;
            connectionManager.OnConnectionFailed -= HandleConnectionFailure;
        }
    }

    #endregion

    // Helper property to safely get the GameManager instance only when needed.
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
        if (GameManager != null) GameManager.isP2P = false;
        painelPrincipal.SetActive(false);
        if (GameManager != null)
        {
            GameManager.StartGame();
            OnGameStart();
        }
    }

    public void MultiplayerButton()
    {
        if (GameManager != null) GameManager.isP2P = true;
        painelPrincipal.SetActive(false);
        multiplayerPanel.SetActive(true);
        hostButton.interactable = true;
        joinButton.interactable = true;
    }

    public void BackToMainMenu()
    {
        multiplayerPanel.SetActive(false);
        lobbyPanel.SetActive(false); // Ensure lobby is also hidden
        painelPrincipal.SetActive(true);

        // Optional: If a player is in a lobby and clicks back, you might want to disconnect them.
        // connectionManager?.RequestShutdown(); 
    }

    public void QuitButton() => Application.Quit();

    #endregion

    #region Multiplayer Button Clicks

    // These methods are called by the UI Buttons in the Inspector.
    // They are now very simple, only telling the ConnectionManager what to do.

    public void OnHostButtonClicked()
    {
        hostButton.interactable = false; // Prevent double-clicking
        joinButton.interactable = false;
        connectionManager.StartHost("HostPlayer");
    }

    public void OnJoinButtonClicked()
    {
        string joinCode = joinCodeInput.text;
        if (string.IsNullOrWhiteSpace(joinCode))
        {
            // Use the central failure handler for immediate feedback.
            HandleConnectionFailure("Join Code cannot be empty!");
            return;
        }
        hostButton.interactable = false; // Prevent double-clicking
        joinButton.interactable = false;
        connectionManager.StartClient("ClientPlayer", joinCode);
    }

    #endregion

    #region Event Handlers (Called by ConnectionManager)

    // These methods contain the UI logic and are triggered by events.

    private void HandleStartingHost()
    {
        ShowLobbyPanel();
        SetConnectionStatus("Creating lobby...", Color.white);
    }

    private void HandleHostSuccess(string lobbyCode)
    {
        // This method is called by the ConnectionManager when the host is ready.

        // You can keep this line if you still have a status text on the UIManager itself.
        SetConnectionStatus("Lobby created! Waiting for players...", Color.green);

        // --- THIS IS THE NEW LINE ---
        // Pass the code to the LobbyUI component to display it there.
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
        SetConnectionStatus("Connected!", Color.green);
    }

    private void HandleConnectionFailure(string errorMessage)
    {
        // This single method handles all connection failures.
        if (lobbyPanel.activeSelf)
        {
            // If we were already in the lobby UI, show the error there.
            SetConnectionStatus($"ERROR: {errorMessage}", Color.red);
            // After a delay, go back to the multiplayer menu.
            Invoke(nameof(ReturnToMultiplayerMenu), 3f);
        }
        else if (multiplayerPanel.activeSelf)
        {
            // If we are still on the multiplayer menu (e.g., empty join code).
            SetConnectionStatus($"ERROR: {errorMessage}", Color.red);
            // Re-enable buttons so the user can try again.
            hostButton.interactable = true;
            joinButton.interactable = true;
        }
    }

    #endregion

    #region UI Helper Methods

    private void ShowLobbyPanel()
    {
        // This is the key function: hides the multiplayer menu and shows the lobby.
        multiplayerPanel.SetActive(false);
        lobbyPanel.SetActive(true);
        connectionStatusText.gameObject.SetActive(true); // Ensure status text is visible in lobby
        lobbyCodeText.gameObject.SetActive(false); // Hide code text until it's ready
    }

    private void ReturnToMultiplayerMenu()
    {
        // Used to reset the UI after a connection failure.
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

    private void UpdateLobbyCodeText(string lobbyCode)
    {
        if (lobbyCodeText == null) return;
        lobbyCodeText.gameObject.SetActive(true);
        lobbyCodeText.text = $"LOBBY CODE:\n{lobbyCode}";
    }

    #endregion

    #region In-Game and Gameplay UI

    public void OnGameStart()
    {
        // Hide all menu panels and show the in-game HUD.
        painelPrincipal.SetActive(false);
        multiplayerPanel.SetActive(false);
        lobbyPanel.SetActive(false);
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