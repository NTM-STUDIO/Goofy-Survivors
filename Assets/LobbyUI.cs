using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

public class LobbyUI : MonoBehaviour
{
    [Header("References")]
    public Transform playerSlotsParent;
    public TextMeshProUGUI ipDisplayText;
    public Button copyIpButton;
    public Button startGameButton;
    
    [Header("Prefabs")]
    public GameObject playerSlotPrefab; // O prefab do slot UI (SEM NetworkObject!)

    private LobbyManagerP2P manager;
    // Dicionário para guardar os slots visuais locais
    private Dictionary<ulong, LobbyPlayerSlot> localSlots = new Dictionary<ulong, LobbyPlayerSlot>();

    public void Initialize(LobbyManagerP2P manager, bool isHost, string ip)
    {
        this.manager = manager;
        ipDisplayText.text = $"IP: {ip}";
        startGameButton.gameObject.SetActive(isHost);

        copyIpButton.onClick.AddListener(() => {
            GUIUtility.systemCopyBuffer = ip.Replace("IP: ", "");
        });

        startGameButton.onClick.AddListener(() => {
            manager.OnStartGameClicked();
        });
    }

    public void AddPlayerSlot(ulong clientId, string playerName, bool isLocal, int initialSelection, List<GameObject> unitPrefabs)
    {
        if (localSlots.ContainsKey(clientId)) return;

        GameObject slotObj = Instantiate(playerSlotPrefab, playerSlotsParent);
        LobbyPlayerSlot slotScript = slotObj.GetComponent<LobbyPlayerSlot>();
        slotScript.Initialize(manager, clientId, playerName, isLocal, unitPrefabs, initialSelection);
        
        localSlots.Add(clientId, slotScript);
    }

    public void RemovePlayerSlot(ulong clientId)
    {
        if (localSlots.TryGetValue(clientId, out LobbyPlayerSlot slot))
        {
            Destroy(slot.gameObject);
            localSlots.Remove(clientId);
        }
    }

    public void UpdatePlayerSelection(ulong clientId, int newIndex)
    {
        if (localSlots.TryGetValue(clientId, out LobbyPlayerSlot slot))
        {
            slot.UpdateSelection(newIndex);
        }
    }

    public int GetPlayerSelection(ulong clientId)
    {
        if (localSlots.TryGetValue(clientId, out LobbyPlayerSlot slot))
        {
            return slot.GetCurrentIndex();
        }
        return 0;
    }
}