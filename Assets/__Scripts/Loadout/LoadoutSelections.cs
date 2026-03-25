using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using DropsAndLoadout;

// Central place to store the player's current loadout choices at runtime and to persist minimal state in PlayerPrefs.
public static class LoadoutSelections
{
    // Runtime selections (set by LoadoutPanel before starting)
    public static GameObject SelectedCharacterPrefab { get; private set; }
    public static WeaponData SelectedWeapon { get; private set; }
    public static ItemDrop SelectedItemDrop { get; private set; }
    public static string EquippedWeaponUniqueId { get; private set; }
    public static List<RuneDefinition> SelectedRunes { get; private set; } = new List<RuneDefinition>();

    // Optional registries to help resolve indices when saving/loading
    public static List<GameObject> CharacterPrefabsContext { get; set; }
    public static WeaponRegistry WeaponRegistryContext { get; set; }
    public static List<RuneDefinition> RuneCatalogContext { get; set; }

    // Keys
    private const string K_CHAR_INDEX = "Loadout_CharacterIndex";
    private const string K_WEAPON_INDEX = "Loadout_WeaponIndex";
    private const string K_RUNES_IDS = "Loadout_RunesCSV";
    private const string K_HAS_CONFIGURED = "Loadout_HasConfigured";
    private const string K_ITEM_UNIQUE_ID = "Loadout_ItemUniqueId";

    public static void SetSelections(GameObject characterPrefab, WeaponData weapon, IEnumerable<RuneDefinition> runes, ItemDrop itemDrop = null)
    {
        SelectedCharacterPrefab = characterPrefab;
        SelectedWeapon = weapon;
        SelectedItemDrop = itemDrop;
        if (itemDrop != null) EquippedWeaponUniqueId = itemDrop.ItemUniqueId;
        else EquippedWeaponUniqueId = null;
        SelectedRunes = (runes != null) ? new List<RuneDefinition>(runes.Where(r => r != null)) : new List<RuneDefinition>();
    }

    public static void ResetSelections()
    {
        SelectedCharacterPrefab = null;
        SelectedWeapon = null;
        SelectedItemDrop = null;
        EquippedWeaponUniqueId = null;
        SelectedRunes = new List<RuneDefinition>();
        Debug.Log("[LoadoutSelections] Selections reset to null. Next run will generate new defaults.");
    }

    // Check if player has ever configured their loadout manually
    public static bool HasBeenConfigured()
    {
        return PlayerPrefs.GetInt(K_HAS_CONFIGURED, 0) == 1;
    }

    // Mark that player has manually configured their loadout
    public static void MarkAsConfigured()
    {
        PlayerPrefs.SetInt(K_HAS_CONFIGURED, 1);
        PlayerPrefs.Save();
    }

    // Initialize with random defaults if no valid selections exist
    public static void EnsureValidDefaults()
    {
        // Random character if none selected
        if (SelectedCharacterPrefab == null && CharacterPrefabsContext != null && CharacterPrefabsContext.Count > 0)
        {
            int randomCharIdx = Random.Range(0, CharacterPrefabsContext.Count);
            SelectedCharacterPrefab = CharacterPrefabsContext[randomCharIdx];
            Debug.Log($"[LoadoutSelections] Auto-selected random character: {SelectedCharacterPrefab.name}");
        }

        // Default weapon if none selected
        if (SelectedWeapon == null && WeaponRegistryContext != null && WeaponRegistryContext.allWeapons != null && WeaponRegistryContext.allWeapons.Count > 0)
        {
            // Default to Panela/Pitchfork if it exists, else index 0
            SelectedWeapon = WeaponRegistryContext.allWeapons.FirstOrDefault(w => w != null && (w.name == "FlyingPan" || w.weaponName == "FlyingPan" || w.name == "Pitchfork" || w.weaponName == "Pitchfork" || w.name == "Panela" || w.weaponName == "Panela"));
            if (SelectedWeapon == null)
            {
                SelectedWeapon = WeaponRegistryContext.GetWeaponData(0);
            }
            Debug.Log($"[LoadoutSelections] Auto-selected default weapon: {SelectedWeapon.name}");
        }

        // Initialize empty runes list if null
        if (SelectedRunes == null)
        {
            SelectedRunes = new List<RuneDefinition>();
        }
    }

    public static void SaveToPlayerPrefs()
    {
        // Character index
        int charIndex = -1;
        if (CharacterPrefabsContext != null && SelectedCharacterPrefab != null)
        {
            charIndex = CharacterPrefabsContext.IndexOf(SelectedCharacterPrefab);
        }
        PlayerPrefs.SetInt(K_CHAR_INDEX, charIndex);

        // Weapon index
        int weaponIndex = -1;
        if (WeaponRegistryContext != null && WeaponRegistryContext.allWeapons != null && SelectedWeapon != null)
        {
            weaponIndex = WeaponRegistryContext.GetWeaponId(SelectedWeapon);
        }
        PlayerPrefs.SetInt(K_WEAPON_INDEX, weaponIndex);

        // Runes by ID CSV
        if (SelectedRunes != null && SelectedRunes.Count > 0)
        {
            var csv = string.Join(",", SelectedRunes.Where(r => r != null && !string.IsNullOrEmpty(r.runeId)).Select(r => r.runeId));
            PlayerPrefs.SetString(K_RUNES_IDS, csv);
        }
        else
        {
            PlayerPrefs.DeleteKey(K_RUNES_IDS);
        }

        // Save EquippedUniqueId
        if (!string.IsNullOrEmpty(EquippedWeaponUniqueId))
        {
            PlayerPrefs.SetString(K_ITEM_UNIQUE_ID, EquippedWeaponUniqueId);
        }
        else
        {
            PlayerPrefs.DeleteKey(K_ITEM_UNIQUE_ID);
        }

        PlayerPrefs.Save();
    }

    // Uses contexts to resolve references. Safe if contexts are not present (keeps runtime selections unchanged).
    public static void LoadFromPlayerPrefs()
    {
        // Character
        int charIndex = PlayerPrefs.GetInt(K_CHAR_INDEX, -1);
        if (charIndex >= 0 && CharacterPrefabsContext != null && charIndex < CharacterPrefabsContext.Count)
        {
            SelectedCharacterPrefab = CharacterPrefabsContext[charIndex];
        }

        // Weapon
        int weaponIndex = PlayerPrefs.GetInt(K_WEAPON_INDEX, -1);
        if (weaponIndex >= 0 && WeaponRegistryContext != null && WeaponRegistryContext.allWeapons != null)
        {
            if (weaponIndex < WeaponRegistryContext.allWeapons.Count)
            {
                SelectedWeapon = WeaponRegistryContext.GetWeaponData(weaponIndex);
            }
            else
            {
                Debug.LogWarning($"[LoadoutSelections] Saved weapon index {weaponIndex} is out of bounds (Count: {WeaponRegistryContext.allWeapons.Count}). Resetting selection.");
                // Fallback to random or default if needed, or just leave it null so EnsureValidDefaults can handle it if called.
                // For now we just avoid the crash.
                SelectedWeapon = null;
            }
        }

        // Runes
        if (RuneCatalogContext != null)
        {
            var csv = PlayerPrefs.GetString(K_RUNES_IDS, null);
            if (!string.IsNullOrEmpty(csv))
            {
                var ids = new HashSet<string>(csv.Split(','));
                SelectedRunes = RuneCatalogContext.Where(r => r != null && ids.Contains(r.runeId)).ToList();
            }
        }

        // Restore Items
        EquippedWeaponUniqueId = PlayerPrefs.GetString(K_ITEM_UNIQUE_ID, null);

        if (!string.IsNullOrEmpty(EquippedWeaponUniqueId) && LoadoutSystem.Instance != null && LoadoutSystem.Instance.Inventory != null)
        {
            SelectedItemDrop = LoadoutSystem.Instance.Inventory.FirstOrDefault(i => i.ItemUniqueId == EquippedWeaponUniqueId);
            
            // Re-resolve the actual base WeaponData from the selected item drop
            // to ensure it is robust against WeaponRegistry list changes across game versions.
            if (SelectedItemDrop != null && WeaponRegistryContext != null && WeaponRegistryContext.allWeapons != null)
            {
                string targetName = SelectedItemDrop.ItemName;
                if (targetName == "Panela") targetName = "FlyingPan"; // Backward compatibility for old saves

                var resolvedWd = WeaponRegistryContext.allWeapons.FirstOrDefault(w => w != null && (w.name == targetName || w.weaponName == targetName));
                if (resolvedWd != null)
                {
                    SelectedWeapon = resolvedWd;
                }
            }
        }

        // Force Panela as the true default if no valid item drop loadout is configured
        if (SelectedItemDrop == null && WeaponRegistryContext != null && WeaponRegistryContext.allWeapons != null)
        {
            var panela = WeaponRegistryContext.allWeapons.FirstOrDefault(w => w != null && (w.name == "FlyingPan" || w.weaponName == "FlyingPan" || w.name == "Pitchfork" || w.weaponName == "Pitchfork" || w.name == "Panela" || w.weaponName == "Panela"));
            if (panela != null)
            {
                SelectedWeapon = panela;
            }
        }
    }
}
