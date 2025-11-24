using UnityEngine;
using System.Linq;
using System.Collections.Generic;

public class WeaponController : MonoBehaviour
{
    public WeaponData WeaponData { get; private set; }

    private int weaponId;
    private PlayerWeaponManager weaponManager;
    private PlayerStats playerStats;
    private WeaponRegistry weaponRegistry;
    private bool isWeaponOwner;
    private float currentCooldown;

    void Awake()
    {
        // This log tells us the moment the GameObject is created.
        Debug.Log($"<color=blue>[WC-AWAKE]</color> WeaponController for '{this.gameObject.name}' has been created in the scene.");
    }

    void Start()
    {
        // This log tells us the frame before its first Update.
        Debug.Log($"<color=blue>[WC-START]</color> WeaponController for '{this.gameObject.name}' is starting.");
    }

    public void Initialize(int id, WeaponData data, PlayerWeaponManager manager, PlayerStats stats, bool owner, WeaponRegistry registry)
    {
        // Guard against null WeaponData (typically means WeaponRegistry mismatch on this client)
        if (data == null)
        {
            Debug.LogError($"[WC-INIT] Null WeaponData for id {id}. Ensure WeaponRegistry contains this weapon on all clients and the field is assigned.");
            return;
        }

        // This is the most important log. It confirms the manager has passed the data.
        Debug.Log($"<color=teal>[WC-INIT]</color> WeaponController is being INITIALIZED with data for '{data.name}'. IsOwner: {owner}");

        this.weaponId = id;
        this.WeaponData = data;
        this.weaponManager = manager;
        this.playerStats = stats;
        this.isWeaponOwner = owner;
        this.weaponRegistry = registry;

        if (isWeaponOwner)
        {
            if (WeaponData.archetype == WeaponArchetype.Aura)
            {
                ActivateAura();
            }
            else if (WeaponData.archetype == WeaponArchetype.Shield)
            {
                if (weaponManager != null) weaponManager.NotifyShieldActivated(weaponId);
            }
        }
    }

    void Update()
    {
        if (!isWeaponOwner || WeaponData == null || playerStats == null) return;
        // Do not auto-cast when the player is downed
        if (playerStats.IsDowned) return;
        if (WeaponData.archetype == WeaponArchetype.Aura || WeaponData.archetype == WeaponArchetype.Shield) return;

        currentCooldown -= Time.deltaTime;
        if (currentCooldown <= 0f)
        {
            Attack();
            currentCooldown = WeaponData.cooldown / Mathf.Max(0.01f, playerStats.attackSpeedMultiplier);
        }
    }

    private void Attack()
    {
        if (WeaponData.archetype == WeaponArchetype.ShadowCloneJutsu)
        {
            SpawnShadowClone();
            return;
        }

        if (weaponManager != null)
        {
            int finalAmount = WeaponData.amount + playerStats.projectileCount;
            Transform[] targets = GetTargets(finalAmount);
            weaponManager.PerformAttack(weaponId, targets);
        }
        else if (isWeaponOwner)
        {
            // Clone-local firing (single-player clone path)
            // Clones should only copy Auras (handled on init) and Orbiting weapons; no projectiles
            if (WeaponData.archetype == WeaponArchetype.Orbit)
            {
                SpawnOrbitingWeaponsAroundSelf();
            }
        }
    }

    private void SpawnShadowClone()
    {
        if (WeaponData == null || WeaponData.weaponPrefab == null)
        {
            Debug.LogError("[ShadowClone] WeaponData or prefab is missing.");
            return;
        }

        if (weaponRegistry == null)
        {
            Debug.LogError("[ShadowClone] WeaponRegistry reference is missing on WeaponController. Assign it on PlayerWeaponManager and pass into Initialize().");
            return;
        }

        // Get player stats from weapon manager or directly
        PlayerStats stats = weaponManager != null ? weaponManager.GetComponent<PlayerStats>() : GetComponentInParent<PlayerStats>();
        if (stats == null)
        {
            Debug.LogError("[ShadowClone] Could not find PlayerStats!");
            return;
        }

        // In multiplayer, ask the server to spawn a networked clone
        if (GameManager.Instance != null && GameManager.Instance.isP2P)
        {
            if (weaponManager == null)
            {
                Debug.LogError("[ShadowClone] weaponManager reference is missing for multiplayer.");
                return;
            }

            // Filter out projectiles and the clone weapon itself
            List<WeaponData> owned = weaponManager.GetOwnedWeapons();
            if (owned == null)
            {
                Debug.LogWarning("[ShadowClone] No owned weapons list returned.");
                return;
            }

            List<int> weaponIdsToClone = owned
                .Where(w => w.archetype != WeaponArchetype.ShadowCloneJutsu && w.archetype != WeaponArchetype.Projectile)
                .Select(w => weaponRegistry.GetWeaponId(w))
                .Where(id => id != -1)
                .ToList();

            weaponManager.NotifyShadowCloneActivated(weaponId, weaponIdsToClone.ToArray());
            return;
        }

        // Single-player: local only
        Debug.Log("[ShadowClone] Spawning single-player shadow clone");

        GameObject cloneObj = Instantiate(WeaponData.weaponPrefab, stats.transform.position, stats.transform.rotation);
        ShadowClone cloneScript = cloneObj.GetComponent<ShadowClone>();
        if (cloneScript == null)
        {
            cloneScript = cloneObj.AddComponent<ShadowClone>();
        }

        // Get weapons to clone - filter out projectiles and clone jutsu
        List<WeaponData> weaponsToClone;
        if (weaponManager != null)
        {
            weaponsToClone = weaponManager
                .GetOwnedWeapons()
                .Where(w => w.archetype != WeaponArchetype.ShadowCloneJutsu && w.archetype != WeaponArchetype.Projectile)
                .ToList();
        }
        else
        {
            // Fallback: use current weapon only
            weaponsToClone = new List<WeaponData> { WeaponData };
        }

        cloneScript.Initialize(weaponsToClone, stats, weaponRegistry);
    }

    private void FireLocally()
    {
        if (WeaponData.archetype == WeaponArchetype.Projectile)
        {
            int finalAmount = WeaponData.amount + playerStats.projectileCount;
            Transform[] targets = GetTargets(finalAmount);
            DamageResult damageResult = playerStats.CalculateDamage(WeaponData.damage);
            float finalSpeed = WeaponData.speed * playerStats.projectileSpeedMultiplier;
            float finalDuration = WeaponData.duration * playerStats.durationMultiplier;
            float finalKnockback = WeaponData.knockback * playerStats.knockbackMultiplier;
            float finalSize = WeaponData.area * playerStats.projectileSizeMultiplier;
            int weaponId = weaponRegistry != null ? weaponRegistry.GetWeaponId(WeaponData) : -1;
            foreach (var target in targets)
            {
                Vector3 direction = (target != null) ? (target.position - playerStats.transform.position).normalized : new Vector3(Random.insideUnitCircle.normalized.x, 0, Random.insideUnitCircle.normalized.y);
                direction.y = 0;
                Vector3 spawnPosWithOffset = playerStats.transform.position + Vector3.up * 2f;
                // Keep root rotation unchanged; visuals rotate Z-only inside ProjectileWeapon
                GameObject projectileObj = Instantiate(WeaponData.weaponPrefab, spawnPosWithOffset, Quaternion.identity);
                var projectile = projectileObj.GetComponent<ProjectileWeapon>();
                if (projectile != null)
                {
                    projectile.ConfigureSource(weaponId, WeaponData.weaponName);
                    projectile.Initialize(target, direction, damageResult.damage, damageResult.isCritical, finalSpeed, finalDuration, finalKnockback, finalSize);
                }
            }
        }
    }

    private void SpawnOrbitingWeaponsAroundSelf()
    {
        int finalAmount = WeaponData.amount + playerStats.projectileCount;
        float angleStep = 360f / Mathf.Max(1, finalAmount);

        // Get the ShadowClone parent (if this is a clone's weapon)
        ShadowClone parentClone = GetComponentInParent<ShadowClone>();

        for (int i = 0; i < finalAmount; i++)
        {
            GameObject orbitingWeaponObj = Instantiate(WeaponData.weaponPrefab, transform.position, Quaternion.identity);
            
            // CRITICAL: Add tracker to auto-destroy when clone is destroyed
            if (parentClone != null)
            {
                var tracker = orbitingWeaponObj.AddComponent<CloneWeaponTracker>();
                tracker.SetParentClone(parentClone);
            }
            
            // Parent to this weapon controller
            orbitingWeaponObj.transform.SetParent(transform, false);
            
            var orbiter = orbitingWeaponObj.GetComponent<OrbitingWeapon>();
            if (orbiter != null)
            {
                orbiter.LocalInitialize(transform, i * angleStep, playerStats, WeaponData);
            }
            
            Debug.Log($"[WeaponController] Spawned orbiting weapon for clone, added tracker: {orbitingWeaponObj.name}");
        }
    }

    private Transform[] GetTargets(int amount)
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        Transform[] targets = new Transform[amount];
        if (enemies.Length == 0) return targets;
        switch (WeaponData.targetingStyle)
        {
            case TargetingStyle.Closest:
                var sortedEnemies = enemies.OrderBy(e => Vector3.Distance(transform.position, e.transform.position)).ToArray();
                for (int i = 0; i < amount && i < sortedEnemies.Length; i++) { targets[i] = sortedEnemies[i].transform; }
                break;
            case TargetingStyle.Random:
                break;
            case TargetingStyle.Strongest:
                var strongestEnemies = enemies.OrderByDescending(e => e.GetComponent<EnemyStats>()?.CurrentHealth ?? 0).ToArray();
                for (int i = 0; i < amount && i < strongestEnemies.Length; i++) { targets[i] = strongestEnemies[i].transform; }
                break;
        }
        return targets;
    }

    private void ActivateAura()
    {
        if (WeaponData.weaponPrefab == null) return;
        bool isP2P = GameManager.Instance != null && GameManager.Instance.isP2P;

        if (!isP2P)
        {
            // Single-player: local-only aura instance
            Transform parent = weaponManager != null ? weaponManager.transform : (transform.parent != null ? transform.parent : transform);
            GameObject auraObj = Instantiate(WeaponData.weaponPrefab, parent.position, Quaternion.identity, parent);
            AuraWeapon aura = auraObj.GetComponent<AuraWeapon>();
            if (aura != null) { aura.Initialize(playerStats, WeaponData); }
        }

        // In multiplayer, request server to spawn the networked aura
        if (isP2P && weaponManager != null)
        {
            weaponManager.NotifyAuraActivated(weaponId);
        }
    }

    public int GetWeaponId() { return this.weaponId; }

    private void OnDestroy()
    {
        Debug.Log($"[WeaponController] Destroying weapon: {WeaponData?.weaponName}");
        
        // If this is a shadow clone's weapon, make sure to clean up any spawned objects
        if (weaponManager == null) // This indicates it's a clone's weapon
        {
            // Clean up any orbiting weapons
            var orbiters = GetComponentsInChildren<OrbitingWeapon>(true);
            foreach (var orbiter in orbiters)
            {
                if (orbiter != null && orbiter.gameObject != this.gameObject)
                {
                    Destroy(orbiter.gameObject);
                }
            }
            
            // Clean up any aura weapons
            var auras = GetComponentsInChildren<AuraWeapon>(true);
            foreach (var aura in auras)
            {
                if (aura != null && aura.gameObject != this.gameObject)
                {
                    Destroy(aura.gameObject);
                }
            }
        }
    }
}