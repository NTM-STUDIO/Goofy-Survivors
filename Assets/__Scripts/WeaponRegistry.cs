using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// A ScriptableObject that acts as a central database for all WeaponData in the game.
/// This allows us to synchronize weapons over the network using a simple integer ID
/// instead of trying to send complex data.
/// </summary>
[CreateAssetMenu(fileName = "WeaponRegistry", menuName = "Survivors Clone/Weapon Registry")]
public class WeaponRegistry : ScriptableObject
{
    // This is the master list. You will drag all of your WeaponData assets here in the Inspector.
    // The order of this list is CRITICAL. The index of an item IS its network ID.
    public List<WeaponData> allWeapons;

    /// <summary>
    /// Looks up a WeaponData object and returns its unique ID (its index in the list).
    /// </summary>
    /// <param name="data">The WeaponData to find.</param>
    /// <returns>The integer ID of the weapon, or -1 if not found.</returns>
    public int GetWeaponId(WeaponData data)
    {
        return allWeapons.IndexOf(data);
    }

    /// <summary>
    /// Looks up a weapon ID and returns the corresponding WeaponData object.
    /// </summary>
    /// <param name="id">The integer ID of the weapon.</param>
    /// <returns>The WeaponData object, or null if the ID is invalid.</returns>
    public WeaponData GetWeaponData(int id)
    {
        // Safety check to make sure the ID is within the bounds of our list.
        if (id >= 0 && id < allWeapons.Count)
        {
            return allWeapons[id];
        }
        
        Debug.LogError($"[WeaponRegistry] Invalid weapon ID requested: {id}. Make sure all weapons are in the registry.");
        return null;
    }
}