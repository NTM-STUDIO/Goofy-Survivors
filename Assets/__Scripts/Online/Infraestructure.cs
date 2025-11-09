// Filename: Infrastructure.cs
// Location: _Scripts/ConnectionSystem/Data/
using System;
namespace MyGame.ConnectionSystem.Data
{
    [Serializable]
    public class ConnectionPayload
    {
        public string playerId;
        public string playerName;
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
}