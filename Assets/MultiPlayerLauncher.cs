using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public class MultiplayerLauncher : MonoBehaviour
{
    [Header("UI References")]
    public TMP_InputField ipInputField;
    public Button hostButton;
    public Button joinButton;

    [Header("Network Prefabs")]
    [Tooltip("The networked Lobby UI prefab to be spawned by the host.")]
    public GameObject lobbyManagerPrefab; // This should be your UI prefab with the LobbyManagerP2P script

    private void OnEnable()
    {
        hostButton.onClick.AddListener(OnHostClicked);
        joinButton.onClick.AddListener(OnJoinClicked);
    }

    private void OnDisable()
    {
        hostButton.onClick.RemoveListener(OnHostClicked);
        joinButton.onClick.RemoveListener(OnJoinClicked);

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnServerStarted -= HandleServerStarted;
        }
    }

    private void OnHostClicked()
    {
        Debug.Log("[Launcher] Host clicked. Waiting for server to start...");
        NetworkManager.Singleton.OnServerStarted += HandleServerStarted;

        if (!NetworkManager.Singleton.StartHost())
        {
            Debug.LogError("Failed to start host!");
            NetworkManager.Singleton.OnServerStarted -= HandleServerStarted;
        }
    }

    private void HandleServerStarted()
    {
        // This is called when the server is ready
        Debug.Log("Server has started successfully! Spawning lobby manager...");
        NetworkManager.Singleton.OnServerStarted -= HandleServerStarted;

        // ## CORRECTION ##
        // Find the main canvas in the scene.
        Canvas mainCanvas = FindObjectOfType<Canvas>();
        if (mainCanvas == null)
        {
            Debug.LogError("FATAL ERROR: Could not find a Canvas in the scene to instantiate the Lobby UI!");
            return;
        }

        if (lobbyManagerPrefab != null)
        {
            // Instantiate the lobby prefab AS A CHILD of the canvas.
            GameObject lobbyManagerInstance = Instantiate(lobbyManagerPrefab, mainCanvas.transform);
            
            // Now that it's instantiated in the right place, spawn it on the network.
            lobbyManagerInstance.GetComponent<NetworkObject>().Spawn();
        }
        else
        {
            Debug.LogError("Lobby Manager Prefab is not assigned in the Inspector!");
        }

        // Hide the launcher UI as its job is done.
        gameObject.SetActive(false);
    }

    private void OnJoinClicked()
    {
        Debug.Log("[Launcher] Join clicked. Attempting to connect as client...");
        string ip = ipInputField.text;
        if (string.IsNullOrEmpty(ip)) ip = "127.0.0.1";

        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport != null)
        {
            transport.ConnectionData.Address = ip;
        }

        if (NetworkManager.Singleton.StartClient())
        {
            gameObject.SetActive(false);
        }
        else
        {
            Debug.LogError("Failed to start client!");
        }
    }
}