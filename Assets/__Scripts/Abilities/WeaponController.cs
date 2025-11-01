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
        if (WeaponData.archetype == WeaponArchetype.Aura || WeaponData.archetype == WeaponArchetype.Shield) return;

        currentCooldown -= Time.deltaTime;
        if (currentCooldown <= 0f)
        {
            Attack();
            currentCooldown = WeaponData.cooldown / Mathf.Max(0.01f, playerStats.attackSpeedMultiplier);
        }
    }

    // --- All other methods are unchanged and complete ---
    #region Unchanged Full Code
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
            FireLocally();
        }
    }

    private void SpawnShadowClone()
    {
        if (WeaponData == null || WeaponData.weaponPrefab == null)
        {
            Debug.LogError("[ShadowClone] WeaponData or prefab is missing.");
            return;
        }
        if (weaponManager == null)
        {
            Debug.LogError("[ShadowClone] weaponManager reference is missing on WeaponController.");
            return;
        }
        if (weaponRegistry == null)
        {
            Debug.LogError("[ShadowClone] WeaponRegistry reference is missing on WeaponController. Assign it on PlayerWeaponManager and pass into Initialize().");
            return;
        }

        // In multiplayer, ask the server to spawn a networked clone and wire server-authoritative aura damage.
        if (GameManager.Instance != null && GameManager.Instance.isP2P)
        {
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

        // Single-player fallback: local only
        GameObject cloneObj = Instantiate(WeaponData.weaponPrefab, playerStats.transform.position, playerStats.transform.rotation);
        ShadowClone cloneScript = cloneObj.GetComponent<ShadowClone>();
        if (cloneScript != null)
        {
            // Do not allow the shadow clone to fire projectiles: filter them out
            List<WeaponData> weaponsToClone = weaponManager
                .GetOwnedWeapons()
                .Where(w => w.archetype != WeaponArchetype.ShadowCloneJutsu && w.archetype != WeaponArchetype.Projectile)
                .ToList();
            cloneScript.Initialize(weaponsToClone, playerStats, weaponRegistry);
        }
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
            foreach (var target in targets)
            {
                Vector3 direction = (target != null) ? (target.position - playerStats.transform.position).normalized : new Vector3(Random.insideUnitCircle.normalized.x, 0, Random.insideUnitCircle.normalized.y);
                direction.y = 0;
                Vector3 spawnPosWithOffset = playerStats.transform.position + Vector3.up * 3f;
                GameObject projectileObj = Instantiate(WeaponData.weaponPrefab, spawnPosWithOffset, Quaternion.LookRotation(direction));
                var projectile = projectileObj.GetComponent<ProjectileWeapon>();
                if (projectile != null)
                {
                    projectile.Initialize(target, direction, damageResult.damage, damageResult.isCritical, finalSpeed, finalDuration, finalKnockback, finalSize);
                }
            }
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
        if (WeaponData.weaponPrefab == null || weaponManager == null) return;
        GameObject auraObj = Instantiate(WeaponData.weaponPrefab, weaponManager.transform.position, Quaternion.identity, weaponManager.transform);
        AuraWeapon aura = auraObj.GetComponent<AuraWeapon>();
        if (aura != null) { aura.Initialize(playerStats, WeaponData); }

        // In multiplayer, also notify the server to create the authoritative aura proxy
        weaponManager.NotifyAuraActivated(weaponId);
    }

    public int GetWeaponId() { return this.weaponId; }
    #endregion
}