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
    private bool isSubscribed = false;

    // OnEnable is called every time the UI panel is set to active.
    // This is the best place to subscribe to events.
    void OnEnable()
    {
        connectionManager = ConnectionManager.Instance;
        if (connectionManager != null)
        {
            // Subscribe to hosting event
            connectionManager.OnHostingStarted += HandleHostingStarted;
            
            // Start coroutine to wait for network setup and subscribe to PlayerList
            StartCoroutine(WaitAndInitialize());
        }
        else
        {
            Debug.LogError("LobbyUI could not find the ConnectionManager instance! Ensure it is in the scene and its execution order is set correctly.", this);
        }

        // Check if we're already host when UI opens
        UpdateStartButtonVisibility();
    }

    private System.Collections.IEnumerator WaitAndInitialize()
    {
        // Wait until NetworkManager is started and PlayerList is assigned
        while (connectionManager == null || 
               connectionManager.PlayerList == null || 
               NetworkManager.Singleton == null ||
               !NetworkManager.Singleton.IsListening)
        {
            yield return null;
        }

        // Subscribe to PlayerList changes if not already subscribed
        if (!isSubscribed && connectionManager.PlayerList != null)
        {
            connectionManager.PlayerList.OnListChanged += HandlePlayerListChanged;
            isSubscribed = true;
            Debug.Log("[LobbyUI] Subscribed to PlayerList changes");
        }

        // Give a small delay for NetworkList to replicate from host to clients
        yield return new WaitForSeconds(0.2f);

        // Now redraw with the replicated data
        Debug.Log($"[LobbyUI] Initial redraw - PlayerList count: {connectionManager.PlayerList.Count}");
        RedrawLobby();
        
        // Update button visibility again after network is ready
        UpdateStartButtonVisibility();
    }

    // OnDisable is called every time the UI panel is deactivated.
    // It is CRITICAL to unsubscribe from events here to prevent errors and memory leaks.
    void OnDisable()
    {
        if (connectionManager != null)
        {
            if (isSubscribed && connectionManager.PlayerList != null)
            {
                connectionManager.PlayerList.OnListChanged -= HandlePlayerListChanged;
                isSubscribed = false;
            }
            connectionManager.OnHostingStarted -= HandleHostingStarted;
        }
    }

    /// <summary>
    /// Event handler called by ConnectionManager only when the local player has successfully become the host.
    /// </summary>
    private void HandleHostingStarted()
    {
        Debug.Log("[LobbyUI] HandleHostingStarted called");
        UpdateStartButtonVisibility();
        RedrawLobby(); // Redraw when hosting starts
    }

    /// <summary>
    /// Event handler called on all clients whenever the PlayerList changes on the host.
    /// </summary>
    private void HandlePlayerListChanged(NetworkListEvent<PlayerData> changeEvent)
    {
        Debug.Log($"[LobbyUI] PlayerList changed - Event type: {changeEvent.Type}, Count: {connectionManager.PlayerList.Count}");
        RedrawLobby();
    }

    /// <summary>
    /// Clears the existing UI slots and creates new ones based on the current PlayerList.
    /// </summary>
    private void RedrawLobby()
    {
        if (connectionManager == null || playerSlotsParent == null)
        {
            Debug.LogWarning("[LobbyUI] RedrawLobby - ConnectionManager or playerSlotsParent is null");
            return;
        }

        if (connectionManager.PlayerList == null)
        {
            Debug.LogWarning("[LobbyUI] RedrawLobby - PlayerList is null");
            return;
        }

        Debug.Log($"[LobbyUI] RedrawLobby called - Player count: {connectionManager.PlayerList.Count}");

        // Clear all previously created player slot GameObjects to prevent duplicates.
        foreach (Transform child in playerSlotsParent)
        {
            Destroy(child.gameObject);
        }

        // Create a new UI slot for each player currently in the synchronized list.
        int slotIndex = 0;
        foreach (PlayerData player in connectionManager.PlayerList)
        {
            GameObject slotInstance = Instantiate(playerSlotPrefab, playerSlotsParent);
            var label = slotInstance.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
            {
                // PlayerName is a FixedString, so we must convert it to a regular string for the UI.
                string playerName = player.PlayerName.ToString();
                label.text = playerName;
                Debug.Log($"[LobbyUI] Created slot {slotIndex} for player: {playerName}");
            }
            else
            {
                Debug.LogWarning($"[LobbyUI] Player slot prefab is missing TextMeshProUGUI component!");
            }
            slotIndex++;
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
            Debug.Log($"[LobbyUI] Displaying lobby code: {code}");
        }
        else
        {
            Debug.LogWarning("[LobbyUI] DisplayLobbyCode - lobbyCodeLabel is null!");
        }
    }

    /// <summary>
    /// This method is linked to the OnClick event of the Start Game Button in the Unity Editor.
    /// </summary>
    public void OnStartGameButtonClicked()
    {
        Debug.Log("[LobbyUI] Start Game button clicked");
        
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