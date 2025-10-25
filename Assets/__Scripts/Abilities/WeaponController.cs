using UnityEngine;
using System.Linq;

public class WeaponController : MonoBehaviour
{
    public WeaponData weaponData;

    private float currentCooldown;
    private PlayerStats playerStats;
    private Transform firePoint;
    private Transform playerTransform;

    void Start()
    {
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;
        playerStats = FindFirstObjectByType<PlayerStats>();
        GameObject playerObject = GameObject.FindWithTag("Player");
        if (playerObject != null)
        {
            playerTransform = playerObject.transform;
            firePoint = playerTransform.Find("Visuals/FirePoint");
            if (firePoint == null) firePoint = playerTransform;
        }
        currentCooldown = 0f;
        
        if (weaponData.archetype == WeaponArchetype.Aura)
        {
            ActivateAura();
        }
    }

    void Update()
    {
        if (playerStats == null || weaponData == null || playerTransform == null) return;
        
        if (weaponData.archetype == WeaponArchetype.Aura)
        {
            return;
        }

        currentCooldown -= Time.deltaTime;
        if (currentCooldown <= 0f)
        {
            Attack();
            float finalAttackSpeed = playerStats.attackSpeedMultiplier;
            float cooldownBasedOnSpeed = weaponData.cooldown / Mathf.Max(0.01f, finalAttackSpeed);
            float finalDuration = weaponData.duration * playerStats.durationMultiplier;
            currentCooldown = Mathf.Max(cooldownBasedOnSpeed, finalDuration);
        }
    }

    private void Attack()
    {
        switch (weaponData.archetype)
        {
            case WeaponArchetype.Projectile:
                FireProjectile();
                break;
            case WeaponArchetype.Orbit:
                ActivateOrbitingWeapon();
                break;
        }
    }

    private void ActivateAura()
    {
        GameObject auraObj = Instantiate(weaponData.weaponPrefab, transform.position, Quaternion.identity, this.transform);
        AuraWeapon aura = auraObj.GetComponent<AuraWeapon>();
        if (aura != null)
        {
            aura.Initialize(playerStats, weaponData);
        }
    }

    #region Other Attack Methods
    // --- METHOD MODIFIED ---
    private void ActivateOrbitingWeapon()
    {
        int finalAmount = weaponData.amount + playerStats.projectileCount;
        float finalSize = weaponData.area * playerStats.projectileSizeMultiplier;
        float finalSpeed = weaponData.speed * playerStats.projectileSpeedMultiplier;
        float finalDuration = weaponData.duration * playerStats.durationMultiplier;
        float finalKnockback = weaponData.knockback * playerStats.knockbackMultiplier;

        Transform orbitCenter = this.transform;
        float angleStep = 360f / finalAmount;

        for (int i = 0; i < finalAmount; i++)
        {
            // Damage is no longer calculated here.

            float startingAngle = i * angleStep;
            GameObject orbitingWeaponObj = Instantiate(weaponData.weaponPrefab, orbitCenter.position, Quaternion.identity, orbitCenter);
            OrbitingWeapon orbiter = orbitingWeaponObj.GetComponent<OrbitingWeapon>();
            if (orbiter != null)
            {
                // Give the orbiter the references it needs to calculate its own damage on hit.
                orbiter.Initialize(orbitCenter, startingAngle, playerStats, weaponData, finalSpeed, finalDuration, finalKnockback, finalSize);
            }
        }
    }

    private void FireProjectile()
    {
        int finalAmount = weaponData.amount + playerStats.projectileCount;
        float finalSize = weaponData.area * playerStats.projectileSizeMultiplier;
        float finalSpeed = weaponData.speed * playerStats.projectileSpeedMultiplier;
        float finalDuration = weaponData.duration * playerStats.durationMultiplier;
        float finalKnockback = weaponData.knockback * playerStats.knockbackMultiplier;

        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        if (enemies.Length == 0 && weaponData.targetingStyle != TargetingStyle.Random) return;

        Transform[] targets = GetTargets(enemies, finalAmount);

        foreach (Transform target in targets)
        {
            // Calculate damage separately for each projectile so they have individual crit chances
            DamageResult damageResult = playerStats.CalculateDamage(weaponData.damage);

            Vector3 direction;
            if (target != null)
            {
                direction = (target.position - firePoint.position).normalized;
                direction.y = 0;
            }
            else
            {
                Vector2 randomCircleDir = Random.insideUnitCircle.normalized;
                direction = new Vector3(randomCircleDir.x, 0, randomCircleDir.y);
            }
            SpawnAndInitializeProjectile(target, direction, damageResult.damage, damageResult.isCritical, finalSpeed, finalDuration, finalKnockback, finalSize);
        }
    }

    private Transform[] GetTargets(GameObject[] enemies, int amount)
    {
        Transform[] targets = new Transform[amount];
        switch (weaponData.targetingStyle)
        {
            case TargetingStyle.Random:
                for (int i = 0; i < amount; i++) targets[i] = null;
                return targets;
            case TargetingStyle.Closest:
                System.Array.Sort(enemies, (a, b) => Vector3.Distance(firePoint.position, a.transform.position).CompareTo(Vector3.Distance(firePoint.position, b.transform.position)));
                for (int i = 0; i < amount; i++) targets[i] = (i < enemies.Length) ? enemies[i].transform : null;
                return targets;
            case TargetingStyle.Strongest:
                IOrderedEnumerable<GameObject> sortedByHealth = enemies.OrderByDescending(e => e.GetComponent<EnemyStats>()?.CurrentHealth ?? 0);
                GameObject[] strongestEnemies = sortedByHealth.ToArray();
                for (int i = 0; i < amount; i++) targets[i] = (i < strongestEnemies.Length) ? strongestEnemies[i].transform : null;
                return targets;
            default:
                return targets;
        }
    }

    private void SpawnAndInitializeProjectile(Transform target, Vector3 direction, float damage, bool isCritical, float speed, float duration, float knockback, float size)
    {
        GameObject projectileObj = Instantiate(weaponData.weaponPrefab, firePoint.position, Quaternion.identity);
        ProjectileWeapon projectile = projectileObj.GetComponent<ProjectileWeapon>();
        if (projectile != null)
        {
            projectile.Initialize(target, direction, damage, isCritical, speed, duration, knockback, size);
        }
    }
    #endregion
}