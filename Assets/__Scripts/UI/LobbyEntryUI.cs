// Filename: LobbyEntryUI.cs
using UnityEngine;
using UnityEngine.UI;
using Unity.Services.Lobbies.Models;
using MyGame.ConnectionSystem.Connection;

public class LobbyEntryUI : MonoBehaviour
{
    [Header("UI References")]
    public TMPro.TextMeshProUGUI lobbyNameText;
    public TMPro.TextMeshProUGUI playerCountText;
    public Button joinButton;

    private Lobby lobbyData;

    public void SetLobbyInfo(Lobby lobby)
    {
        lobbyData = lobby;

        if (lobbyNameText != null)
            lobbyNameText.text = lobby.Name;
            
        if (playerCountText != null)
            playerCountText.text = $"{lobby.Players.Count}/{lobby.MaxPlayers}";
            
        if (joinButton != null)
        {
            joinButton.onClick.RemoveAllListeners();
            joinButton.onClick.AddListener(OnJoinClicked);
        }
    }

    private void OnJoinClicked()
    {
        Debug.Log($"[LobbyEntryUI] Join button clicked for lobby: {lobbyData.Name} (ID: {lobbyData.Id})");

        // --- THIS IS THE CORRECTION ---
        // We now check for the Lobby's ID, which is guaranteed to exist from a query.
        if (lobbyData != null && !string.IsNullOrEmpty(lobbyData.Id))
        {
            if (ConnectionManager.Instance != null)
            {
                // We pass the Lobby ID to the ConnectionManager.
                ConnectionManager.Instance.StartClient("ClientPlayer", lobbyData.Id);
                
                joinButton.interactable = false;
            }
            else
            {
                Debug.LogError("[LobbyEntryUI] ConnectionManager.Instance is null! Cannot join lobby.");
            }
        }
        else
        {
            Debug.LogError("[LobbyEntryUI] Lobby data is invalid or has no Lobby ID.");
        }
    }
}