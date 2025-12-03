using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Sistema de rastreamento de dano por habilidade que mantém estatísticas separadas para cada jogador.
/// Em multiplayer, cada jogador (host ou client) vê apenas suas próprias estatísticas de dano.
/// Em single-player, funciona normalmente com um único jogador.
/// </summary>
public static class AbilityDamageTracker
{
    // Dicionário que mapeia NetworkObjectId do jogador para suas estatísticas de dano por habilidade
    private static readonly Dictionary<ulong, Dictionary<string, float>> playerDamageTotals = new Dictionary<ulong, Dictionary<string, float>>();

    public static void Reset()
    {
        playerDamageTotals.Clear();
    }

    /// <summary>
    /// Reseta apenas as estatísticas de um jogador específico
    /// </summary>
    public static void ResetPlayer(ulong playerNetId)
    {
        if (playerDamageTotals.ContainsKey(playerNetId))
        {
            playerDamageTotals[playerNetId].Clear();
        }
    }

    /// <summary>
    /// Registra dano causado por uma habilidade de um jogador específico
    /// </summary>
    public static void RecordDamage(string abilityKey, float amount, GameObject source, PlayerStats attacker)
    {
        if (amount <= 0f || source == null || attacker == null)
        {
            Debug.LogWarning($"[AbilityDamageTracker] RecordDamage chamado com parâmetros inválidos: amount={amount}, source={(source != null ? source.name : "null")}, attacker={(attacker != null ? "valid" : "null")}");
            return;
        }

        if (!HasAbilityTag(source))
        {
            Debug.LogWarning($"[AbilityDamageTracker] Source '{source.name}' não tem tag 'Ability'");
            return;
        }

        string key = string.IsNullOrWhiteSpace(abilityKey) ? source.name : abilityKey;
        RecordDamageDirectly(key, amount, attacker);
    }

    /// <summary>
    /// Registra dano diretamente sem verificar a tag (usado por ClientRpcs)
    /// </summary>
    public static void RecordDamageDirectly(string abilityKey, float amount, PlayerStats attacker)
    {
        if (amount <= 0f || attacker == null || string.IsNullOrWhiteSpace(abilityKey))
        {
            Debug.LogWarning($"[AbilityDamageTracker] RecordDamageDirectly inválido: key='{abilityKey}', amount={amount}");
            return;
        }

        // Obtém o NetworkObjectId do jogador atacante
        ulong playerNetId = GetPlayerNetworkId(attacker);
        Debug.Log($"[AbilityDamageTracker] Recording {amount:F0} damage from '{abilityKey}' for player {playerNetId}");

        // Cria o dicionário de dano do jogador se não existir
        if (!playerDamageTotals.ContainsKey(playerNetId))
        {
            playerDamageTotals[playerNetId] = new Dictionary<string, float>();
        }

        var playerDamage = playerDamageTotals[playerNetId];

        if (playerDamage.TryGetValue(abilityKey, out float current))
        {
            playerDamage[abilityKey] = current + amount;
        }
        else
        {
            playerDamage[abilityKey] = amount;
        }
    }

    /// <summary>
    /// Obtém as estatísticas de dano de um jogador específico
    /// </summary>
    public static IReadOnlyDictionary<string, float> GetPlayerTotalsSnapshot(PlayerStats player)
    {
        if (player == null)
        {
            Debug.LogWarning("[AbilityDamageTracker] GetPlayerTotalsSnapshot chamado com player null!");
            return new Dictionary<string, float>();
        }

        ulong playerNetId = GetPlayerNetworkId(player);
        Debug.Log($"[AbilityDamageTracker] GetPlayerTotalsSnapshot para player {playerNetId}. Total de jogadores rastreados: {playerDamageTotals.Count}");

        if (playerDamageTotals.TryGetValue(playerNetId, out var playerDamage))
        {
            Debug.Log($"[AbilityDamageTracker] Encontradas {playerDamage.Count} habilidades para o jogador {playerNetId}");
            var snapshot = new Dictionary<string, float>();
            foreach (var kvp in playerDamage)
            {
                snapshot[kvp.Key] = Mathf.Round(kvp.Value);
                Debug.Log($"  - {kvp.Key}: {kvp.Value:F0} dano");
            }
            return snapshot;
        }

        Debug.LogWarning($"[AbilityDamageTracker] Nenhum dado encontrado para o jogador {playerNetId}");
        return new Dictionary<string, float>();
    }

    /// <summary>
    /// Retorna snapshot de TODOS os jogadores (para debug)
    /// </summary>
    public static IReadOnlyDictionary<string, float> GetTotalsSnapshot()
    {
        // Para manter compatibilidade com código antigo, retorna os dados do jogador local
        var localPlayer = GetLocalPlayer();
        if (localPlayer != null)
        {
            return GetPlayerTotalsSnapshot(localPlayer);
        }
        return new Dictionary<string, float>();
    }

    public static void LogTotals()
    {
        if (playerDamageTotals.Count == 0)
        {
            Debug.Log("[AbilityDamageTracker] No ability damage recorded for any player.");
            return;
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("===== Ability Damage Totals By Player =====");
        
        foreach (var playerEntry in playerDamageTotals)
        {
            builder.AppendLine($"\n--- Player {playerEntry.Key} ---");
            foreach (var abilityEntry in playerEntry.Value.OrderByDescending(kvp => kvp.Value))
            {
                builder.AppendLine($"  {abilityEntry.Key}: {Mathf.RoundToInt(abilityEntry.Value)}");
            }
        }

        Debug.Log(builder.ToString());
    }

    /// <summary>
    /// Obtém o NetworkObjectId do jogador, funciona tanto em single-player quanto multiplayer
    /// </summary>
    private static ulong GetPlayerNetworkId(PlayerStats player)
    {
        var networkObject = player.GetComponent<NetworkObject>();
        if (networkObject != null && networkObject.IsSpawned)
        {
            ulong netId = networkObject.NetworkObjectId;
            Debug.Log($"[AbilityDamageTracker] Player NetworkObjectId: {netId} (Multiplayer)");
            return netId;
        }
        // Em single-player ou se não estiver em rede, usa um ID único baseado no hash do objeto
        // Usando GetHashCode() ao invés de InstanceID para garantir um valor positivo consistente
        ulong uniqueId = (ulong)Mathf.Abs(player.GetHashCode());
        Debug.Log($"[AbilityDamageTracker] Player HashCode ID: {uniqueId} (Single-player)");
        return uniqueId;
    }

    /// <summary>
    /// Encontra o PlayerStats do jogador local
    /// </summary>
    private static PlayerStats GetLocalPlayer()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            // Multiplayer: procura pelo jogador que é owner local
            var allPlayers = Object.FindObjectsByType<PlayerStats>(FindObjectsSortMode.None);
            foreach (var player in allPlayers)
            {
                var netObj = player.GetComponent<NetworkObject>();
                if (netObj != null && netObj.IsOwner)
                {
                    return player;
                }
            }
        }
        else
        {
            // Single-player: retorna o primeiro PlayerStats encontrado
            return Object.FindFirstObjectByType<PlayerStats>();
        }
        return null;
    }

    private static bool HasAbilityTag(GameObject source)
    {
        // Aceita objetos com tag "Ability" ou "Untagged" (armas/habilidades geralmente são untagged)
        if (source.CompareTag("Ability"))
        {
            return true;
        }

        // Se o objeto não tem tag "Enemy", "Player", ou "Reaper", provavelmente é uma habilidade
        if (!source.CompareTag("Enemy") && !source.CompareTag("Player") && !source.CompareTag("Reaper"))
        {
            // Aceita como habilidade se não for um inimigo ou jogador
            return true;
        }

        // Verifica nos pais se tem a tag "Ability"
        Transform current = source.transform.parent;
        while (current != null)
        {
            if (current.CompareTag("Ability"))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }
}
