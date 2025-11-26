using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class PlayerSpawnManager : NetworkBehaviour
{
    [Header("Spawn Settings")]
    [SerializeField] private Transform playerSpawnPoint;
    [SerializeField] private List<GameObject> runtimeNetworkPrefabs;

    private GameObject singlePlayerManualSelection;
    private Dictionary<ulong, GameObject> p2pSelections = new Dictionary<ulong, GameObject>();

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            Debug.Log(">>> [PlayerSpawnManager] Servidor Iniciado. À espera de jogadores.");
            
            // Regista prefabs para evitar erros de hash
            RegisterRuntimePrefabs();
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }

    private void RegisterRuntimePrefabs()
    {
        if (runtimeNetworkPrefabs == null) return;
        foreach (var prefab in runtimeNetworkPrefabs)
        {
            if (prefab != null) try { RuntimeNetworkPrefabRegistry.TryRegister(prefab); } catch { }
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        // Se alguém entrar a meio do jogo, spawna logo
        if (GameManager.Instance.CurrentState == GameManager.GameState.Playing)
        {
            StartCoroutine(SpawnPlayerForClientRoutine(clientId));
        }
    }

    public void SetChosenPrefab(GameObject prefab) => singlePlayerManualSelection = prefab;
    public void SetPlayerSelections(Dictionary<ulong, GameObject> selections) => p2pSelections = selections;

    public void StartSpawningProcess()
    {
        if (GameManager.Instance.isP2P)
        {
            // Verifica se somos Host através do NetworkManager
            if (NetworkManager.Singleton.IsHost)
            {
                foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
                {
                    StartCoroutine(SpawnPlayerForClientRoutine(client.ClientId));
                }
            }
        }
        else
        {
            SpawnSinglePlayer();
        }
    }

    private void SpawnSinglePlayer()
    {
        GameObject prefab = singlePlayerManualSelection;
        
        if (prefab == null) 
        { 
            LoadoutSelections.EnsureValidDefaults(); 
            prefab = LoadoutSelections.SelectedCharacterPrefab; 
        }
        
        if (prefab == null && runtimeNetworkPrefabs.Count > 0) 
            prefab = runtimeNetworkPrefabs[0];

        if (prefab != null)
        {
            var p = Instantiate(prefab, playerSpawnPoint.position, playerSpawnPoint.rotation);
            InitializePlayerSystems(p);
        }
    }

    private IEnumerator SpawnPlayerForClientRoutine(ulong clientId)
    {
        yield return new WaitForSeconds(0.1f);

        // Verifica se já existe jogador
        if (NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
        {
            if (client.PlayerObject != null) yield break; // Já tem boneco
        }

        // Escolhe Prefab
        GameObject prefabToUse = null;
        if (p2pSelections.TryGetValue(clientId, out var sel)) prefabToUse = sel;

        // Fallbacks
        if (prefabToUse == null && LoadoutSelections.CharacterPrefabsContext != null && LoadoutSelections.CharacterPrefabsContext.Count > 0)
            prefabToUse = LoadoutSelections.CharacterPrefabsContext[0];

        if (prefabToUse == null && runtimeNetworkPrefabs.Count > 0)
            prefabToUse = runtimeNetworkPrefabs[0];

        if (prefabToUse != null)
        {
            // Instancia e Spawna na Rede
            var instance = Instantiate(prefabToUse, playerSpawnPoint.position, playerSpawnPoint.rotation);
            var netObj = instance.GetComponent<NetworkObject>();

            if (netObj != null)
            {
                netObj.SpawnAsPlayerObject(clientId, true);

                // Regista no Revive System
                if (GameManager.Instance.reviveManager) 
                    GameManager.Instance.reviveManager.RegisterPlayer(clientId);
                
                // --- AQUI ESTÁ A CORREÇÃO DA ARMA ---
                StartCoroutine(GiveStartingWeapon(clientId));
            }
        }

        // Inicializa UI local no Host se for ele a spawnar
        if (clientId == NetworkManager.Singleton.LocalClientId) InitializeClientsClientRpc();
    }

    [ClientRpc]
    private void InitializeClientsClientRpc()
    {
        if (NetworkManager.Singleton.LocalClient != null && NetworkManager.Singleton.LocalClient.PlayerObject != null)
        {
            InitializePlayerSystems(NetworkManager.Singleton.LocalClient.PlayerObject.gameObject);
        }
    }

    // --- ESTE É O MÉTODO QUE DÁ A ARMA NO MULTIPLAYER ---
    private IEnumerator GiveStartingWeapon(ulong clientId)
    {
        // Espera 0.5s para garantir que o objeto foi propagado na rede e inicializado
        yield return new WaitForSeconds(0.5f);
        
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(clientId, out var netObj))
        {
             // Se não encontrou pelo ID do cliente, tenta encontrar pelo PlayerObject do cliente
             // (O TryGetValue acima procura por NetworkObjectId, mas o clientId é diferente)
        }

        // Correção: Vamos buscar o objeto do jogador corretamente
        NetworkObject playerObject = NetworkManager.Singleton.SpawnManager.GetPlayerNetworkObject(clientId);

        if (playerObject != null)
        {
             var weaponManager = playerObject.GetComponent<PlayerWeaponManager>();
             
             if (weaponManager != null)
             {
                 Debug.Log($">>> [PlayerSpawnManager] A dar arma inicial ao Cliente {clientId}...");
                 weaponManager.Server_GiveStartingWeapon();
             }
             else
             {
                 Debug.LogError($">>> [PlayerSpawnManager] O Cliente {clientId} spawnou mas o Prefab não tem 'PlayerWeaponManager'!");
             }
        }
        else
        {
            Debug.LogError($">>> [PlayerSpawnManager] Falha ao encontrar objeto do jogador para dar arma (Client {clientId})");
        }
    }

    private void InitializePlayerSystems(GameObject playerObj)
    {
        FindObjectOfType<PlayerExperience>()?.Initialize(); 
        FindObjectOfType<UpgradeManager>()?.Initialize(playerObj);
        if (!playerObj.GetComponent<ApplyRunesOnSpawn>()) 
            playerObj.AddComponent<ApplyRunesOnSpawn>();
    }
}