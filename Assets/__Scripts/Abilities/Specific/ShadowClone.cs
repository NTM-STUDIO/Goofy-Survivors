using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

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

    private void OnDestroy()
    {
        // Ensure any aura proxies created under this clone are removed (server-only object)
        var serverAuras = GetComponentsInChildren<ServerAura>(true);
        foreach (var sa in serverAuras)
        {
            if (sa != null) Destroy(sa.gameObject);
        }

        // Clean up any AuraWeapon instances that were parented under this clone
        var auras = GetComponentsInChildren<AuraWeapon>(true);
        foreach (var aura in auras)
        {
            if (aura == null) continue;
            var no = aura.GetComponent<NetworkObject>();
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening)
            {
                // MP path: only the server should despawn networked objects
                if (nm.IsServer)
                {
                    if (no != null && no.IsSpawned) no.Despawn(true); else Destroy(aura.gameObject);
                }
            }
            else
            {
                // Single-player path
                Destroy(aura.gameObject);
            }
        }

        // Clean up any orbiters that were parented under this clone (SP and MP safety)
        var orbiters = GetComponentsInChildren<OrbitingWeapon>(true);
        foreach (var orb in orbiters)
        {
            if (orb == null) continue;
            var no = orb.GetComponent<NetworkObject>();
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening)
            {
                // MP path: only the server should despawn networked objects
                if (nm.IsServer)
                {
                    if (no != null && no.IsSpawned) no.Despawn(true); else Destroy(orb.gameObject);
                }
            }
            else
            {
                // Single-player path
                Destroy(orb.gameObject);
            }
        }
    }
}