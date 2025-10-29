using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System.Collections.Generic;

public class LobbyManagerP2P : NetworkBehaviour
{
    [Header("UI Prefab")]
    [Tooltip("O prefab VISUAL da UI do lobby (NÃO DEVE ter NetworkObject)")]
    public GameObject lobbyUiPrefab;
    
    [Header("Game Data")]
    public List<GameObject> unitPrefabs;

    private LobbyUI localUI;
    private Dictionary<ulong, int> serverPlayerSelections = new Dictionary<ulong, int>();

    public override void OnNetworkSpawn()
    {
        DontDestroyOnLoad(gameObject);

        Canvas mainCanvas = FindObjectOfType<Canvas>();
        if (mainCanvas == null)
        {
            Debug.LogError("LobbyManagerP2P: Não foi encontrado nenhum Canvas!");
            return;
        }

        GameObject uiObj = Instantiate(lobbyUiPrefab, mainCanvas.transform);
        localUI = uiObj.GetComponent<LobbyUI>();
        
        string ipToShow;
        if (IsHost)
        {
            ipToShow = NetworkManager.Singleton.GetComponent<UnityTransport>().ConnectionData.Address;
        }
        else
        {
            ipToShow = NetworkManager.Singleton.GetComponent<UnityTransport>().ConnectionData.Address;
        }
        
        localUI.Initialize(this, IsHost, ipToShow);

        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            OnClientConnected(NetworkManager.Singleton.LocalClientId);
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;
        
        if (!serverPlayerSelections.ContainsKey(clientId))
        {
            serverPlayerSelections.Add(clientId, 0);
        }
        RefreshLobbyStateForAll();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;

        if (serverPlayerSelections.ContainsKey(clientId))
        {
            serverPlayerSelections.Remove(clientId);
        }
        RefreshLobbyStateForAll();
    }
    
    private void RefreshLobbyStateForAll()
    {
        if (!IsServer) return;

        List<ulong> ids = new List<ulong>();
        List<int> selections = new List<int>();
        foreach(var kvp in serverPlayerSelections)
        {
            ids.Add(kvp.Key);
            selections.Add(kvp.Value);
        }
        
        SyncLobbyStateClientRpc(ids.ToArray(), selections.ToArray());
    }

    [ClientRpc]
    private void SyncLobbyStateClientRpc(ulong[] connectedIds, int[] currentSelections)
    {
        if (localUI == null) return;
        
        localUI.RemoveDisconnectedPlayers(new List<ulong>(connectedIds));

        for (int i = 0; i < connectedIds.Length; i++)
        {
            ulong uid = connectedIds[i];
            int sel = currentSelections[i];
            
            string pName;

            // =======================================================================
            // ## A CORREÇÃO ESTÁ AQUI ##
            // =======================================================================
            if (uid == NetworkManager.Singleton.LocalClientId && IsHost) pName = "Host (You)";
            else if (uid == NetworkManager.Singleton.LocalClientId) pName = "You";
            // Errado: else if (uid == NetworkManager.Singleton.ServerClientId) pName = "Host";
            // Correto:
            else if (uid == NetworkManager.ServerClientId) pName = "Host"; 
            else pName = $"Player {uid}";
            
            bool isLocal = uid == NetworkManager.Singleton.LocalClientId;
            localUI.AddOrUpdatePlayerSlot(uid, pName, isLocal, sel, unitPrefabs);
        }
    }

    public void LocalPlayerChangedSelection(int newIndex)
    {
        SubmitSelectionServerRpc(newIndex);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SubmitSelectionServerRpc(int newIndex, ServerRpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        if (serverPlayerSelections.ContainsKey(senderId))
        {
            serverPlayerSelections[senderId] = newIndex;
            RefreshLobbyStateForAll();
        }
    }

    public void OnStartGameClicked()
    {
        if (!IsHost) return;
        
        Dictionary<ulong, GameObject> finalSelections = new Dictionary<ulong, GameObject>();
        foreach(var kvp in serverPlayerSelections)
        {
            finalSelections.Add(kvp.Key, unitPrefabs[kvp.Value]);
        }

        GameManager.Instance.SetPlayerSelections_P2P(finalSelections);
        StartGameClientRpc();
    }

    [ClientRpc]
    private void StartGameClientRpc()
    {
        if (localUI != null) Destroy(localUI.gameObject);
        GameManager.Instance.StartGame();
        Destroy(gameObject);
    }
    
    public override void OnNetworkDespawn()
    {
        if (localUI != null) Destroy(localUI.gameObject);
        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
        base.OnNetworkDespawn();
    }
}