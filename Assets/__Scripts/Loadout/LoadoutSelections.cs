using UnityEngine;
using System.Collections.Generic;
using System.Linq;

// Central place to store the player's current loadout choices at runtime and to persist minimal state in PlayerPrefs.
public static class LoadoutSelections
{
    // Runtime selections (set by LoadoutPanel before starting)
    public static GameObject SelectedCharacterPrefab { get; private set; }
    public static WeaponData SelectedWeapon { get; private set; }
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

    public static void SetSelections(GameObject characterPrefab, WeaponData weapon, IEnumerable<RuneDefinition> runes)
    {
        SelectedCharacterPrefab = characterPrefab;
        SelectedWeapon = weapon;
        SelectedRunes = (runes != null) ? new List<RuneDefinition>(runes.Where(r => r != null)) : new List<RuneDefinition>();
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

        // Random weapon if none selected
        if (SelectedWeapon == null && WeaponRegistryContext != null && WeaponRegistryContext.allWeapons != null && WeaponRegistryContext.allWeapons.Count > 0)
        {
            int randomWeaponIdx = Random.Range(0, WeaponRegistryContext.allWeapons.Count);
            SelectedWeapon = WeaponRegistryContext.GetWeaponData(randomWeaponIdx);
            Debug.Log($"[LoadoutSelections] Auto-selected random weapon: {SelectedWeapon.name}");
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
        if (weaponIndex >= 0 && WeaponRegistryContext != null)
        {
            SelectedWeapon = WeaponRegistryContext.GetWeaponData(weaponIndex);
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
    }
}
