// Filename: RelaySessionManager.cs
using UnityEngine;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using TMPro;

public class RelaySessionManager : MonoBehaviour
{
    [SerializeField] private TMP_Text joinCodeText;
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private GameObject connectionPanel;

    private Lobby currentLobby;

    // --- MONOBEHAVIOUR & AUTHENTICATION ---

    async void Start()
    {
        // Initialize UGS and sign in anonymously
        await UnityServices.InitializeAsync();

        AuthenticationService.Instance.SignedIn += () => {
            Debug.Log("Signed in as: " + AuthenticationService.Instance.PlayerId);
        };
        
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
    }

    // --- HOSTING LOGIC ---

    public async void CreateRelayAndLobby()
    {
        try
        {
            // 1. CREATE RELAY
            // Ask Relay service for an allocation
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(3); // 3 = Max players - 1
            
            // Get the join code for the Relay
            string relayJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            Debug.Log("Relay Join Code: " + relayJoinCode);

            // 2. CREATE LOBBY (SESSION)
            // Create a lobby and put the Relay join code in its custom data
            CreateLobbyOptions options = new CreateLobbyOptions
            {
                Data = new System.Collections.Generic.Dictionary<string, DataObject>
                {
                    { "RELAY_JOIN_CODE", new DataObject(DataObject.VisibilityOptions.Member, relayJoinCode) }
                }
            };
            currentLobby = await LobbyService.Instance.CreateLobbyAsync("My Game Lobby", 4, options);
            
            // Display the LOBBY join code for players to use
            joinCodeText.text = "LOBBY CODE: " + currentLobby.LobbyCode;
            Debug.Log("Lobby created with code: " + currentLobby.LobbyCode);

            // 3. START NETWORK MANAGER AS HOST
            // Connect NetworkManager to the Relay server
            var unityTransport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            unityTransport.SetHostRelayData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes,
                allocation.Key,
                allocation.ConnectionData
            );
            
            NetworkManager.Singleton.StartHost();
            HideConnectionPanel();
        }
        catch (RelayServiceException e)
        {
            Debug.LogError("Relay creation failed: " + e.Message);
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError("Lobby creation failed: " + e.Message);
        }
    }

    // --- CLIENT LOGIC ---

    public async void JoinRelayAndLobby()
    {
        try
        {
            string lobbyCode = joinCodeInput.text;
            if (string.IsNullOrEmpty(lobbyCode))
            {
                Debug.LogError("Lobby code cannot be empty.");
                return;
            }

            // 1. JOIN LOBBY
            // Find and join the lobby using the code
            currentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode);
            Debug.Log("Joined lobby with code: " + lobbyCode);

            // 2. GET RELAY CODE FROM LOBBY
            // Extract the Relay join code from the lobby's custom data
            string relayJoinCode = currentLobby.Data["RELAY_JOIN_CODE"].Value;
            Debug.Log("Received Relay code: " + relayJoinCode);
            
            // 3. JOIN RELAY
            // Use the code to join the Relay allocation
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(relayJoinCode);
            
            // 4. START NETWORK MANAGER AS CLIENT
            // Connect NetworkManager to the Relay server
            var unityTransport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            unityTransport.SetClientRelayData(
                joinAllocation.RelayServer.IpV4,
                (ushort)joinAllocation.RelayServer.Port,
                joinAllocation.AllocationIdBytes,
                joinAllocation.Key,
                joinAllocation.ConnectionData,
                joinAllocation.HostConnectionData
            );
            
            NetworkManager.Singleton.StartClient();
            HideConnectionPanel();
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError("Failed to join lobby: " + e.Message);
        }
        catch (RelayServiceException e)
        {
            Debug.LogError("Failed to join relay: " + e.Message);
        }
    }
    
    private void HideConnectionPanel()
    {
        if (connectionPanel != null)
        {
            connectionPanel.SetActive(false);
        }
    }
}