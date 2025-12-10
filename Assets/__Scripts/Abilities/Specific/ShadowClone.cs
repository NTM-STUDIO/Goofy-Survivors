using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Unity.Netcode;
using System.Collections;

public class ShadowClone : MonoBehaviour
{
    [Header("Clone Stats")]
    [SerializeField] private float lifetime = 5f;
    [SerializeField] private float health = 1f;
    [Tooltip("If true, the clone will act more like a real player: keep PlayerStats active, spawn projectiles, and be targetable.")]
    [SerializeField] private bool behaveLikePlayer = true;

    [Header("Internal References")]
    [Tooltip("An empty child object to organize the clone's weapons.")]
    [SerializeField] private Transform weaponContainer;

    private PlayerStats ownerStats;
    private WeaponRegistry registry;

    void Awake()
    {
        // Disable components early to prevent them from running even for a frame
        DisablePlayerComponents();

        if (weaponContainer == null)
        {
            weaponContainer = new GameObject("WeaponContainer").transform;
            weaponContainer.SetParent(this.transform);
            weaponContainer.localPosition = Vector3.zero;
        }
    }

    void Start()
    {
        Debug.Log($"[ShadowClone] Created with Lifetime: {lifetime}s. Will self-destruct at {Time.time + lifetime}");
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
        
        // PlayerStats: if we want the clone to behave like a player, keep it enabled
        var stats = GetComponent<PlayerStats>();
        if (stats != null)
        {
            if (!behaveLikePlayer)
            {
                stats.enabled = false;
                Debug.Log("[ShadowClone] Disabled PlayerStats component");
            }
            else
            {
                // Ensure colliders/visuals are enabled for a player-like clone
                stats.ClientSyncHp(stats.CurrentHp, stats.maxHp);
                Debug.Log("[ShadowClone] Keeping PlayerStats active for player-like behavior");
            }
        }
        
        // Disable any NetworkBehaviour components (clone is local-only in SP)
        var networkBehaviours = GetComponents<Unity.Netcode.NetworkBehaviour>();
        foreach (var nb in networkBehaviours)
        {
            if (nb == null) continue;
            // If we want the clone to behave like a player, keep its PlayerStats NetworkBehaviour enabled
            if (behaveLikePlayer && nb is PlayerStats) continue;
            nb.enabled = false;
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

        // Fallback: se não foram passadas armas, tenta obter do PlayerWeaponManager do owner
        if ((playerWeapons == null || playerWeapons.Count == 0) && ownerStats != null)
        {
            var pwm = ownerStats.GetComponent<PlayerWeaponManager>();
            if (pwm != null)
            {
                playerWeapons = pwm.GetOwnedWeapons()
                    .Where(w => w.archetype != WeaponArchetype.ShadowCloneJutsu && w.archetype != WeaponArchetype.Projectile)
                    .ToList();

                Debug.Log($"[ShadowClone] Fallback: obtained {playerWeapons.Count} weapons from owner PlayerWeaponManager");
            }
        }

        // Re-enable physics so the clone can be moved by AI
        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.linearVelocity = Vector3.zero;
        }

        // If clone should behave like a player, ensure tag/colliders are set so enemies and systems treat it as a player
        if (behaveLikePlayer)
        {
            try { gameObject.tag = "Player"; } catch { }
            foreach (var c in GetComponentsInChildren<Collider>(true)) c.enabled = true;
        }

        // Attach simple decision-tree AI controller to drive the clone
        var ai = GetComponent<CloneAIController>();
        if (ai == null) ai = gameObject.AddComponent<CloneAIController>();
        // Prefer clone's own PlayerStats if we're behaving like a real player
        var cloneStats = GetComponent<PlayerStats>();
        
        // SYNC STATS: Copy owner's power to clone, but keep Health independent
        if (cloneStats != null && ownerStats != null)
        {
            SyncStatsFromOwner(cloneStats, ownerStats);
        }

        ai.Initialize(cloneStats, ownerStats, this);

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
            // If clone has its own PlayerStats and should behave like a player, use it as the weapon owner
            var statsForWeapons = (behaveLikePlayer && cloneStats != null) ? cloneStats : ownerStats;
            wc.Initialize(weaponId, weaponData, null, statsForWeapons, true, registry);

            // Handle specific weapon types
            HandleWeaponType(weaponData, weaponControllerObj);
        }

        // Subscribe to weapon updates
        if (ownerStats != null)
        {
            var pwm = ownerStats.GetComponent<PlayerWeaponManager>();
            if (pwm != null)
            {
                pwm.OnWeaponAdded += HandleNewWeaponAdded;
            }
        }
    }

    private void HandleNewWeaponAdded(WeaponData newWeapon)
    {
        if (newWeapon == null) return;
        // Avoid duplicates if we already have it (basic check)
        bool alreadyHas = false;
        var existingControllers = weaponContainer.GetComponentsInChildren<WeaponController>();
        foreach(var c in existingControllers)
        {
            if (c.WeaponData == newWeapon) { alreadyHas = true; break; }
        }
        if (alreadyHas) return;

        // Skip ShadowCloneJutsu or Projectiles if not allowed
        if (newWeapon.archetype == WeaponArchetype.ShadowCloneJutsu) return;
        if (newWeapon.archetype == WeaponArchetype.Projectile && !(behaveLikePlayer || ownerStats != null)) return;

        Debug.Log($"[ShadowClone] Acquired new weapon dynamically: {newWeapon.weaponName}");

        GameObject weaponControllerObj = new GameObject(newWeapon.weaponName + " (Clone)");
        weaponControllerObj.transform.SetParent(weaponContainer);
        weaponControllerObj.transform.localPosition = Vector3.zero;

        WeaponController wc = weaponControllerObj.AddComponent<WeaponController>();
        int weaponId = registry.GetWeaponId(newWeapon); // registry is saved in Initialize
            
        var statsForWeapons = (behaveLikePlayer && GetComponent<PlayerStats>() != null) ? GetComponent<PlayerStats>() : ownerStats;
        wc.Initialize(weaponId, newWeapon, null, statsForWeapons, true, registry);

        HandleWeaponType(newWeapon, weaponControllerObj);
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
                // Projectiles require a PlayerStats reference to attribute damage and compute multipliers.
                // If the clone has its own PlayerStats (behaveLikePlayer) we allow them.
                // Otherwise, if we have the owner's stats available, we also allow projectiles but they will
                // be attributed to the owner (so the clone borrows the owner's stats for firing).
                if (behaveLikePlayer && GetComponent<PlayerStats>() != null)
                {
                    Debug.Log($"[ShadowClone] Allowing projectile weapon for player-like clone: {weaponData.weaponName}");
                }
                else if (ownerStats != null)
                {
                    Debug.Log($"[ShadowClone] Allowing projectile weapon using owner stats for clone: {weaponData.weaponName}");
                }
                else
                {
                    Debug.Log($"[ShadowClone] Skipping projectile weapon for clone: {weaponData.weaponName} (no PlayerStats available)");
                    break;
                }
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
        // Unsubscribe
        if (ownerStats != null)
        {
            var pwm = ownerStats.GetComponent<PlayerWeaponManager>();
            if (pwm != null)
            {
                pwm.OnWeaponAdded -= HandleNewWeaponAdded;
            }
        }

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

    private void SyncStatsFromOwner(PlayerStats target, PlayerStats source)
    {
        // Copy combat stats
        target.damageMultiplier = source.damageMultiplier;
        target.critChance = source.critChance;
        target.critDamageMultiplier = source.critDamageMultiplier;
        target.cooldownReduction = source.cooldownReduction;
        target.attackSpeedMultiplier = source.attackSpeedMultiplier;
        target.projectileCount = source.projectileCount;
        target.projectileSizeMultiplier = source.projectileSizeMultiplier;
        target.durationMultiplier = source.durationMultiplier;
        target.knockbackMultiplier = source.knockbackMultiplier;
        
        // Copy movement speed (useful for keeping up)
        target.movementSpeed = source.movementSpeed;
        
        // Other utilities
        target.pickupRange = source.pickupRange;
        target.luck = source.luck;
        target.pierceCount = source.pierceCount;

        // Visuals
        target.IncreaseMaxHP(0); // Força refresh UI local se necessário
        
        Debug.Log($"[ShadowClone] Stats synced from owner. DMG: {target.damageMultiplier}, CDR: {target.cooldownReduction}");
    }

    private IEnumerator DelayedCleanup()
    {
        // Wait one frame to avoid destruction during frame processing
        yield return null;
        CleanupAllWeaponsImmediately();
    }

    private void CleanupAllWeaponsImmediately()
    {
        // Method 1: Destroy weapon container (should take all children with it)
        if (weaponContainer != null && weaponContainer.gameObject != null)
        {
            Destroy(weaponContainer.gameObject);
        }
        
        // Method 2: Manually find and destroy all orbiting weapons that are children
        // Note: Using Destroy instead of DestroyImmediate to allow physics loop to finish safe
        var orbiters = GetComponentsInChildren<OrbitingWeapon>(true);
        foreach (var orbiter in orbiters)
        {
            if (orbiter != null && orbiter.gameObject != this.gameObject)
            {
                Destroy(orbiter.gameObject);
            }
        }
        
        // Method 3: Clean up auras
        var auras = GetComponentsInChildren<AuraWeapon>(true);
        foreach (var aura in auras)
        {
            if (aura != null && aura.gameObject != this.gameObject)
            {
                Destroy(aura.gameObject);
            }
        }
        
        // Method 4: Clean up weapon controllers
        var weaponControllers = GetComponentsInChildren<WeaponController>(true);
        foreach (var weapon in weaponControllers)
        {
            if (weapon != null && weapon.gameObject != this.gameObject)
            {
                Destroy(weapon.gameObject);
            }
        }
    }
}