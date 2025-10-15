using System.Collections.Generic;
using UnityEngine;

public class PlayerWeaponManager : MonoBehaviour
{
    public List<WeaponData> weapons; // Current weapons
    public List<WeaponData> possibleWeapons; // All possible weapons
    public Transform weaponParent; // A transform to spawn weapons under

    void Start()
    {
        // This part is for starting weapons, it's a bit redundant but okay.
        // We will make a temp copy to avoid issues while iterating.
        var startingWeapons = new List<WeaponData>(weapons); 
        weapons.Clear(); // Start with a clean list
        foreach (WeaponData weapon in startingWeapons)
        {
            AddWeapon(weapon);
        }
    }

    public void AddWeapon(WeaponData weaponData)
    {
        // First, check if we already have this weapon to be absolutely safe.
        if (weapons.Contains(weaponData))
        {
            // Optionally handle leveling up the weapon here instead.
            return; 
        }

        // --- THIS IS THE MISSING LINE ---
        weapons.Add(weaponData);
        // ---------------------------------

        // Create a new GameObject to hold the weapon's logic
        GameObject weaponObject = new GameObject(weaponData.name + " Controller");
        weaponObject.transform.SetParent(weaponParent); // Keep the hierarchy clean

        // Add the controller and assign its data
        WeaponController controller = weaponObject.AddComponent<WeaponController>();
        controller.weaponData = weaponData;
    }
}