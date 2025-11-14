// Filename: MultiplayerServicesFacade.cs
using System;
using System.Threading; // --- ADD THIS NAMESPACE --- for CancellationTokenSource
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
        
        // --- CORRECTION: Replaced Coroutine with a CancellationTokenSource for async Tasks ---
        private CancellationTokenSource m_LobbyTrackerCts;

        public Lobby CurrentLobby { get; private set; }
        public string RelayIP { get; private set; }
        public ushort RelayPort { get; private set; }
        public byte[] RelayAllocationId { get; private set; }
        public byte[] RelayKey { get; private set; }
        public byte[] RelayConnectionData { get; private set; }
        public byte[] RelayHostConnectionData { get; private set; }
        public string LobbyJoinCode { get; private set; }
        public string JoinCodeToJoin { get; set; }

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

        void OnDestroy()
        {
            // Ensure the tracking task is stopped when this object is destroyed.
            StopLobbyTracking();
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

                StartLobbyTracking();

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

        public async void LeaveLobby()
        {
            if (CurrentLobby == null) return;

            Debug.Log($"[MultiplayerServicesFacade] Leaving lobby: {CurrentLobby.Name}");
            StopLobbyTracking();

            try
            {
                if (CurrentLobby.HostId == AuthenticationService.Instance.PlayerId)
                {
                    await LobbyService.Instance.DeleteLobbyAsync(CurrentLobby.Id);
                }
                else
                {
                    await LobbyService.Instance.RemovePlayerAsync(CurrentLobby.Id, AuthenticationService.Instance.PlayerId);
                }
            }
            catch (LobbyServiceException e)
            {
                Debug.LogError($"Error leaving lobby: {e}");
            }
            finally
            {
                CurrentLobby = null;
                LobbyJoinCode = null;
            }
        }

        private void StartLobbyTracking()
        {
            StopLobbyTracking(); // Stop any previous tracking
            m_LobbyTrackerCts = new CancellationTokenSource();
            LobbyHeartbeatAndCleanup(m_LobbyTrackerCts.Token);
        }

        private void StopLobbyTracking()
        {
            if (m_LobbyTrackerCts != null)
            {
                m_LobbyTrackerCts.Cancel();
                m_LobbyTrackerCts.Dispose();
                m_LobbyTrackerCts = null;
            }
        }

        // --- CORRECTION: Converted from an IEnumerator to an async Task method ---
        private async void LobbyHeartbeatAndCleanup(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested && CurrentLobby != null)
            {
                // Only the host should be doing this.
                if (CurrentLobby.HostId == AuthenticationService.Instance.PlayerId)
                {
                    try
                    {
                        // 1. Send heartbeat to keep the lobby alive
                        await LobbyService.Instance.SendHeartbeatPingAsync(CurrentLobby.Id);
                        Debug.Log("[MultiplayerServicesFacade] Lobby heartbeat sent.");

                        // 2. Get latest lobby data to check for cleanup
                        CurrentLobby = await LobbyService.Instance.GetLobbyAsync(CurrentLobby.Id);
                        if (CurrentLobby.Players.Count <= 1)
                        {
                            Debug.Log("[MultiplayerServicesFacade] Host is the only player in the lobby.");
                        }
                    }
                    catch (LobbyServiceException e)
                    {
                        Debug.LogError($"Lobby tracking failed: {e}. Stopping tracking.");
                        CurrentLobby = null; // Stop the loop
                        break;
                    }
                }

                try
                {
                    // Wait for 15 seconds before the next heartbeat, cancellable.
                    await Task.Delay(15000, cancellationToken);
                }
                catch (TaskCanceledException)
                {
                    // This is expected when we call StopLobbyTracking().
                    break;
                }
            }

            Debug.Log("[MultiplayerServicesFacade] Lobby tracking task stopped.");
        }
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