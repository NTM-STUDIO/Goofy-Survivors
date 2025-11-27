using UnityEngine;
using Unity.Netcode;
using System;
using System.Collections.Generic;
using System.Linq;

// Lives on the player NetworkObject. Client sends chosen loadout to server after spawn.
public class LoadoutSync : NetworkBehaviour
{
    // Server-side store (by OwnerClientId)
    private static readonly Dictionary<ulong, SyncedSelection> ServerSelections = new Dictionary<ulong, SyncedSelection>();

    public struct SyncedSelection
    {
        public int weaponId;           // -1 if none
        public List<string> runeIds;   // may be empty
        public int characterIndex;     // -1 if none/host default
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        // The owning client should send their selection up. Host (IsServer) also
        // needs to apply its own selection, so do this for any owner client.
        if (IsOwner && IsClient)
        {
            // Aguardar 1 frame para garantir que DefaultLoadoutInitializer.Awake() já rodou
            StartCoroutine(SendLoadoutAfterDelay());
        }
    }

    private System.Collections.IEnumerator SendLoadoutAfterDelay()
    {
        // Aguardar 1 frame para garantir que todos os Awake() terminaram
        yield return null;
        
        Debug.Log($"[LoadoutSync] Preparing to send loadout to server...");
        Debug.Log($"[LoadoutSync] Character: {LoadoutSelections.SelectedCharacterPrefab?.name ?? "NULL"}");
        Debug.Log($"[LoadoutSync] Weapon: {LoadoutSelections.SelectedWeapon?.name ?? "NULL"}");
        Debug.Log($"[LoadoutSync] Runes: {LoadoutSelections.SelectedRunes?.Count ?? 0}");
        
        // Use the public helper which handles both host (apply locally) and client (ServerRpc)
        RequestSendSelectionToServer();
    }

    private void TrySendSelectionToServer()
    {
        var payload = BuildSelectionPayload();
        Debug.Log($"[LoadoutSync] Submitting to server: weaponId={payload.weaponId}, charIndex={payload.characterIndex}, runes={payload.runeIdsCsv}");
        SubmitSelectionServerRpc(payload.weaponId, payload.characterIndex, payload.runeIdsCsv);
    }

    // Build compact payload from current LoadoutSelections
    private (int weaponId, int characterIndex, string runeIdsCsv) BuildSelectionPayload()
    {
        int weaponId = -1;
        if (LoadoutSelections.WeaponRegistryContext != null && LoadoutSelections.SelectedWeapon != null)
        {
            weaponId = LoadoutSelections.WeaponRegistryContext.GetWeaponId(LoadoutSelections.SelectedWeapon);
        }
        int characterIndex = -1;
        if (LoadoutSelections.CharacterPrefabsContext != null && LoadoutSelections.SelectedCharacterPrefab != null)
        {
            characterIndex = LoadoutSelections.CharacterPrefabsContext.IndexOf(LoadoutSelections.SelectedCharacterPrefab);
        }
        var runeIds = (LoadoutSelections.SelectedRunes != null)
            ? LoadoutSelections.SelectedRunes.Where(r => r != null && !string.IsNullOrEmpty(r.runeId)).Select(r => r.runeId).ToArray()
            : Array.Empty<string>();
        string runeIdsCsv = string.Join("|", runeIds);
        return (weaponId, characterIndex, runeIdsCsv);
    }

    // Public helper: request immediate send of current selection to server.
    // Works for normal clients (via ServerRpc) and for hosts (applies directly on server side).
    public void RequestSendSelectionToServer()
    {
        if (!IsClient) return;
        var payload = BuildSelectionPayload();
        if (IsServer)
        {
            // Host: apply directly as if server received it from OwnerClientId
            ProcessReceivedSelection(payload.weaponId, payload.characterIndex, payload.runeIdsCsv, OwnerClientId);
        }
        else
        {
            SubmitSelectionServerRpc(payload.weaponId, payload.characterIndex, payload.runeIdsCsv);
        }
    }

    [ServerRpc]
    private void SubmitSelectionServerRpc(int weaponId, int characterIndex, string runeIdsCsv, ServerRpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        ProcessReceivedSelection(weaponId, characterIndex, runeIdsCsv, sender);
    }

    // Centralized server-side processing for a received selection from clientId.
    private void ProcessReceivedSelection(int weaponId, int characterIndex, string runeIdsCsv, ulong senderClientId)
    {
        List<string> runeList;
        if (string.IsNullOrEmpty(runeIdsCsv))
        {
            runeList = new List<string>();
        }
        else
        {
            runeList = runeIdsCsv.Split('|').Where(s => !string.IsNullOrEmpty(s)).ToList();
        }
        var sel = new SyncedSelection
        {
            weaponId = weaponId,
            runeIds = runeList != null ? runeList : new List<string>(),
            characterIndex = characterIndex
        };
        ServerSelections[senderClientId] = sel;

        Debug.Log($"[LoadoutSync] Server received loadout from client {senderClientId}: weaponId={weaponId}, charIndex={characterIndex}, runes={runeList.Count}");

        // Apply immediately if components available
        // Try to apply on the NetworkObject instance for this sender if present
        try
        {
            var spawnMgr = NetworkManager.Singleton?.SpawnManager;
            if (spawnMgr != null)
            {
                var playerNO = spawnMgr.GetPlayerNetworkObject(senderClientId);
                if (playerNO != null)
                {
                    var comp = playerNO.GetComponent<LoadoutSync>();
                    if (comp != null)
                    {
                        comp.ApplySelectionOnServer(sel);
                    }
                }
                else
                {
                    // Fallback: try to apply on this object if it matches sender
                    if (OwnerClientId == senderClientId)
                    {
                        ApplySelectionOnServer(sel);
                    }
                }
            }
            else
            {
                if (IsServer && OwnerClientId == senderClientId)
                {
                    ApplySelectionOnServer(sel);
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[LoadoutSync] Failed to apply selection on server: {ex.Message}");
        }

        // Ensure the server gives the starting weapon in case it already ran earlier
        try
        {
            var spawnMgr2 = NetworkManager.Singleton?.SpawnManager;
            if (spawnMgr2 != null)
            {
                var playerNO2 = spawnMgr2.GetPlayerNetworkObject(senderClientId);
                if (playerNO2 != null)
                {
                    var pwm = playerNO2.GetComponent<PlayerWeaponManager>();
                    if (pwm != null)
                    {
                        Debug.Log($"[LoadoutSync] Instructing server to (re)give starting weapon for client {senderClientId}");
                        pwm.Server_GiveStartingWeapon();
                    }
                }
            }
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[LoadoutSync] Failed to reapply starting weapon: {ex.Message}");
        }
    }

    private void ApplySelectionOnServer(SyncedSelection sel)
    {
        if (!IsServer) return;
        var pwm = GetComponent<PlayerWeaponManager>();
        var ps = GetComponent<PlayerStats>();
        var runtime = GetComponent<RuneRuntime>();
        if (pwm == null || ps == null) return;

        // Override starting weapon field on server before Server_GiveStartingWeapon is called
        if (sel.weaponId >= 0 && pwm.Registry != null)
        {
            var data = pwm.Registry.GetWeaponData(sel.weaponId);
            if (data != null)
            {
                // No need to mutate the private serialized startingWeapon field via SerializedObject (Editor-only API).
                // PlayerWeaponManager.Server_GiveStartingWeapon already checks LoadoutSync for an override weaponId
                // and uses that instead of its serialized startingWeapon.
            }
        }

        // Character prefab selection (host validates via context list)
        if (sel.characterIndex >= 0 && LoadoutSelections.CharacterPrefabsContext != null && sel.characterIndex < LoadoutSelections.CharacterPrefabsContext.Count)
        {
            var chosenPrefab = LoadoutSelections.CharacterPrefabsContext[sel.characterIndex];
            // We cannot swap a spawned player object easily; log for lobby-phase implementation.
            // For future: host should spawn correct prefab using characterIndex before player object creation.
        }

        // Resolve rune definitions by ID using LoadoutSelections.RuneCatalogContext (optional) or scene scan
        List<RuneDefinition> defs = new List<RuneDefinition>();
        if (sel.runeIds != null && sel.runeIds.Count > 0)
        {
            if (LoadoutSelections.RuneCatalogContext != null)
            {
                var map = LoadoutSelections.RuneCatalogContext.Where(r => r != null).ToDictionary(r => r.runeId, r => r);
                foreach (var id in sel.runeIds)
                {
                    if (string.IsNullOrEmpty(id)) continue;
                    if (map.TryGetValue(id, out var def)) defs.Add(def);
                }
            }
            else
            {
                // Last resort: find all RuneDefinition in Resources (requires they be placed in Resources) or skip
            }
        }

        // Ensure ApplyRunesOnSpawn exists and initialize runtime with defs
        var apply = GetComponent<ApplyRunesOnSpawn>();
        if (apply == null) apply = gameObject.AddComponent<ApplyRunesOnSpawn>();
        // In MP, ApplyRunesOnSpawn applies only on server; we initialize runtime ourselves to ensure effects
        if (defs.Count > 0)
        {
            var rr = GetComponent<RuneRuntime>();
            if (rr == null) rr = gameObject.AddComponent<RuneRuntime>();
            rr.Initialize(ps, defs);
        }
    }

    // Public server accessor for GameManager/PlayerWeaponManager
    public static bool TryGetSelectionFor(ulong clientId, out SyncedSelection selection)
    {
        return ServerSelections.TryGetValue(clientId, out selection);
    }
}
