// Filename: MultiplayerHelpers.cs
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using Unity.Services.Multiplayer;

// Use a namespace for YOUR game to keep things organized.
namespace MyGame.Connection
{
    /// <summary>
    /// This is the most important file for you to customize.
    /// It defines the data that will be synchronized for each player in a session.
    /// </summary>
    public struct SessionPlayerData : INetworkSerializable
    {
        public ulong ClientID;
        public FixedString64Bytes PlayerName;
        public bool IsConnected;

        // --- TODO: ADD YOUR GAME-SPECIFIC DATA HERE! ---
        // Examples:
        // public int Score;
        // public int TeamID;
        // public FixedString32Bytes SelectedCharacter;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientID);
            serializer.SerializeValue(ref PlayerName);
            serializer.SerializeValue(ref IsConnected);
            
            // Remember to serialize your custom data here too!
            // serializer.SerializeValue(ref Score);
            // serializer.SerializeValue(ref TeamID);
        }
    }

    [Serializable]
    public class ConnectionPayload
    {
        public string playerId;
        public string playerName;
        public bool isDebug;
    }

    public class ClientPrefs
    {
        public static string GetGuid()
        {
            if (!UnityEngine.PlayerPrefs.HasKey("client_guid"))
            {
                UnityEngine.PlayerPrefs.SetString("client_guid", Guid.NewGuid().ToString());
            }
            return UnityEngine.PlayerPrefs.GetString("client_guid");
        }
    }
    
    public readonly struct UnityServiceErrorMessage
    {
        public readonly string Title;
        public readonly string Message;
        public UnityServiceErrorMessage(string title, string message) { Title = title; Message = message; }
    }

    public readonly struct SessionListFetchedMessage
    {
        public readonly IReadOnlyList<ISession> Sessions;
        public SessionListFetchedMessage(IReadOnlyList<ISession> sessions) { Sessions = sessions; }
    }
    
    public class RateLimitCooldown
    {
        public bool CanCall => m_LastCallTime + m_Cooldown < UnityEngine.Time.time;
        private readonly float m_Cooldown;
        private float m_LastCallTime;

        public RateLimitCooldown(float cooldown)
        {
            m_Cooldown = cooldown;
            m_LastCallTime = -cooldown;
        }

        public void PutOnCooldown()
        {
            m_LastCallTime = UnityEngine.Time.time;
        }
    }
}