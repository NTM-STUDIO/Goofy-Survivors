// Filename: ConnectionManager.cs
using System;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Netcode;
using Unity.Collections; // Required for NetworkList and FixedString
using MyGame.ConnectionSystem.Data;
using MyGame.ConnectionSystem.Services;
using MyGame.ConnectionSystem.States;

// This struct holds the synchronized data for a single player in the lobby.
// It must implement INetworkSerializable for Netcode to send it across the network.
public struct PlayerData : INetworkSerializable, IEquatable<PlayerData>
{
    public ulong ClientId;
    public FixedString64Bytes PlayerName; // Using FixedString because it's network-safe.

    // The serialization method tells Netcode how to read/write this struct's data.
    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref PlayerName);
    }

    // This is useful for comparing two PlayerData structs.
    public bool Equals(PlayerData other)
    {
        return ClientId == other.ClientId && PlayerName.Equals(other.PlayerName);
    }
}


namespace MyGame.ConnectionSystem.Connection
{
    /// <summary>
    /// Manages the connection state, player data, and provides events for the UI to hook into.
    /// This is a non-networked MonoBehaviour that controls the NetworkManager.
    /// </summary>
    public class ConnectionManager : MonoBehaviour
    {
        public static ConnectionManager Instance { get; private set; }

        private NetworkManager m_NetworkManager;
        public NetworkManager NetworkManager => m_NetworkManager;

        // This is the synchronized "source of truth" for who is in the lobby.
        public NetworkList<PlayerData> PlayerList { get; private set; }

        #region Events
        // Event for when the process of hosting begins.
        public event Action OnStartingHost;
        // Event for when the lobby code has been successfully created.
        public event Action<string> OnHostCreated;
        // Event for when the process of joining as a client begins.
        public event Action OnStartingClient;
        // Event for when the local client successfully connects to a server.
        public event Action OnClientConnected;
        // Event that fires when the local player has successfully started and connected as the host.
        public event Action OnHostingStarted;
        // Event for when any connection attempt fails.
        public event Action<string> OnConnectionFailed;
        #endregion

        #region State Machine
        private ConnectionState m_CurrentState;
        public int MaxConnectedPlayers = 8;
        internal ConnectionMethodBase m_ConnectionMethod;

        internal readonly OfflineState m_Offline = new();
        internal readonly StartingHostState m_StartingHost = new();
        internal readonly HostingState m_Hosting = new();
        internal readonly ClientConnectingState m_ClientConnecting = new();
        internal readonly ClientConnectedState m_ClientConnected = new();
        internal readonly ClientReconnectingState m_ClientReconnecting = new();
        #endregion

        #region Unity Lifecycle
        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Initialize the synchronized list.
            PlayerList = new NetworkList<PlayerData>();
        }

        void Start()
        {
            m_NetworkManager = FindObjectOfType<NetworkManager>();
            if (m_NetworkManager == null)
            {
                Debug.LogError("[ConnectionManager] FATAL: NetworkManager not found in scene!");
                return;
            }

            // Initialize all states
            var states = new System.Collections.Generic.List<ConnectionState> { m_Offline, m_StartingHost, m_Hosting, m_ClientConnecting, m_ClientConnected, m_ClientReconnecting };
            foreach (var state in states)
            {
                if (MultiplayerServicesFacade.Instance == null || ProfileManager.Instance == null)
                {
                    Debug.LogError("[ConnectionManager] FATAL: A required manager (Facade or Profile) is missing its Instance!");
                    return;
                }
                state.Initialize(this, MultiplayerServicesFacade.Instance, ProfileManager.Instance);
            }

            m_CurrentState = m_Offline;
            m_CurrentState.Enter();

            // Subscribe to NetworkManager events
            m_NetworkManager.OnClientConnectedCallback += HandleClientConnected;
            m_NetworkManager.OnClientDisconnectCallback += HandleClientDisconnected;
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
                // ALWAYS unsubscribe from events to prevent memory leaks and errors.
                m_NetworkManager.OnClientConnectedCallback -= HandleClientConnected;
                m_NetworkManager.OnClientDisconnectCallback -= HandleClientDisconnected;
                m_NetworkManager.OnConnectionEvent -= OnConnectionEvent;
                m_NetworkManager.OnServerStarted -= OnServerStarted;
                m_NetworkManager.ConnectionApprovalCallback -= ApprovalCheck;
                m_NetworkManager.OnTransportFailure -= OnTransportFailure;
                m_NetworkManager.OnServerStopped -= OnServerStopped;
            }
        }
        #endregion

        #region Player List Management (Server-Side)
        private void HandleClientConnected(ulong clientId)
        {
            if (!m_NetworkManager.IsServer) return;
            PlayerList.Add(new PlayerData { ClientId = clientId, PlayerName = $"Player {clientId + 1}" });
        }

        private void HandleClientDisconnected(ulong clientId)
        {
            if (!m_NetworkManager.IsServer) return;
            for (int i = 0; i < PlayerList.Count; i++)
            {
                if (PlayerList[i].ClientId == clientId)
                {
                    PlayerList.RemoveAt(i);
                    break;
                }
            }
        }
        #endregion

        #region Event Firing Logic
        private void OnConnectionEvent(NetworkManager nm, ConnectionEventData data)
        {
            if (data.EventType == ConnectionEvent.ClientConnected)
            {
                m_CurrentState.OnClientConnected(data.ClientId);

                // If the client that connected is this local machine...
                if (data.ClientId == m_NetworkManager.LocalClientId)
                {
                    OnClientConnected?.Invoke();

                    // And if this machine is also the server, it means we have successfully started hosting.
                    // This is the correct time to fire the event for host-specific UI.
                    if (m_NetworkManager.IsServer)
                    {
                        Debug.Log("[ConnectionManager] Host has connected to itself. Firing OnHostingStarted event.");
                        OnHostingStarted?.Invoke();
                    }
                }
            }
            else if (data.EventType == ConnectionEvent.ClientDisconnected)
            {
                m_CurrentState.OnClientDisconnect(data.ClientId);
            }
        }
        #endregion

        #region State Machine and Connection Logic
        internal void ChangeState(ConnectionState nextState)
        {
            Debug.Log($"[ConnectionManager] State Change: {m_CurrentState?.GetType().Name ?? "NULL"} -> {nextState.GetType().Name}");
            m_CurrentState?.Exit();
            m_CurrentState = nextState;
            m_CurrentState.Enter();
        }

        private void OnServerStarted() => m_CurrentState.OnServerStarted();
        private void ApprovalCheck(NetworkManager.ConnectionApprovalRequest req, NetworkManager.ConnectionApprovalResponse res) => m_CurrentState.ApprovalCheck(req, res);

        private void OnTransportFailure()
        {
            OnConnectionFailed?.Invoke("Transport failure!");
            m_CurrentState.OnTransportFailure();
        }

        private void OnServerStopped(bool wasHost)
        {
            // Clear the list when the server stops to ensure clients see an empty lobby if they reconnect.
            if (m_NetworkManager.IsServer)
            {
                PlayerList.Clear();
            }
            m_CurrentState.OnServerStopped();
        }

        public async void StartHost(string playerName)
        {
            OnStartingHost?.Invoke();
            try
            {
                m_CurrentState.StartHostSession(playerName);
                string lobbyCode = await WaitForLobbyCode();
                OnHostCreated?.Invoke(lobbyCode);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to start host: {e.Message}");
                OnConnectionFailed?.Invoke("Failed to create lobby.");
            }
        }

        public async void StartClient(string playerName, string joinCode)
        {
            OnStartingClient?.Invoke();
            try
            {
                m_CurrentState.StartClientSession(playerName, joinCode);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to start client: {e.Message}");
                OnConnectionFailed?.Invoke("Failed to join lobby.");
            }
        }

        private async Task<string> WaitForLobbyCode()
        {
            float timeout = 15f;
            float time = 0;
            while (time < timeout)
            {
                if (!string.IsNullOrEmpty(MultiplayerServicesFacade.Instance.LobbyJoinCode))
                {
                    return MultiplayerServicesFacade.Instance.LobbyJoinCode;
                }
                await Task.Delay(100);
                time += 0.1f;
            }
            throw new TimeoutException("Timed out waiting for lobby code.");
        }

        public void RequestShutdown() => m_CurrentState.OnUserRequestedShutdown();
        #endregion
    }
}