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
    public void OpenChest(PlayerWeaponManager interactingPlayer)
    {
        if (hasOpened) return; // Prevent opening the same chest twice
        hasOpened = true;

        // --- VALIDATION CHECKS ---
        if (weaponRegistry == null)
        {
            Debug.LogError("ChestScript Error: WeaponRegistry has not been assigned in the Inspector! The chest cannot function.", this.gameObject);
            return;
        }

        // Use the specific player who opened the chest (passed from trigger)
        if (interactingPlayer == null)
        {
            Debug.LogError("OpenChest failed: Interacting PlayerWeaponManager was null.", this.gameObject);
            return;
        }

    UIManager uiManager = Object.FindFirstObjectByType<UIManager>();
        // In P2P we do not need UIManager here on the server, as UI will be shown via targeted ClientRpc

        // --- CORE LOGIC (THE FIX) ---

        // 1. Get the list of ALL possible weapons from the registry.
        List<WeaponData> allPossibleWeapons = weaponRegistry.allWeapons;

        // 2. Get the list of weapons the player ALREADY has by calling the new helper method.
    List<WeaponData> ownedWeapons = interactingPlayer.GetOwnedWeapons();

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

            // Award only to the interacting player
            var gm = GameManager.Instance;
            if (gm != null && gm.isP2P)
            {
                // Server gives to the owner and shows UI on that client's screen only
                interactingPlayer.Server_GiveWeaponToOwner(weaponRegistry.GetWeaponId(newWeapon));
            }
            else
            {
                // Single-player path: add locally and show UI locally
                interactingPlayer.AddWeapon(newWeapon);
                if (uiManager != null)
                {
                    uiManager.OpenNewWeaponPanel(newWeapon);
                }
            }
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