// Filename: SessionPlayerData.cs
// Location: _Scripts/ConnectionSystem/Data/

using Unity.Netcode;
using Unity.Collections; // <-- ADD THIS LINE

namespace MyGame.ConnectionSystem.Data
{
    public struct SessionPlayerData : INetworkSerializable
    {
        public ulong ClientID;
        public FixedString64Bytes PlayerName; // This will now be found

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientID);
            serializer.SerializeValue(ref PlayerName);
        }
    }
}