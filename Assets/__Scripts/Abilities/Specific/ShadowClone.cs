using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class ShadowClone : MonoBehaviour
{
    [Header("Clone Stats")]
    [SerializeField] private float lifetime = 5f;
    [SerializeField] private float health = 1f;

    [Header("Internal References")]
    [Tooltip("An empty child object to organize the clone's weapons.")]
    [SerializeField] private Transform weaponContainer;

    private PlayerStats ownerStats;
    private WeaponRegistry registry;

    void Start()
    {
        // CRITICAL: Ensure clone is NOT parented to player
        if (transform.parent != null)
        {
            Debug.LogWarning($"[ShadowClone] Was parented to {transform.parent.name}, unparenting now!");
            Vector3 worldPos = transform.position;
            Quaternion worldRot = transform.rotation;
            transform.SetParent(null, true);
            transform.position = worldPos;
            transform.rotation = worldRot;
        }
        
        // Disable any movement/player components that might be on the prefab
        DisablePlayerComponents();
        
        if (weaponContainer == null)
        {
            weaponContainer = new GameObject("WeaponContainer").transform;
            weaponContainer.SetParent(this.transform);
            weaponContainer.localPosition = Vector3.zero;
        }
        Destroy(gameObject, lifetime);
    }

    /// <summary>
    /// Disables player-specific components that shouldn't run on a clone (movement, input, etc.)
    /// </summary>
    private void DisablePlayerComponents()
    {
        // Disable Movement script if present (prevents clone from moving with player input)
        var movement = GetComponent<Movement>();
        if (movement != null)
        {
            movement.enabled = false;
            Debug.Log("[ShadowClone] Disabled Movement component");
        }
        
        // Disable Rigidbody velocity (freeze in place)
        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            Debug.Log("[ShadowClone] Set Rigidbody to kinematic");
        }
        
        // Disable PlayerStats if present (clone uses owner's stats, not its own)
        var stats = GetComponent<PlayerStats>();
        if (stats != null)
        {
            stats.enabled = false;
            Debug.Log("[ShadowClone] Disabled PlayerStats component");
        }
        
        // Disable any NetworkBehaviour components (clone is local-only in SP)
        var networkBehaviours = GetComponents<Unity.Netcode.NetworkBehaviour>();
        foreach (var nb in networkBehaviours)
        {
            if (nb != null)
            {
                nb.enabled = false;
            }
        }
        
        // Disable TwoSpriteIsometricController if present
        var isoController = GetComponent<TwoSpriteIsometricController>();
        if (isoController != null)
        {
            isoController.enabled = false;
            Debug.Log("[ShadowClone] Disabled TwoSpriteIsometricController");
        }
        
        // Disable NetworkPlayerVisuals if present
        var netVisuals = GetComponent<NetworkPlayerVisuals>();
        if (netVisuals != null)
        {
            netVisuals.enabled = false;
            Debug.Log("[ShadowClone] Disabled NetworkPlayerVisuals");
        }
    }

    /// <summary>
    /// THE FIX: This method now accepts the owner's stats and the weapon registry.
    /// It uses this data to properly initialize the weapons it creates.
    /// </summary>
    public void Initialize(List<WeaponData> playerWeapons, PlayerStats ownerStats, WeaponRegistry registry)
    {
        Debug.Log($"[ShadowClone] Initializing clone with {playerWeapons?.Count} weapons");
        
        this.ownerStats = ownerStats;
        this.registry = registry;

        if (playerWeapons == null || ownerStats == null || registry == null) 
        {
            Debug.LogError("[ShadowClone] Missing initialization data!");
            return;
        }

        foreach (var weaponData in playerWeapons)
        {
            if (weaponData == null) continue;

            Debug.Log($"[ShadowClone] Creating weapon: {weaponData.weaponName} (Archetype: {weaponData.archetype})");
            
            GameObject weaponControllerObj = new GameObject(weaponData.weaponName + " (Clone)");
            weaponControllerObj.transform.SetParent(weaponContainer);
            weaponControllerObj.transform.localPosition = Vector3.zero;

            WeaponController wc = weaponControllerObj.AddComponent<WeaponController>();
            int weaponId = registry.GetWeaponId(weaponData);
            
            // IMPORTANTE: Pass null para weaponManager em clones singleplayer
            wc.Initialize(weaponId, weaponData, null, ownerStats, true, registry);

            // Handle specific weapon types
            HandleWeaponType(weaponData, weaponControllerObj);
        }
    }

    private void HandleWeaponType(WeaponData weaponData, GameObject weaponControllerObj)
    {
        switch (weaponData.archetype)
        {
            case WeaponArchetype.Aura:
                SpawnAuraForClone(weaponData, weaponControllerObj);
                break;
                
            case WeaponArchetype.Orbit:
                SpawnOrbitingWeaponForClone(weaponData, weaponControllerObj);
                break;
                
            case WeaponArchetype.Projectile:
                // Clones não devem disparar projéteis em singleplayer
                Debug.Log($"[ShadowClone] Skipping projectile weapon for clone: {weaponData.weaponName}");
                break;
                
            default:
                Debug.Log($"[ShadowClone] Weapon type {weaponData.archetype} handled by WeaponController");
                break;
        }
    }

    private void SpawnAuraForClone(WeaponData weaponData, GameObject parentObj)
    {
        if (weaponData.weaponPrefab == null) return;
        
        GameObject auraObj = Instantiate(weaponData.weaponPrefab, transform.position, Quaternion.identity, parentObj.transform);
        AuraWeapon aura = auraObj.GetComponent<AuraWeapon>();
        if (aura != null) 
        {
            aura.Initialize(ownerStats, weaponData);
            
            // Add tracker to auto-destroy when clone is destroyed
            var auraTracker = auraObj.AddComponent<CloneWeaponTracker>();
            auraTracker.SetParentClone(this);
            
            Debug.Log($"[ShadowClone] Aura spawned for clone: {weaponData.weaponName}");
        }
    }

    private void SpawnOrbitingWeaponForClone(WeaponData weaponData, GameObject parentObj)
    {
        if (weaponData.weaponPrefab == null) return;
        
        int finalAmount = weaponData.amount + ownerStats.projectileCount;
        float angleStep = 360f / Mathf.Max(1, finalAmount);

        for (int i = 0; i < finalAmount; i++)
        {
            GameObject orbitingWeaponObj = Instantiate(weaponData.weaponPrefab, transform.position, Quaternion.identity, parentObj.transform);
            
            // Add tracker to auto-destroy when clone is destroyed
            var orbiterTracker = orbitingWeaponObj.AddComponent<CloneWeaponTracker>();
            orbiterTracker.SetParentClone(this);
            
            var orbiter = orbitingWeaponObj.GetComponent<OrbitingWeapon>();
            if (orbiter != null)
            {
                orbiter.LocalInitialize(transform, i * angleStep, ownerStats, weaponData);
                Debug.Log($"[ShadowClone] Orbiting weapon spawned for clone: {weaponData.weaponName}");
            }
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
            // Start cleanup before destruction
            CleanupAllWeaponsImmediately();
            Destroy(gameObject);
        }
    }

    private void OnDestroy()
    {
        Debug.Log("[ShadowClone] Clone destroyed, starting cleanup...");
        
        // Use a coroutine for safer cleanup
        if (gameObject.activeInHierarchy)
        {
            StartCoroutine(DelayedCleanup());
        }
        else
        {
            CleanupAllWeaponsImmediately();
        }
    }

    private IEnumerator DelayedCleanup()
    {
        // Wait one frame to avoid destruction during frame processing
        yield return null;
        CleanupAllWeaponsImmediately();
    }

    private void CleanupAllWeaponsImmediately()
    {
        Debug.Log("[ShadowClone] Starting immediate weapon cleanup...");
        
        // Method 1: Destroy weapon container (should take all children with it)
        if (weaponContainer != null && weaponContainer.gameObject != null)
        {
            Debug.Log($"[ShadowClone] Destroying weapon container with {weaponContainer.childCount} children");
            DestroyImmediate(weaponContainer.gameObject);
        }
        
        // Method 2: Manually find and destroy all orbiting weapons that are children
        var orbiters = GetComponentsInChildren<OrbitingWeapon>(true);
        int destroyedOrbiterCount = 0;
        
        foreach (var orbiter in orbiters)
        {
            if (orbiter != null && orbiter.gameObject != this.gameObject)
            {
                DestroyImmediate(orbiter.gameObject);
                destroyedOrbiterCount++;
            }
        }
        
        Debug.Log($"[ShadowClone] Destroyed {destroyedOrbiterCount} orbiting weapons");
        
        // Method 3: Clean up auras
        var auras = GetComponentsInChildren<AuraWeapon>(true);
        int destroyedAuraCount = 0;
        foreach (var aura in auras)
        {
            if (aura != null && aura.gameObject != this.gameObject)
            {
                DestroyImmediate(aura.gameObject);
                destroyedAuraCount++;
            }
        }
        
        Debug.Log($"[ShadowClone] Destroyed {destroyedAuraCount} auras");
        
        // Method 4: Clean up weapon controllers
        var weaponControllers = GetComponentsInChildren<WeaponController>(true);
        int destroyedWeaponCount = 0;
        foreach (var weapon in weaponControllers)
        {
            if (weapon != null && weapon.gameObject != this.gameObject)
            {
                DestroyImmediate(weapon.gameObject);
                destroyedWeaponCount++;
            }
        }
        
        Debug.Log($"[ShadowClone] Destroyed {destroyedWeaponCount} weapon controllers");
        
        Debug.Log("[ShadowClone] Weapon cleanup completed");
    }
}