using UnityEngine;
using TMPro;
using MyGame.ConnectionSystem.Connection;
using Unity.Netcode;
using System.Collections.Generic;

public class LobbyPlayerListUI : MonoBehaviour
{
    public Transform playerListContainer;
    public GameObject playerEntryPrefab;

    private NetworkList<PlayerData> playerList;

    void Start()
    {
        if (ConnectionManager.Instance != null)
        {
            playerList = ConnectionManager.Instance.PlayerList;
            playerList.OnListChanged += OnPlayerListChanged;
            RefreshPlayerList();
        }
    }

    void OnDestroy()
    {
        if (playerList != null)
            playerList.OnListChanged -= OnPlayerListChanged;
    }

    private void OnPlayerListChanged(NetworkListEvent<PlayerData> change)
    {
        RefreshPlayerList();
    }

    private void RefreshPlayerList()
    {
        foreach (Transform child in playerListContainer)
            Destroy(child.gameObject);

        if (playerList == null) return;
        foreach (var player in playerList)
        {
            GameObject entry = Instantiate(playerEntryPrefab, playerListContainer);
            var text = entry.GetComponentInChildren<TMP_Text>();
            if (text != null)
                text.text = player.PlayerName.ToString();
        }
    }
}
