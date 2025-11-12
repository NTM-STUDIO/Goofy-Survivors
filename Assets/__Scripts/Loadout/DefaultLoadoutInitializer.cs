using UnityEngine;
using System.Linq;

[DefaultExecutionOrder(-100)]
public class DefaultLoadoutInitializer : MonoBehaviour
{
    [Header("Data Sources (mesmas do LoadoutPanel)")]
    [Tooltip("List of available characters. Deve coincidir com o LoadoutPanel.")]
    public PlayerCharacterData[] availableCharacters;
    
    [Tooltip("Registry of all weapons.")]
    public WeaponRegistry weaponRegistry;
    
    [Tooltip("Catalog of runes (opcional).")]
    public RuneDefinition[] runeCatalog;

    void Awake()
    {
        Debug.Log("[DefaultLoadoutInitializer] === INITIALIZING LOADOUT ===");

        if (availableCharacters != null && availableCharacters.Length > 0)
        {
            LoadoutSelections.CharacterPrefabsContext = availableCharacters
                .Where(c => c != null && c.playerPrefab != null)
                .Select(c => c.playerPrefab)
                .ToList();
            Debug.Log($"[DefaultLoadoutInitializer] Set {LoadoutSelections.CharacterPrefabsContext.Count} characters in context");
        }
        else
        {
            Debug.LogError("[DefaultLoadoutInitializer] No available characters assigned in Inspector!");
        }

        LoadoutSelections.WeaponRegistryContext = weaponRegistry;
        if (weaponRegistry != null && weaponRegistry.allWeapons != null)
        {
            Debug.Log($"[DefaultLoadoutInitializer] Set {weaponRegistry.allWeapons.Count} weapons in context");
        }
        else
        {
            Debug.LogError("[DefaultLoadoutInitializer] No weapon registry assigned in Inspector!");
        }

        if (runeCatalog != null && runeCatalog.Length > 0)
        {
            LoadoutSelections.RuneCatalogContext = runeCatalog.Where(r => r != null).ToList();
            Debug.Log($"[DefaultLoadoutInitializer] Set {LoadoutSelections.RuneCatalogContext.Count} runes in context");
        }

        bool wasConfigured = LoadoutSelections.HasBeenConfigured();
        Debug.Log($"[DefaultLoadoutInitializer] Has been configured before: {wasConfigured}");

        if (wasConfigured)
        {
            // Carregar escolhas anteriores guardadas
            LoadoutSelections.LoadFromPlayerPrefs();
            Debug.Log($"[DefaultLoadoutInitializer] Loaded from PlayerPrefs: Character={LoadoutSelections.SelectedCharacterPrefab?.name}, Weapon={LoadoutSelections.SelectedWeapon?.name}");
        }
        else
        {
            // Primeira vez ou nunca configurou - usar defaults aleatórios
            Debug.Log("[DefaultLoadoutInitializer] First time or no loadout configured - using random defaults");
            LoadoutSelections.EnsureValidDefaults();
        }

        Debug.Log($"[DefaultLoadoutInitializer] FINAL LOADOUT: Character={LoadoutSelections.SelectedCharacterPrefab?.name}, Weapon={LoadoutSelections.SelectedWeapon?.name}, Runes={LoadoutSelections.SelectedRunes?.Count ?? 0}");
    }

    void Start()
    {
        var gm = GameManager.Instance;
        if (gm != null && !gm.isP2P)
        {
            if (LoadoutSelections.SelectedCharacterPrefab != null)
            {
                gm.SetChosenPlayerPrefab(LoadoutSelections.SelectedCharacterPrefab);
                Debug.Log($"[DefaultLoadoutInitializer] Set GameManager character to: {LoadoutSelections.SelectedCharacterPrefab.name}");
            }
            else
            {
                Debug.LogError("[DefaultLoadoutInitializer] SelectedCharacterPrefab is null! Cannot set GameManager character.");
            }
        }
        else if (gm != null && gm.isP2P)
        {
            Debug.Log("[DefaultLoadoutInitializer] Multiplayer mode - LoadoutSync will handle character selection");
        }
    }
}
