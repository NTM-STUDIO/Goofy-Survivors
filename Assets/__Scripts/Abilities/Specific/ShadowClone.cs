using System.Collections.Generic;
using UnityEngine;

public class ShadowClone : MonoBehaviour
{
    [Header("Clone Stats")]
    [SerializeField] private float lifetime = 5f;
    [SerializeField] private float health = 1f;

    [Header("Internal References")]
    [Tooltip("An empty child object to organize the clone's weapons.")]
    [SerializeField] private Transform weaponContainer;

    void Start()
    {
        if (weaponContainer == null)
        {
            weaponContainer = new GameObject("WeaponContainer").transform;
            weaponContainer.SetParent(this.transform);
            weaponContainer.localPosition = Vector3.zero;
        }
        Destroy(gameObject, lifetime);
    }

    /// <summary>
    /// THE FIX: This method now accepts the owner's stats and the weapon registry.
    /// It uses this data to properly initialize the weapons it creates.
    /// </summary>
    public void Initialize(List<WeaponData> playerWeapons, PlayerStats ownerStats, WeaponRegistry registry)
    {
        if (playerWeapons == null || ownerStats == null || registry == null) return;

        foreach (var weaponData in playerWeapons)
        {
            GameObject weaponControllerObj = new GameObject(weaponData.weaponName + " (Clone)");
            weaponControllerObj.transform.SetParent(weaponContainer);

            WeaponController wc = weaponControllerObj.AddComponent<WeaponController>();
            
            // This is the correct way to set up the new controller.
            // We pass 'null' for the manager because a clone's weapons are self-contained.
            // We pass 'true' for ownership because the clone's weapons are always controlled by the clone itself.
            int weaponId = registry.GetWeaponId(weaponData);
            wc.Initialize(weaponId, weaponData, null, ownerStats, true, registry);
        }
    }

    /// <summary>
    /// Public method for the clone to take damage.
    /// </summary>
    public void TakeDamage(float amount)
    {
        if (health <= 0) return;

        health -= amount;
        if (health <= 0)
        {
            Destroy(gameObject);
        }
    }
}