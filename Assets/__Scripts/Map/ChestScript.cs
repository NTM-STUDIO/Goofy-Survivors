using UnityEngine;
using System.Linq;
using System.Collections.Generic;

public class ChestScript : MonoBehaviour
{
    [Header("Required Asset")]
    [Tooltip("You MUST assign your WeaponRegistry asset here for the chest to know what weapons exist.")]
    [SerializeField] private WeaponRegistry weaponRegistry;

    private bool hasOpened = false;

    // This method should be called by the player who interacts with the chest.
    public void OpenChest()
    {
        if (hasOpened) return; // Prevent opening the same chest twice
        hasOpened = true;

        // --- VALIDATION CHECKS ---
        if (weaponRegistry == null)
        {
            Debug.LogError("ChestScript Error: WeaponRegistry has not been assigned in the Inspector! The chest cannot function.", this.gameObject);
            return;
        }

        // In a multiplayer game, you would ideally get the PlayerWeaponManager from the specific player who opened the chest.
        // FindObjectOfType will find the first one, which works for single-player but might not be the correct player in multiplayer.
        // For now, this matches your original logic.
        PlayerWeaponManager playerWeaponManager = FindObjectOfType<PlayerWeaponManager>();
        if (playerWeaponManager == null)
        {
            Debug.LogError("OpenChest failed: Could not find a PlayerWeaponManager in the scene.", this.gameObject);
            return;
        }

        UIManager uiManager = FindObjectOfType<UIManager>();
        if (uiManager == null)
        {
            Debug.LogError("OpenChest failed: Could not find a UIManager in the scene.", this.gameObject);
            return;
        }

        // --- CORE LOGIC (THE FIX) ---

        // 1. Get the list of ALL possible weapons from the registry.
        List<WeaponData> allPossibleWeapons = weaponRegistry.allWeapons;

        // 2. Get the list of weapons the player ALREADY has by calling the new helper method.
        List<WeaponData> ownedWeapons = playerWeaponManager.GetOwnedWeapons();

        // 3. Use LINQ's Except() to find the weapons the player does NOT own yet.
        List<WeaponData> availableWeapons = allPossibleWeapons
            .Except(ownedWeapons)
            .ToList();

        // --- AWARDING THE WEAPON ---

        if (availableWeapons.Count > 0)
        {
            // Pick a random weapon from the list of available ones.
            int randomIndex = Random.Range(0, availableWeapons.Count);
            WeaponData newWeapon = availableWeapons[randomIndex];

            // Use the refactored AddWeapon method. This works for both single-player and multiplayer.
            playerWeaponManager.AddWeapon(newWeapon);

            // Trigger the UI to show what the player got.
            uiManager.OpenNewWeaponPanel(newWeapon);
        }
        else
        {
            // This is your original, correct fallback logic.
            Debug.Log("Player has all available weapons from chests. Dropping a standard item instead.");
            MapConsumable mapConsumable = GetComponent<MapConsumable>();
            if (mapConsumable != null)
            {
                mapConsumable.DropRandomItem();
            }
        }
    }
}