using System.Collections.Generic;
using UnityEngine;

public class PlayerWeaponManager : MonoBehaviour
{
    public List<WeaponData> weapons; // Current weapons
    public List<WeaponData> possibleWeapons; // All possible weapons
    public Transform weaponParent; // A transform to spawn weapons under

    void Start()
    {
        foreach (WeaponData weapon in weapons)
        {
            AddWeapon(weapon);
        }
    }

    public void AddWeapon(WeaponData weaponData)
    {
        // Create a new GameObject to hold the weapon's logiac
        GameObject weaponObject = new GameObject(weaponData.weaponName + " Controller");
        weaponObject.transform.SetParent(weaponParent); // Keep the hierarchy clean

        // Add the controller and assign its data
        WeaponController controller = weaponObject.AddComponent<WeaponController>();
        controller.weaponData = weaponData;
    }
}