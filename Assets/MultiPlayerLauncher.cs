using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
// ADICIONAR ESTAS DIRETIVAS PARA ACEDER ÀS INFORMAÇÕES DE REDE
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
        
        // Tenta preencher automaticamente o campo de IP com a melhor opção encontrada
        ipInputField.text = GetLocalIPAddress();
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
        string hostIp = ipInputField.text;
        if (string.IsNullOrEmpty(hostIp))
        {
            // Se o campo estiver vazio, faz uma última tentativa de encontrar o IP
            hostIp = GetLocalIPAddress();
        }

        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport != null)
        {
            transport.ConnectionData.Address = hostIp;
            transport.ConnectionData.Port = 7777;
        }

        Debug.Log($"[Launcher] A tentar iniciar como Host no IP: {hostIp}...");
        NetworkManager.Singleton.OnServerStarted += HandleServerStarted;

        if (!NetworkManager.Singleton.StartHost())
        {
            Debug.LogError("Falha ao iniciar o Host!");
            NetworkManager.Singleton.OnServerStarted -= HandleServerStarted;
        }
    }

    private void HandleServerStarted()
    {
        Debug.Log("Servidor iniciado com sucesso! A spawnar o lobby manager...");
        NetworkManager.Singleton.OnServerStarted -= HandleServerStarted;
        
        if (lobbyManagerPrefab != null)
        {
            GameObject lobbyManagerInstance = Instantiate(lobbyManagerPrefab);
            lobbyManagerInstance.GetComponent<NetworkObject>().Spawn();
        }
        else
        {
            Debug.LogError("Prefab do Lobby Manager (lógica) não está atribuído no Inspector!");
        }
        
        gameObject.SetActive(false);
    }

    private void OnJoinClicked()
    {
        string joinIp = ipInputField.text;
        if (string.IsNullOrEmpty(joinIp))
        {
            Debug.LogError("Por favor, insira o IP do Host para se conectar!");
            return;
        }

        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        if (transport != null)
        {
            transport.ConnectionData.Address = joinIp;
            transport.ConnectionData.Port = 7777;
        }
        
        Debug.Log($"[Launcher] A tentar conectar-se como Cliente ao IP: {joinIp}...");

        if (NetworkManager.Singleton.StartClient())
        {
            gameObject.SetActive(false);
        }
        else
        {
            Debug.LogError("Falha ao iniciar o Cliente!");
        }
    }

    /// <summary>
    /// Função inteligente para encontrar o melhor endereço IP local.
    /// Dá prioridade a IPs de VPNs conhecidas (como Radmin) e depois a IPs de LAN.
    /// </summary>
    private string GetLocalIPAddress()
    {
        // Estratégia 1: Procurar especificamente por uma interface de rede da Radmin VPN
        try
        {
            foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
            {
                // Procuramos por "Radmin" na descrição da placa de rede
                if (nic.Description.Contains("Radmin") && nic.OperationalStatus == OperationalStatus.Up)
                {
                    foreach (UnicastIPAddressInformation ip in nic.GetIPProperties().UnicastAddresses)
                    {
                        if (ip.Address.AddressFamily == AddressFamily.InterNetwork) // Procurar por IPv4
                        {
                            Debug.Log($"Encontrado IP da Radmin VPN: {ip.Address.ToString()}");
                            return ip.Address.ToString();
                        }
                    }
                }
            }
        }
        catch { /* Ignorar erros, vamos para a estratégia 2 */ }

        // Estratégia 2 (Fallback): Procurar pelo primeiro IP de rede local (LAN) válido
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                // Queremos um endereço IPv4 que não seja o de loopback (127.0.0.1)
                if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                {
                    Debug.Log($"Encontrado IP de LAN: {ip.ToString()}");
                    return ip.ToString();
                }
            }
        }
        catch { /* Ignorar erros, vamos para a estratégia 3 */ }

        // Estratégia 3 (Fallback Final): Usar 127.0.0.1 para testes locais
        Debug.LogWarning("Nenhum IP de VPN ou LAN encontrado. A usar 127.0.0.1 (localhost).");
        return "127.0.0.1";
    }
}