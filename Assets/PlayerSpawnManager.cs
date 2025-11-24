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
            Debug.Log(">>> [PlayerSpawnManager] Servidor Iniciado e à escuta de conexões.");
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($">>> [PlayerSpawnManager] Cliente {clientId} conectou-se.");
        if (GameManager.Instance.CurrentState == GameManager.GameState.Playing)
        {
            Debug.Log($">>> [PlayerSpawnManager] Jogo já a decorrer. Tentando spawnar Cliente {clientId}...");
            StartCoroutine(SpawnPlayerForClientRoutine(clientId));
        }
    }

    public void SetChosenPrefab(GameObject prefab) => singlePlayerManualSelection = prefab;
    public void SetPlayerSelections(Dictionary<ulong, GameObject> selections) => p2pSelections = selections;

    // CHAMADO PELO UIMANAGER -> GAMEMANAGER -> AQUI
    // CHAMADO PELO UIMANAGER -> GAMEMANAGER -> AQUI
    public void StartSpawningProcess()
    {
        Debug.Log(">>> [PlayerSpawnManager] StartSpawningProcess CHAMADO.");

        if (GameManager.Instance.isP2P)
        {
            Debug.Log(">>> [PlayerSpawnManager] Modo P2P Detetado.");

            // --- CORREÇÃO AQUI ---
            // Antes estava: if (IsHost) -> Isto falha se o objeto Manager não estiver spawnado na rede
            // Agora usamos: NetworkManager.Singleton.IsHost -> Isto verifica a conexão global
            if (NetworkManager.Singleton.IsHost)
            {
                int count = NetworkManager.Singleton.ConnectedClientsList.Count;
                Debug.Log($">>> [PlayerSpawnManager] Eu sou o Host (Verificado Globalmente). Clientes conectados: {count}");

                foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
                {
                    Debug.Log($">>> [PlayerSpawnManager] Iniciando rotina para ClientID: {client.ClientId}");
                    StartCoroutine(SpawnPlayerForClientRoutine(client.ClientId));
                }
            }
            else
            {
                // Se chegou aqui, o NetworkManager diz que não somos Host.
                // Isto só devia acontecer se um Cliente tentasse iniciar o jogo (o que o UI deve bloquear).
                Debug.LogError($">>> [PlayerSpawnManager] ERRO: StartSpawningProcess chamado, mas NetworkManager.IsHost é FALSE.");
            }
        }
        else
        {
            Debug.Log(">>> [PlayerSpawnManager] Modo SinglePlayer Detetado.");
            SpawnSinglePlayer();
        }
    }

    private void SpawnSinglePlayer()
    {
        // ... (código singleplayer igual) ...
        Debug.Log(">>> [PlayerSpawnManager] Spawnando SinglePlayer...");
        GameObject prefab = singlePlayerManualSelection;
        if (prefab == null) { LoadoutSelections.EnsureValidDefaults(); prefab = LoadoutSelections.SelectedCharacterPrefab; }
        if (prefab == null && runtimeNetworkPrefabs.Count > 0) prefab = runtimeNetworkPrefabs[0];

        if (prefab != null)
        {
            var p = Instantiate(prefab, playerSpawnPoint.position, playerSpawnPoint.rotation);
            InitializePlayerSystems(p);
        }
    }

    private IEnumerator SpawnPlayerForClientRoutine(ulong clientId)
    {
        Debug.Log($">>> [DEBUG SPAWN] Iniciando Spawn para {clientId}...");
        yield return new WaitForSeconds(0.1f);

        // CHECK 1: O Cliente existe?
        if (!NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out var client))
        {
            Debug.LogError($">>> [DEBUG SPAWN] ERRO: Cliente {clientId} não encontrado na lista do NetworkManager!");
            yield break;
        }

        // CHECK 2: Já tem Player Object? (Isto acontece se AutoCreatePlayer estiver ligado)
        if (client.PlayerObject != null)
        {
            Debug.LogWarning($">>> [DEBUG SPAWN] AVISO: Cliente {clientId} JÁ TEM um objeto jogador! O Netcode pode ter criado um automático. Abortando spawn manual para não duplicar.");
            // SE ESTA MENSAGEM APARECER, DESLIGA O "AUTO CREATE PLAYER PREFAB" NO NETWORK MANAGER
            yield break;
        }

        // CHECK 3: Seleção de Prefab
        GameObject prefabToUse = null;
        if (p2pSelections.TryGetValue(clientId, out var sel)) prefabToUse = sel;

        if (prefabToUse == null)
        {
            Debug.Log($">>> [DEBUG SPAWN] Sem seleção no Dicionário para {clientId}. Tentando Fallbacks...");
            if (LoadoutSelections.CharacterPrefabsContext != null && LoadoutSelections.CharacterPrefabsContext.Count > 0)
                prefabToUse = LoadoutSelections.CharacterPrefabsContext[0];

            if (prefabToUse == null && runtimeNetworkPrefabs.Count > 0)
            {
                prefabToUse = runtimeNetworkPrefabs[0];
                Debug.Log($">>> [DEBUG SPAWN] Usando Fallback da lista RuntimeNetworkPrefabs: {prefabToUse.name}");
            }
        }

        if (prefabToUse != null)
        {
            Debug.Log($">>> [DEBUG SPAWN] Instanciando {prefabToUse.name} para {clientId}...");
            var instance = Instantiate(prefabToUse, playerSpawnPoint.position, playerSpawnPoint.rotation);
            var netObj = instance.GetComponent<NetworkObject>();

            if (netObj != null)
            {
                Debug.Log($">>> [DEBUG SPAWN] Executando SpawnAsPlayerObject para {clientId}...");
                netObj.SpawnAsPlayerObject(clientId, true);
                Debug.Log($">>> [DEBUG SPAWN] SUCESSO! Jogador spawnado.");

                if (GameManager.Instance.reviveManager) GameManager.Instance.reviveManager.RegisterPlayer(clientId);
                StartCoroutine(GiveStartingWeapon(clientId));
            }
            else
            {
                Debug.LogError($">>> [DEBUG SPAWN] ERRO CRÍTICO: O Prefab {prefabToUse.name} NÃO TEM NetworkObject!");
            }
        }
        else
        {
            Debug.LogError($">>> [DEBUG SPAWN] ERRO CRÍTICO: Nenhum Prefab encontrado! Verifica a lista 'Runtime Network Prefabs' no Inspector.");
        }

        if (clientId == NetworkManager.Singleton.LocalClientId) InitializeClientsClientRpc();
    }

    [ClientRpc]
    private void InitializeClientsClientRpc()
    {
        if (NetworkManager.Singleton.LocalClient != null && NetworkManager.Singleton.LocalClient.PlayerObject != null)
            InitializePlayerSystems(NetworkManager.Singleton.LocalClient.PlayerObject.gameObject);
    }

private IEnumerator GiveStartingWeapon(ulong clientId)
    {
        yield return new WaitForSeconds(0.2f); // Espera a rede estabilizar
        
        // Procura o objeto do jogador
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(clientId, out var netObj))
        {
             // --- TEM DE ESTAR ASSIM (SEM // ANTES DO CODIGO) ---
             var weaponManager = netObj.GetComponent<PlayerWeaponManager>();
             if (weaponManager != null)
             {
                 weaponManager.Server_GiveStartingWeapon();
                 Debug.Log($">>> [PlayerSpawnManager] Arma entregue ao Client {clientId}");
             }
             // ----------------------------------------------------
        }
    }

    private void InitializePlayerSystems(GameObject playerObj)
    {
        FindObjectOfType<PlayerExperience>()?.Initialize(playerObj);
        FindObjectOfType<UpgradeManager>()?.Initialize(playerObj);
        if (!playerObj.GetComponent<ApplyRunesOnSpawn>()) playerObj.AddComponent<ApplyRunesOnSpawn>();
    }
}