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
        // Only the owning client sends their selection up
        if (IsOwner && IsClient && !IsServer)
        {
            TrySendSelectionToServer();
        }
    }

    private void TrySendSelectionToServer()
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
        // Send rune IDs by string
        var runeIds = (LoadoutSelections.SelectedRunes != null)
            ? LoadoutSelections.SelectedRunes.Where(r => r != null && !string.IsNullOrEmpty(r.runeId)).Select(r => r.runeId).ToArray()
            : Array.Empty<string>();
        string runeIdsCsv = string.Join("|", runeIds);

        SubmitSelectionServerRpc(weaponId, characterIndex, runeIdsCsv);
    }

    [ServerRpc]
    private void SubmitSelectionServerRpc(int weaponId, int characterIndex, string runeIdsCsv)
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
            runeIds = runeList != null ? runeList : new List<string>()
            , characterIndex = characterIndex
        };
        ServerSelections[OwnerClientId] = sel;

        // Apply immediately if components available
        ApplySelectionOnServer(sel);
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
