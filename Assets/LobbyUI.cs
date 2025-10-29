using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Linq; // Necessário para a função .Contains()

public class LobbyUI : MonoBehaviour
{
    [Header("Referências da UI (Arrastar no Prefab)")]
    public Transform playerSlotsParent;
    public TextMeshProUGUI ipDisplayText;
    public Button copyIpButton;
    public Button startGameButton;
    
    [Header("Prefabs")]
    public GameObject playerSlotPrefab; // O prefab do slot individual de um jogador

    private LobbyManagerP2P manager;
    private Dictionary<ulong, LobbyPlayerSlot> localSlots = new Dictionary<ulong, LobbyPlayerSlot>();

    /// <summary>
    /// Método de inicialização chamado pelo LobbyManagerP2P.
    /// </summary>
    public void Initialize(LobbyManagerP2P manager, bool isHost, string ip)
    {
        this.manager = manager;
        ipDisplayText.text = $"IP: {ip}";
        startGameButton.gameObject.SetActive(isHost);

        // Configura os listeners dos botões para comunicarem de volta com o "cérebro" (o manager)
        copyIpButton.onClick.AddListener(() => {
            GUIUtility.systemCopyBuffer = ip;
            Debug.Log($"IP Copiado: {ip}");
        });

        startGameButton.onClick.AddListener(() => {
            manager.OnStartGameClicked();
        });
    }

    /// <summary>
    /// Adiciona um novo slot de jogador se ele não existir, ou atualiza um existente.
    /// Chamado pelo LobbyManagerP2P.
    /// </summary>
    public void AddOrUpdatePlayerSlot(ulong clientId, string playerName, bool isLocal, int selection, List<GameObject> unitPrefabs)
    {
        // Tenta encontrar um slot já existente para este jogador
        if (localSlots.TryGetValue(clientId, out LobbyPlayerSlot existingSlot))
        {
            // O slot já existe, então apenas atualizamos a sua seleção de unidade
            existingSlot.UpdateSelection(selection);
        }
        else
        {
            // O slot não existe, então criamos um novo
            GameObject slotObj = Instantiate(playerSlotPrefab, playerSlotsParent);
            LobbyPlayerSlot newSlotScript = slotObj.GetComponent<LobbyPlayerSlot>();
            
            newSlotScript.Initialize(manager, clientId, playerName, isLocal, unitPrefabs, selection);
            
            // Adiciona o novo slot ao nosso dicionário para referência futura
            localSlots.Add(clientId, newSlotScript);
        }
    }

    /// <summary>
    /// Remove da UI os slots de jogadores que se desconectaram.
    /// Chamado pelo LobbyManagerP2P.
    /// </summary>
    public void RemoveDisconnectedPlayers(List<ulong> connectedIds)
    {
        // Criamos uma lista de jogadores a remover para evitar modificar o dicionário enquanto o percorremos
        List<ulong> playersToRemove = new List<ulong>();
        
        foreach (ulong existingClientId in localSlots.Keys)
        {
            // Se um jogador que temos na nossa UI não está na lista de jogadores conectados do servidor...
            if (!connectedIds.Contains(existingClientId))
            {
                // ... adicionamo-lo à lista de remoção.
                playersToRemove.Add(existingClientId);
            }
        }

        // Agora, percorremos a lista de remoção e destruímos os objetos
        foreach (ulong clientIdToRemove in playersToRemove)
        {
            if (localSlots.TryGetValue(clientIdToRemove, out LobbyPlayerSlot slotToRemove))
            {
                Destroy(slotToRemove.gameObject);
                localSlots.Remove(clientIdToRemove);
            }
        }
    }
}