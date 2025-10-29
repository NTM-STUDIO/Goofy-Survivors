using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;

public class MultiplayerLauncher : MonoBehaviour
{
    [Header("UI References")]
    public TMP_InputField ipInputField;
    public Button hostButton;
    public Button joinButton;

    [Header("Network Prefabs")]
    public GameObject lobbyManagerPrefab;

    private void OnEnable()
    {
        hostButton.onClick.AddListener(OnHostClicked);
        joinButton.onClick.AddListener(OnJoinClicked);

        // Auto-fill the input field with best local IP (Radmin if possible)
        ipInputField.text = GetLocalIPAddress();
    }

    private void OnDisable()
    {
        hostButton.onClick.RemoveListener(OnHostClicked);
        joinButton.onClick.RemoveListener(OnJoinClicked);

        if (NetworkManager.Singleton != null)
            NetworkManager.Singleton.OnServerStarted -= HandleServerStarted;
    }

    private void OnHostClicked()
    {
        string hostIp = GetLocalIPAddress(); // Radmin IP
        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

        if (transport != null)
        {
            // Reset possible leftover config
            transport.SetConnectionData("0.0.0.0", 7777); // listen on all interfaces
            // But store the Radmin IP so clients know which to connect to
            Debug.Log($"[Transport] Host will listen on all interfaces, advertise {hostIp}:7777");
        }

        Debug.Log($"[Launcher] Starting Host on IP: {hostIp} ...");
        NetworkManager.Singleton.OnServerStarted += HandleServerStarted;

        if (!NetworkManager.Singleton.StartHost())
        {
            Debug.LogError("❌ Failed to start Host!");
            NetworkManager.Singleton.OnServerStarted -= HandleServerStarted;
        }
    }

    private void HandleServerStarted()
    {
        Debug.Log("✅ Server started successfully! Spawning lobby manager...");
        NetworkManager.Singleton.OnServerStarted -= HandleServerStarted;

        if (lobbyManagerPrefab != null)
        {
            var lobbyManagerInstance = Instantiate(lobbyManagerPrefab);
            lobbyManagerInstance.GetComponent<NetworkObject>().Spawn();
        }
        else
        {
            Debug.LogError("❌ LobbyManager prefab not assigned in Inspector!");
        }

        gameObject.SetActive(false);
    }

    private void OnJoinClicked()
    {
        string joinIp = ipInputField.text.Trim();
        if (string.IsNullOrEmpty(joinIp))
        {
            Debug.LogError("Please enter the Host IP to connect!");
            return;
        }

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport != null)
        {
            // Correct Unity 6 API
            transport.SetConnectionData(joinIp, 7777);
            Debug.Log($"[Transport] Client will connect to {joinIp}:7777");
        }

        Debug.Log($"[Launcher] Attempting to join host at {joinIp}...");
        if (NetworkManager.Singleton.StartClient())
        {
            gameObject.SetActive(false);
        }
        else
        {
            Debug.LogError("❌ Failed to start Client!");
        }
    }

    /// <summary>
    /// Detects the best local IPv4 (prefers Radmin VPN).
    /// </summary>
    private string GetLocalIPAddress()
    {
        try
        {
            // Priority 1: Radmin VPN
            foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (ni.OperationalStatus == OperationalStatus.Up &&
                    ni.Description.Contains("Radmin", System.StringComparison.OrdinalIgnoreCase))
                {
                    foreach (var ip in ni.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            Debug.Log($"[Network] Using Radmin VPN IP: {ip.Address}");
                            return ip.Address.ToString();
                        }
                    }
                }
            }

            // Priority 2: first valid LAN IPv4
            foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                {
                    Debug.Log($"[Network] Using LAN IP: {ip}");
                    return ip.ToString();
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[Network] Error detecting IP: {ex.Message}");
        }

        Debug.LogWarning("[Network] No Radmin or LAN IP found, using 127.0.0.1");
        return "127.0.0.1";
    }
}
