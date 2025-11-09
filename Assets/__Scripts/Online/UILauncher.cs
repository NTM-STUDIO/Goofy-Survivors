// Filename: UILauncher.cs
using UnityEngine;
using TMPro;
using MyGame.ConnectionSystem.Connection;

public class UILauncher : MonoBehaviour
{
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private TMP_Text lobbyCodeText;

    public void OnHostClicked()
    {
        Debug.Log("[UILauncher] Host button clicked.");
        if (ConnectionManager.Instance != null)
        {
            ConnectionManager.Instance.StartHost("HostPlayer");
            if (lobbyCodeText != null)
            {
                lobbyCodeText.gameObject.SetActive(true);
                lobbyCodeText.text = "A gerar código do Lobby...";
            }
        }
        else
        {
            Debug.LogError("[UILauncher] ERRO FATAL: ConnectionManager.Instance é nulo!");
        }
    }

    public void OnClientClicked()
    {
        Debug.Log("[UILauncher] Client button clicked.");
        if (string.IsNullOrEmpty(joinCodeInput.text))
        {
            Debug.LogError("O Join Code não pode estar vazio!");
            return;
        }
        if (ConnectionManager.Instance != null)
        {
            ConnectionManager.Instance.StartClient("ClientPlayer", joinCodeInput.text);
        }
        else
        {
            Debug.LogError("[UILauncher] ERRO FATAL: ConnectionManager.Instance é nulo!");
        }
    }
}