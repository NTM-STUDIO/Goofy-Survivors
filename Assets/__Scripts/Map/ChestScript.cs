using UnityEngine;
using System.Linq;
using System.Collections.Generic;

public class ChestScript : MonoBehaviour
{
    public void OpenChest()
    {
        PlayerWeaponManager playerWeaponManager = FindObjectOfType<PlayerWeaponManager>();
        if (playerWeaponManager == null)
        {
            Debug.LogError("OpenChest failed: Could not find a PlayerWeaponManager in the scene.");
            return;
        }

        UIManager uiManager = FindObjectOfType<UIManager>();
        if (uiManager == null)
        {
            Debug.LogError("OpenChest failed: Could not find a UIManager in the scene.");
            return;
        }

        List<WeaponData> availableWeapons = playerWeaponManager.possibleWeapons
            .Except(playerWeaponManager.weapons)
            .ToList();

        if (availableWeapons.Count > 0)
        {
            int randomIndex = Random.Range(0, availableWeapons.Count);
            WeaponData newWeapon = availableWeapons[randomIndex];
            playerWeaponManager.AddWeapon(newWeapon);
            uiManager.NewWeaponUi(newWeapon);
        }
        else
        {
            Debug.Log("Player has all available weapons from chests. Dropping a standard item instead.");
            MapConsumable mapConsumable = GetComponent<MapConsumable>();
            if (mapConsumable != null)
            {
                mapConsumable.DropRandomItem();
            }
        }
    }
}