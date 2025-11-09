// Filename: ConnectionMethod.cs
// Location: _Scripts/ConnectionSystem/Connection/
using System.Threading.Tasks;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using UnityEngine;
using MyGame.ConnectionSystem.Data;
using MyGame.ConnectionSystem.Services;

namespace MyGame.ConnectionSystem.Connection
{
    public abstract class ConnectionMethodBase
    {
        protected ConnectionManager m_ConnectionManager;
        readonly ProfileManager m_ProfileManager;
        protected readonly string m_PlayerName;

        public ConnectionMethodBase(ConnectionManager connectionManager, ProfileManager profileManager, string playerName)
        {
            m_ConnectionManager = connectionManager;
            m_ProfileManager = profileManager;
            m_PlayerName = playerName;
        }

        public abstract void SetupHostConnection();
        public abstract void SetupClientConnection();

        protected void SetConnectionPayload(string playerId, string playerName)
        {
            var payload = JsonUtility.ToJson(new ConnectionPayload { playerId = playerId, playerName = playerName });
            m_ConnectionManager.NetworkManager.NetworkConfig.ConnectionData = System.Text.Encoding.UTF8.GetBytes(payload);
        }

        protected string GetPlayerId()
        {
            if (UnityServices.State == ServicesInitializationState.Initialized && AuthenticationService.Instance.IsSignedIn)
            {
                return AuthenticationService.Instance.PlayerId;
            }
            return ClientPrefs.GetGuid();
        }
    }

    public class ConnectionMethodRelay : ConnectionMethodBase
    {
        MultiplayerServicesFacade m_UGSFacade;

        public ConnectionMethodRelay(MultiplayerServicesFacade ugsFacade, ConnectionManager connectionManager, ProfileManager profileManager, string playerName)
            : base(connectionManager, profileManager, playerName)
        {
            m_UGSFacade = ugsFacade;
        }

        public override void SetupHostConnection()
        {
            SetConnectionPayload(GetPlayerId(), m_PlayerName);
            var utp = (UnityTransport)m_ConnectionManager.NetworkManager.NetworkConfig.NetworkTransport;
            utp.SetHostRelayData(m_UGSFacade.RelayIP, m_UGSFacade.RelayPort, m_UGSFacade.RelayAllocationId, m_UGSFacade.RelayKey, m_UGSFacade.RelayConnectionData);
        }

        public override void SetupClientConnection()
        {
            SetConnectionPayload(GetPlayerId(), m_PlayerName);
            var utp = (UnityTransport)m_ConnectionManager.NetworkManager.NetworkConfig.NetworkTransport;
            utp.SetClientRelayData(m_UGSFacade.RelayIP, m_UGSFacade.RelayPort, m_UGSFacade.RelayAllocationId, m_UGSFacade.RelayKey, m_UGSFacade.RelayConnectionData, m_UGSFacade.RelayHostConnectionData);
        }
    }
}