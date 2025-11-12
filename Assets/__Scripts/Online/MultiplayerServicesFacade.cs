// Filename: MultiplayerServicesFacade.cs
using System;
using System.Threading.Tasks;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using UnityEngine;

namespace MyGame.ConnectionSystem.Services
{
    public class MultiplayerServicesFacade : MonoBehaviour
    {
        public static MultiplayerServicesFacade Instance { get; private set; }

        private AuthenticationServiceFacade m_Auth;

        public Lobby CurrentLobby { get; private set; }
        public string RelayIP { get; private set; }
        public ushort RelayPort { get; private set; }
        public byte[] RelayAllocationId { get; private set; }
        public byte[] RelayKey { get; private set; }
        public byte[] RelayConnectionData { get; private set; }
        public byte[] RelayHostConnectionData { get; private set; }
        public string LobbyJoinCode { get; private set; }
        public string JoinCodeToJoin { get; set; } // This will now hold either a Lobby Code or a Lobby ID

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            m_Auth = new AuthenticationServiceFacade();
        }

        async void Start()
        {
            try
            {
                await m_Auth.EnsureSignedIn();
                Debug.Log("[MultiplayerServicesFacade] Unity Services Initialized and Player Signed In.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[MultiplayerServicesFacade] Failed to initialize services: {e}");
            }
        }

        public async Task<(bool Success, Lobby Session)> TryCreateSessionAsync(string sessionName, int maxPlayers, bool isPrivate)
        {
            try
            {
                var allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);
                RelayIP = allocation.RelayServer.IpV4;
                RelayPort = (ushort)allocation.RelayServer.Port;
                RelayAllocationId = allocation.AllocationIdBytes;
                RelayKey = allocation.Key;
                RelayConnectionData = allocation.ConnectionData;
                RelayHostConnectionData = allocation.ConnectionData;
                
                string relayJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
                var options = new CreateLobbyOptions { Data = new System.Collections.Generic.Dictionary<string, DataObject> { { "joinCode", new DataObject(DataObject.VisibilityOptions.Member, relayJoinCode) } } };
                CurrentLobby = await LobbyService.Instance.CreateLobbyAsync(sessionName, maxPlayers, options);
                LobbyJoinCode = CurrentLobby.LobbyCode;
                Debug.Log($"LOBBY CREATED! JOIN CODE: {LobbyJoinCode}");
                return (true, CurrentLobby);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                return (false, null);
            }
        }
        
        public async Task<(bool Success, Lobby Session)> TryJoinSessionAsync(string lobbyCode)
        {
            try
            {
                CurrentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode);
                var relayJoinCode = CurrentLobby.Data["joinCode"].Value;
                var joinAllocation = await RelayService.Instance.JoinAllocationAsync(relayJoinCode);
                RelayIP = joinAllocation.RelayServer.IpV4;
                RelayPort = (ushort)joinAllocation.RelayServer.Port;
                RelayAllocationId = joinAllocation.AllocationIdBytes;
                RelayKey = joinAllocation.Key;
                RelayConnectionData = joinAllocation.ConnectionData;
                RelayHostConnectionData = joinAllocation.HostConnectionData;
                return (true, CurrentLobby);
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                return (false, null);
            }
        }

        // --- THIS IS THE NEW METHOD FOR JOINING FROM THE LOBBY BROWSER ---
        public async Task<(bool Success, Lobby Session)> TryJoinSessionByIdAsync(string lobbyId)
        {
            try
            {
                CurrentLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId);
                var relayJoinCode = CurrentLobby.Data["joinCode"].Value;
                var joinAllocation = await RelayService.Instance.JoinAllocationAsync(relayJoinCode);
                
                RelayIP = joinAllocation.RelayServer.IpV4;
                RelayPort = (ushort)joinAllocation.RelayServer.Port;
                RelayAllocationId = joinAllocation.AllocationIdBytes;
                RelayKey = joinAllocation.Key;
                RelayConnectionData = joinAllocation.ConnectionData;
                RelayHostConnectionData = joinAllocation.HostConnectionData;
                
                return (true, CurrentLobby);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to join lobby by ID '{lobbyId}': {e}");
                return (false, null);
            }
        }
        
        public void BeginTracking() {}
        public void EndTracking() {}
    }
    
    public class AuthenticationServiceFacade
    {
        public async Task EnsureSignedIn()
        {
            if (UnityServices.State == ServicesInitializationState.Uninitialized)
            {
                await UnityServices.InitializeAsync();
            }
            
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }
        }
    }
}