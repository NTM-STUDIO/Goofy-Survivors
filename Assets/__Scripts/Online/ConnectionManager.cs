// Filename: ConnectionManager.cs
using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using MyGame.ConnectionSystem.Data;
using MyGame.ConnectionSystem.Services; // --- ADD THIS NAMESPACE ---
using MyGame.ConnectionSystem.States;

namespace MyGame.ConnectionSystem.Connection
{
    public class ConnectionManager : MonoBehaviour
    {
        public static ConnectionManager Instance { get; private set; }

        private NetworkManager m_NetworkManager;
        public NetworkManager NetworkManager => m_NetworkManager;
        
        private ConnectionState m_CurrentState;
        public int MaxConnectedPlayers = 8;
        internal ConnectionMethodBase m_ConnectionMethod;

        // --- CORRECTION: Change protection level from private (default) to internal ---
        internal readonly OfflineState m_Offline = new();
        internal readonly StartingHostState m_StartingHost = new();
        internal readonly HostingState m_Hosting = new();
        internal readonly ClientConnectingState m_ClientConnecting = new();
        internal readonly ClientConnectedState m_ClientConnected = new();
        internal readonly ClientReconnectingState m_ClientReconnecting = new();

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        void Start()
        {
            m_NetworkManager = FindObjectOfType<NetworkManager>();
            if (m_NetworkManager == null)
            {
                Debug.LogError("[ConnectionManager] FATAL: NetworkManager not found in scene!");
                return;
            }

            // Initialize all states, passing dependencies manually
            var states = new List<ConnectionState> { m_Offline, m_StartingHost, m_Hosting, m_ClientConnecting, m_ClientConnected, m_ClientReconnecting };
            foreach (var state in states)
            {
                // Ensure the other singletons exist before trying to pass them
                if (MultiplayerServicesFacade.Instance == null || ProfileManager.Instance == null)
                {
                    Debug.LogError("[ConnectionManager] FATAL: A required manager (Facade or Profile) is missing its Instance!");
                    return;
                }
                state.Initialize(this, MultiplayerServicesFacade.Instance, ProfileManager.Instance);
            }

            m_CurrentState = m_Offline;
            m_CurrentState.Enter();

            m_NetworkManager.OnConnectionEvent += OnConnectionEvent;
            m_NetworkManager.OnServerStarted += OnServerStarted;
            m_NetworkManager.ConnectionApprovalCallback += ApprovalCheck;
            m_NetworkManager.OnTransportFailure += OnTransportFailure;
            m_NetworkManager.OnServerStopped += OnServerStopped;
        }

        void OnDestroy()
        {
            if (m_NetworkManager != null)
            {
                m_NetworkManager.OnConnectionEvent -= OnConnectionEvent;
                m_NetworkManager.OnServerStarted -= OnServerStarted;
                m_NetworkManager.ConnectionApprovalCallback -= ApprovalCheck;
                m_NetworkManager.OnTransportFailure -= OnTransportFailure;
                m_NetworkManager.OnServerStopped -= OnServerStopped;
            }
        }

        internal void ChangeState(ConnectionState nextState)
        {
            Debug.Log($"[ConnectionManager] State Change: {m_CurrentState?.GetType().Name ?? "NULL"} -> {nextState.GetType().Name}");
            m_CurrentState?.Exit();
            m_CurrentState = nextState;
            m_CurrentState.Enter();
        }

        private void OnConnectionEvent(NetworkManager nm, ConnectionEventData data)
        {
            if (data.EventType == ConnectionEvent.ClientConnected) m_CurrentState.OnClientConnected(data.ClientId);
            else if (data.EventType == ConnectionEvent.ClientDisconnected) m_CurrentState.OnClientDisconnect(data.ClientId);
        }

        private void OnServerStarted() => m_CurrentState.OnServerStarted();
        private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest req, NetworkManager.ConnectionApprovalResponse res) => m_CurrentState.ApprovalCheck(req, res);
        private void OnTransportFailure() => m_CurrentState.OnTransportFailure();
        private void OnServerStopped(bool wasHost) => m_CurrentState.OnServerStopped();

        public void StartHost(string playerName) => m_CurrentState.StartHostSession(playerName);
        public void StartClient(string playerName, string joinCode) => m_CurrentState.StartClientSession(playerName, joinCode);
        public void RequestShutdown() => m_CurrentState.OnUserRequestedShutdown();
    }
}