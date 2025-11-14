// Filename: LobbyUI.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using MyGame.ConnectionSystem.Connection; // Needed to find the ConnectionManager

/// <summary>
/// Manages the user interface for the multiplayer lobby.
/// This script is responsible for displaying the list of connected players,
/// the lobby code, and host-specific controls like the "Start Game" button.
/// It is purely a "presenter" and gets its data from the ConnectionManager.
/// </summary>
public class LobbyUI : MonoBehaviour
{
    [Header("UI Elements")]
    [Tooltip("The parent object where player slot prefabs will be instantiated.")]
    [SerializeField] private Transform playerSlotsParent;

    [Tooltip("The UI prefab for a single player slot. IMPORTANT: This prefab must NOT have a NetworkObject component on it.")]
    [SerializeField] private GameObject playerSlotPrefab;

    [Tooltip("The TextMeshPro label used to display the lobby's join code.")]
    [SerializeField] private TextMeshProUGUI lobbyCodeLabel;

    [Header("Host Controls")]
    [Tooltip("The button that only the host can see to start the game.")]
    [SerializeField] private Button startGameButton;

    private ConnectionManager connectionManager;

    // OnEnable is called every time the UI panel is set to active.
    // This is the best place to subscribe to events.
    void OnEnable()
    {
        connectionManager = ConnectionManager.Instance;
        if (connectionManager != null)
        {
            // Subscribe to both the player list and the hosting status events.
            connectionManager.PlayerList.OnListChanged += HandlePlayerListChanged;
            connectionManager.OnHostingStarted += HandleHostingStarted;
        }
        else
        {
            Debug.LogError("LobbyUI could not find the ConnectionManager instance! Ensure it is in the scene and its execution order is set correctly.", this);
        }

        // Initially hide the start button. It will be enabled by the HandleHostingStarted event if this player is the host.
        if(startGameButton != null)
        {
            startGameButton.gameObject.SetActive(false);
        }

        // Perform an initial draw of the lobby state in case we were already connected
        // before this UI panel became visible.
        RedrawLobby();
    }

    // OnDisable is called every time the UI panel is deactivated.
    // It is CRITICAL to unsubscribe from events here to prevent errors and memory leaks.
    void OnDisable()
    {
        if (connectionManager != null)
        {
            connectionManager.PlayerList.OnListChanged -= HandlePlayerListChanged;
            connectionManager.OnHostingStarted -= HandleHostingStarted;
        }
    }

    /// <summary>
    /// Event handler called by ConnectionManager only when the local player has successfully become the host.
    /// </summary>
    private void HandleHostingStarted()
    {
        UpdateStartButtonVisibility();
    }

    /// <summary>
    /// Event handler called on all clients whenever the PlayerList changes on the host.
    /// </summary>
    private void HandlePlayerListChanged(NetworkListEvent<PlayerData> changeEvent)
    {
        RedrawLobby();
    }

    /// <summary>
    /// Clears the existing UI slots and creates new ones based on the current PlayerList.
    /// </summary>
    private void RedrawLobby()
    {
        if (connectionManager == null || playerSlotsParent == null) return;

        // Clear all previously created player slot GameObjects to prevent duplicates.
        foreach (Transform child in playerSlotsParent)
        {
            Destroy(child.gameObject);
        }

        // Create a new UI slot for each player currently in the synchronized list.
        foreach (PlayerData player in connectionManager.PlayerList)
        {
            GameObject slotInstance = Instantiate(playerSlotPrefab, playerSlotsParent);
            var label = slotInstance.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                // PlayerName is a FixedString, so we must convert it to a regular string for the UI.
                label.text = player.PlayerName.ToString();
            }
        }
    }

    /// <summary>
    /// Checks if the local player is the host and shows/hides the start button.
    /// This is now called by the HandleHostingStarted event at the correct time.
    /// </summary>
    public void UpdateStartButtonVisibility()
    {
        if (startGameButton != null)
        {
            bool isHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
            bool isServer = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
            
            // IsHost = IsServer && IsClient, so if we're server we should be host in this context
            bool shouldShowButton = isHost || isServer;
            
            Debug.Log($"[LobbyUI] UpdateStartButtonVisibility - IsHost: {isHost}, IsServer: {isServer}, Showing button: {shouldShowButton}");
            startGameButton.gameObject.SetActive(shouldShowButton);
        }
        else
        {
            Debug.LogWarning("[LobbyUI] UpdateStartButtonVisibility - startGameButton is NULL!");
        }
    }

    /// <summary>
    /// A public method that the UIManager can call to set the lobby code text.
    /// </summary>
    public void DisplayLobbyCode(string code)
    {
        if (lobbyCodeLabel != null)
        {
            lobbyCodeLabel.gameObject.SetActive(true);
            lobbyCodeLabel.text = $"CODE: {code}";
        }
    }

    /// <summary>
    /// This method is linked to the OnClick event of the Start Game Button in the Unity Editor.
    /// </summary>
    public void OnStartGameButtonClicked()
    {
        // We only need to tell the GameManager to start. Its internal logic already
        // ensures that only the host can actually begin the match.
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StartGame();
        }
        else
        {
            Debug.LogError("[LobbyUI] Could not find GameManager instance to start the game!", this);
        }
    }
}