using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

public class PlayerManager : NetworkBehaviour
{
    public static PlayerManager Instance { get; private set; }

    // Lista pública que os inimigos consultam
    public List<Transform> ActivePlayers { get; private set; } = new List<Transform>();

    private float refreshTimer;

    private void Awake()
    {
        if (Instance != null && Instance != this) Destroy(gameObject);
        else Instance = this;
    }

    private void Update()
    {
        // Se for Multiplayer e não for Servidor, não precisa processar isto
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !IsServer) return;

        refreshTimer -= Time.deltaTime;
        if (refreshTimer <= 0f)
        {
            RefreshPlayerList();
            refreshTimer = 0.5f; // Atualiza a lista a cada 0.5s
        }
    }

    private void RefreshPlayerList()
    {
        ActivePlayers.Clear();

        // 1. Tenta encontrar via Netcode (Multiplayer)
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client.PlayerObject != null)
                {
                    // Verifica se o jogador está vivo (opcional, depende do teu script PlayerStats)
                    // var stats = client.PlayerObject.GetComponent<PlayerStats>();
                    // if (stats != null && stats.IsDowned) continue;

                    ActivePlayers.Add(client.PlayerObject.transform);
                }
            }
        }
        // 2. Fallback para Singleplayer ou Testes sem Rede (Procura por Tag)
        else
        {
            GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
            foreach (var p in players)
            {
                ActivePlayers.Add(p.transform);
            }
        }
    }
}