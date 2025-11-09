using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using UnityEngine.UI;

namespace Net
{
    /// <summary>
    /// Centralized session UI actions for lobby/gameplay: ESC menu toggle, leave, restart, and return-to-lobby.
    /// Attach this to a UI GameObject present in gameplay scenes and wire buttons to the public UI_ methods.
    /// </summary>
    public class SessionUIActions : MonoBehaviour
    {
        [Header("UI Panels & Groups")]
        [Tooltip("Panel that opens with ESC. Contains buttons for host/client actions.")]
        public GameObject escPanel;
        [Tooltip("Container shown only for host (Restart, Leave for all)")]
        public GameObject hostButtonsGroup;
        [Tooltip("Container shown only for clients (Leave)")]
        public GameObject clientButtonsGroup;

        [Header("Prefabs & Scene Names")]
        [Tooltip("Networked LobbyManagerP2P prefab to spawn after loading the lobby scene.")]
        public GameObject lobbyManagerPrefab;
        [Tooltip("Lobby scene name (default: P2P)")]
        public string lobbySceneName = "P2P";
        [Tooltip("Gameplay scene name (default: MainScene)")]
        public string gameplaySceneName = "MainScene";

        private bool isHost => NetworkManager.Singleton && NetworkManager.Singleton.IsServer;

        private void Start()
        {
            // Set groups based on host/client role when available; keep safe if running offline
            if (hostButtonsGroup) hostButtonsGroup.SetActive(NetworkManager.Singleton && NetworkManager.Singleton.IsServer);
            if (clientButtonsGroup) clientButtonsGroup.SetActive(NetworkManager.Singleton && NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer);

            if (escPanel) escPanel.SetActive(false);
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (escPanel) escPanel.SetActive(!escPanel.activeSelf);
            }
        }

        // UI hook: Client or Host leaves the current NGO session and goes back to lobby scene offline.
        public void UI_LeaveSession()
        {
            // Shutdown NGO first to disconnect properly
            if (NetworkManager.Singleton)
            {
                NetworkManager.Singleton.Shutdown();
            }
            // Load lobby scene locally so player sees multiplayer UI again
            if (!string.IsNullOrWhiteSpace(lobbySceneName))
            {
                SceneManager.LoadScene(lobbySceneName, LoadSceneMode.Single);
            }
        }

        // UI hook: Host-only. Synchronously return everyone to the lobby scene and spawn lobby manager.
        public void UI_BackToLobby()
        {
            if (!NetworkManager.Singleton || !NetworkManager.Singleton.IsServer)
            {
                Debug.LogWarning("[SessionUI] BackToLobby is host-only.");
                return;
            }

            var nsm = NetworkManager.Singleton.SceneManager;
            if (nsm == null)
            {
                Debug.LogError("[SessionUI] NetworkSceneManager missing.");
                return;
            }

            // Subscribe to scene load complete to spawn the lobby manager once
            nsm.OnLoadEventCompleted += HandleLobbySceneLoaded;
            nsm.LoadScene(lobbySceneName, LoadSceneMode.Single);
        }

        // UI hook: Host-only. Reload gameplay scene for a new round.
        public void UI_RestartHost()
        {
            if (!NetworkManager.Singleton || !NetworkManager.Singleton.IsServer)
            {
                Debug.LogWarning("[SessionUI] Restart is host-only.");
                return;
            }

            var nsm = NetworkManager.Singleton.SceneManager;
            if (nsm == null)
            {
                Debug.LogError("[SessionUI] NetworkSceneManager missing.");
                return;
            }

            nsm.LoadScene(gameplaySceneName, LoadSceneMode.Single);
        }

        private void HandleLobbySceneLoaded(string sceneName, LoadSceneMode mode, System.Collections.Generic.List<ulong> clientsCompleted, System.Collections.Generic.List<ulong> clientsTimedOut)
        {
            // Ensure we only handle the lobby scene
            if (!string.Equals(sceneName, lobbySceneName)) return;

            // Unsubscribe immediately to avoid multiple spawns if reloading
            var nsm = NetworkManager.Singleton?.SceneManager;
            if (nsm != null) nsm.OnLoadEventCompleted -= HandleLobbySceneLoaded;

            if (!NetworkManager.Singleton || !NetworkManager.Singleton.IsServer)
            {
                return;
            }

            if (lobbyManagerPrefab == null)
            {
                Debug.LogWarning("[SessionUI] LobbyManager prefab not assigned; lobby UI won’t appear automatically.");
                return;
            }

            var go = Instantiate(lobbyManagerPrefab);
            var no = go.GetComponent<NetworkObject>();
            if (no == null)
            {
                Debug.LogError("[SessionUI] LobbyManager prefab is missing NetworkObject.");
                Destroy(go);
                return;
            }
            no.Spawn();
        }
    }
}
