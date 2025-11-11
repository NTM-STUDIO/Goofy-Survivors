// Filename: ConnectionState.cs
// Location: _Scripts/ConnectionSystem/States/
using System;
using Unity.Netcode;
using UnityEngine;
using MyGame.ConnectionSystem.Connection;
using MyGame.ConnectionSystem.Data;
using MyGame.ConnectionSystem.Services;

namespace MyGame.ConnectionSystem.States
{
    public abstract class ConnectionState
    {
        protected ConnectionManager m_ConnectionManager;
        protected ProfileManager m_ProfileManager;
        protected MultiplayerServicesFacade m_UGSFacade;

        public virtual void Initialize(ConnectionManager connectionManager, MultiplayerServicesFacade ugsFacade, ProfileManager profileManager)
        {
            m_ConnectionManager = connectionManager;
            m_UGSFacade = ugsFacade;
            m_ProfileManager = profileManager;
        }

        public virtual void Enter() {}
        public virtual void Exit() {}
        public virtual void OnClientConnected(ulong clientId) {}
        public virtual void OnClientDisconnect(ulong clientId) {}
        public virtual void OnServerStarted() {}
        public virtual void OnServerStopped() {}
        public virtual void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response) {}
        public virtual void OnTransportFailure() {}
        public virtual void OnUserRequestedShutdown() {}
        public virtual void StartHostSession(string playerName) {}
        public virtual void StartClientSession(string playerName, string joinCode) {}
    }

    public class OfflineState : ConnectionState
    {
        public override void StartHostSession(string playerName)
        {
            m_ConnectionManager.m_ConnectionMethod = new ConnectionMethodRelay(m_UGSFacade, m_ConnectionManager, m_ProfileManager, playerName);
            m_ConnectionManager.ChangeState(m_ConnectionManager.m_StartingHost);
        }

        public override void StartClientSession(string playerName, string joinCode)
        {
            m_UGSFacade.JoinCodeToJoin = joinCode;
            m_ConnectionManager.m_ConnectionMethod = new ConnectionMethodRelay(m_UGSFacade, m_ConnectionManager, m_ProfileManager, playerName);
            m_ConnectionManager.ChangeState(m_ConnectionManager.m_ClientConnecting);
        }
    }

    public class StartingHostState : ConnectionState
    {
        public override async void Enter()
        {
            try
            {
                var (success, session) = await m_UGSFacade.TryCreateSessionAsync("My Game", m_ConnectionManager.MaxConnectedPlayers, false);
                if (!success)
                {
                    Debug.LogError("Failed to create UGS Session.");
                    m_ConnectionManager.ChangeState(m_ConnectionManager.m_Offline);
                    return;
                }

                m_ConnectionManager.m_ConnectionMethod.SetupHostConnection();

                if (!m_ConnectionManager.NetworkManager.StartHost())
                {
                    Debug.LogError("NetworkManager failed to start host.");
                    m_ConnectionManager.ChangeState(m_ConnectionManager.m_Offline);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Error while starting host: " + e);
                m_ConnectionManager.ChangeState(m_ConnectionManager.m_Offline);
            }
        }

        public override void OnServerStarted()
        {
            m_ConnectionManager.ChangeState(m_ConnectionManager.m_Hosting);
        }

        public override void OnTransportFailure()
        {
            Debug.LogError("Transport failure prevented host from starting.");
            m_ConnectionManager.ChangeState(m_ConnectionManager.m_Offline);
        }
    }

    public class HostingState : ConnectionState
    {
        public override void OnUserRequestedShutdown()
        {
            m_ConnectionManager.NetworkManager.Shutdown();
        }

        public override void OnServerStopped()
        {
            m_ConnectionManager.ChangeState(m_ConnectionManager.m_Offline);
        }

        public override void ApprovalCheck(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            bool approve = m_ConnectionManager.NetworkManager.ConnectedClientsIds.Count < m_ConnectionManager.MaxConnectedPlayers;
            response.Approved = approve;
            // We spawn players manually in GameManager.StartGame_P2P_Host()
            // so clients won't receive a PlayerObject until the host starts the match.
            response.CreatePlayerObject = false;
            response.Pending = false;
        }
    }

    public class ClientConnectingState : ConnectionState
    {
        public override async void Enter()
        {
            try
            {
                var (success, session) = await m_UGSFacade.TryJoinSessionAsync(m_UGSFacade.JoinCodeToJoin);
                if (!success)
                {
                    Debug.LogError("Failed to join UGS Session.");
                    m_ConnectionManager.ChangeState(m_ConnectionManager.m_Offline);
                    return;
                }

                m_ConnectionManager.m_ConnectionMethod.SetupClientConnection();

                if (!m_ConnectionManager.NetworkManager.StartClient())
                {
                    Debug.LogError("NetworkManager failed to start client.");
                    m_ConnectionManager.ChangeState(m_ConnectionManager.m_Offline);
                }
            }
            catch (Exception e)
            {
                Debug.LogError("Error while connecting as client: " + e);
                m_ConnectionManager.ChangeState(m_ConnectionManager.m_Offline);
            }
        }

        public override void OnClientConnected(ulong clientId)
        {
            if (clientId == m_ConnectionManager.NetworkManager.LocalClientId)
            {
                m_ConnectionManager.ChangeState(m_ConnectionManager.m_ClientConnected);
            }
        }

        public override void OnTransportFailure()
        {
            Debug.LogError("Transport failure prevented client from connecting.");
            m_ConnectionManager.ChangeState(m_ConnectionManager.m_Offline);
        }
    }
    
    public class ClientConnectedState : ConnectionState
    {
        public override void OnUserRequestedShutdown()
        {
            m_ConnectionManager.NetworkManager.Shutdown();
        }

        public override void OnClientDisconnect(ulong clientId)
        {
            if (clientId == m_ConnectionManager.NetworkManager.LocalClientId)
            {
                m_ConnectionManager.ChangeState(m_ConnectionManager.m_Offline);
            }
        }
    }

    public class ClientReconnectingState : ConnectionState { }
}