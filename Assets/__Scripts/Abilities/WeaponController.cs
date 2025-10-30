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

    public void Initialize(int id, WeaponData data, PlayerWeaponManager manager, PlayerStats stats, bool owner, WeaponRegistry registry)
    {
        this.weaponId = id;
        this.WeaponData = data;
        this.weaponManager = manager;
        this.playerStats = stats;
        this.isWeaponOwner = owner;
        this.weaponRegistry = registry;

        if (isWeaponOwner && WeaponData.archetype == WeaponArchetype.Aura) { ActivateAura(); }
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
        else if (isWeaponOwner) // This condition is for weapons owned by a Shadow Clone (which have no manager)
        {
            FireLocally();
        }
    }

    /// <summary>
    /// THE FIX: This method now correctly calls the new Initialize method on the ShadowClone script.
    /// </summary>
    private void SpawnShadowClone()
    {
        if (WeaponData.weaponPrefab == null || weaponManager == null || weaponRegistry == null) return;

        GameObject cloneObj = Instantiate(WeaponData.weaponPrefab, playerStats.transform.position, playerStats.transform.rotation);
        ShadowClone cloneScript = cloneObj.GetComponent<ShadowClone>();

        if (cloneScript != null)
        {
            List<WeaponData> weaponsToClone = weaponManager.GetOwnedWeapons()
                .Where(w => w.archetype != WeaponArchetype.ShadowCloneJutsu)
                .ToList();
            
            // Provide all the necessary data to the new clone.
            cloneScript.Initialize(weaponsToClone, playerStats, weaponRegistry);
        }
    }

    /// <summary>
    /// A local-only fire method used by weapons attached to a Shadow Clone.
    /// </summary>
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
                Vector3 direction = (target != null)
                    ? (target.position - playerStats.transform.position).normalized
                    : new Vector3(Random.insideUnitCircle.normalized.x, 0, Random.insideUnitCircle.normalized.y);
                direction.y = 0;

                GameObject projectileObj = Instantiate(WeaponData.weaponPrefab, playerStats.transform.position, Quaternion.LookRotation(direction));
                var projectile = projectileObj.GetComponent<ProjectileWeapon>();
                if (projectile != null)
                {
                    projectile.Initialize(target, direction, damageResult.damage, damageResult.isCritical, finalSpeed, finalDuration, finalKnockback, finalSize);
                }
            }
        }
        // Add local fire logic for other archetypes if clones can use them (e.g., Orbit)
    }

    #region Unchanged Helper Methods
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
    }

    public int GetWeaponId() { return this.weaponId; }
    #endregion
}