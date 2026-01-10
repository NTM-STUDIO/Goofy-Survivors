using Unity.Netcode;
using UnityEngine;

/// <summary>
/// Helper centralizado para verificações consistentes de estado de rede.
/// Use este helper em vez de verificar NetworkManager.Singleton diretamente.
/// </summary>
public static class NetworkStateHelper
{
    /// <summary>
    /// Verifica se o NetworkManager está ativo e escutando conexões.
    /// </summary>
    public static bool IsNetworkActive => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
    
    /// <summary>
    /// Verifica se estamos no servidor (host ou dedicated server).
    /// </summary>
    public static bool IsServer => IsNetworkActive && NetworkManager.Singleton.IsServer;
    
    /// <summary>
    /// Verifica se estamos em um cliente (incluindo o host).
    /// </summary>
    public static bool IsClient => IsNetworkActive && NetworkManager.Singleton.IsClient;
    
    /// <summary>
    /// Verifica se estamos no host (servidor + cliente).
    /// </summary>
    public static bool IsHost => IsNetworkActive && NetworkManager.Singleton.IsHost;
    
    /// <summary>
    /// Obtém o ID do cliente local (ou ServerClientId se for servidor dedicado).
    /// </summary>
    public static ulong LocalClientId => IsNetworkActive ? NetworkManager.Singleton.LocalClientId : 0;
    
    /// <summary>
    /// Verifica se um NetworkBehaviour específico está spawned e ativo.
    /// </summary>
    public static bool IsSpawnedAndActive(NetworkBehaviour behaviour)
    {
        return behaviour != null && behaviour.IsSpawned && IsNetworkActive;
    }
    
    /// <summary>
    /// Obtém o ServerTime atual (útil para eventos temporais críticos).
    /// Retorna 0 se não estiver em rede.
    /// </summary>
    public static double GetServerTime()
    {
        return IsNetworkActive ? NetworkManager.Singleton.ServerTime.Time : 0.0;
    }
    
    /// <summary>
    /// Verifica se um objeto de rede existe e está spawned.
    /// </summary>
    public static bool TryGetSpawnedObject(ulong networkObjectId, out NetworkObject networkObject)
    {
        networkObject = null;
        
        if (!IsNetworkActive)
            return false;
        
        return NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out networkObject);
    }
    
    /// <summary>
    /// Verifica se um cliente específico está conectado.
    /// </summary>
    public static bool IsClientConnected(ulong clientId)
    {
        return IsServer && NetworkManager.Singleton.ConnectedClients.ContainsKey(clientId);
    }
    
    /// <summary>
    /// Obtém o número de clientes conectados.
    /// Retorna 1 em singleplayer.
    /// </summary>
    public static int GetConnectedClientCount()
    {
        if (!IsNetworkActive)
            return 1; // Singleplayer
        
        return NetworkManager.Singleton.ConnectedClients.Count;
    }
    
    /// <summary>
    /// Log condicional que só exibe em modo debug ou editor.
    /// </summary>
    public static void DebugLog(string message, Object context = null)
    {
        #if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (context != null)
            Debug.Log($"[Network] {message}", context);
        else
            Debug.Log($"[Network] {message}");
        #endif
    }
    
    /// <summary>
    /// Log de warning para problemas de rede.
    /// </summary>
    public static void WarningLog(string message, Object context = null)
    {
        if (context != null)
            Debug.LogWarning($"[Network Warning] {message}", context);
        else
            Debug.LogWarning($"[Network Warning] {message}");
    }
    
    /// <summary>
    /// Log de erro para problemas críticos de rede.
    /// </summary>
    public static void ErrorLog(string message, Object context = null)
    {
        if (context != null)
            Debug.LogError($"[Network Error] {message}", context);
        else
            Debug.LogError($"[Network Error] {message}");
    }
}
