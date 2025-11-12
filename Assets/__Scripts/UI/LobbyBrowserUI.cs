// Filename: LobbyBrowserUI.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using System.Threading.Tasks;

public class LobbyBrowserUI : MonoBehaviour
{
    [Header("UI References")]
    public Transform lobbyListContainer;
    public GameObject lobbyEntryPrefab;
    public Button refreshButton;

    private void Start()
    {
        if (refreshButton != null)
        {
            refreshButton.onClick.AddListener(OnRefreshClicked);
        }
        
        // Automatically refresh the list when this UI panel becomes active.
        OnRefreshClicked();
    }

    /// <summary>
    /// Called by the Refresh button's OnClick event.
    /// </summary>
    public async void OnRefreshClicked()
    {
        if (refreshButton != null) refreshButton.interactable = false; // Disable button while refreshing
        await RefreshLobbyListAsync();
        if (refreshButton != null) refreshButton.interactable = true; // Re-enable when done
    }

    /// <summary>
    /// Fetches the list of available lobbies from the Unity Lobby service and populates the UI.
    /// </summary>
    private async Task RefreshLobbyListAsync()
    {
        // Clear the existing list of lobbies before populating it again.
        foreach (Transform child in lobbyListContainer)
        {
            Destroy(child.gameObject);
        }

        try
        {
            // Ensure the player is signed into Unity Services before making any API calls.
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                Debug.Log("[LobbyBrowser] Player not signed in. Signing in anonymously...");
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            }

            // Set up query options to get the 25 most recent lobbies that are not full.
            var queryOptions = new QueryLobbiesOptions
            {
                Count = 25,
                // Filter for lobbies that have at least one available slot.
                Filters = new List<QueryFilter>
                {
                    new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT)
                },
                // Order the results by the newest lobbies first.
                Order = new List<QueryOrder>
                {
                    new QueryOrder(false, QueryOrder.FieldOptions.Created)
                }
            };

            QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync(queryOptions);

            if (response == null || response.Results == null)
            {
                Debug.Log("[LobbyBrowser] No lobbies found.");
                return;
            }

            Debug.Log($"[LobbyBrowser] Found {response.Results.Count} lobbies.");

            // Create a UI entry for each lobby found.
            foreach (var lobby in response.Results)
            {
                GameObject entryGO = Instantiate(lobbyEntryPrefab, lobbyListContainer);
                LobbyEntryUI lobbyEntryUI = entryGO.GetComponent<LobbyEntryUI>();
                if (lobbyEntryUI != null)
                {
                    lobbyEntryUI.SetLobbyInfo(lobby);
                }
            }
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"Lobby query failed with a service exception: {e}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"An unexpected error occurred while refreshing lobbies: {e}");
        }
    }
}