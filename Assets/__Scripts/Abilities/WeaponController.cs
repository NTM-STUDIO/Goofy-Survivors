using UnityEngine;
using System.Linq; // Used for the 'OrderByDescending' method.

public class WeaponController : MonoBehaviour
{
    public WeaponData weaponData;

    private float currentCooldown;
    private PlayerStats playerStats;
    private Transform firePoint; // Used for projectiles
    private Transform playerTransform; // Used as the center for orbiting weapons

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
    }

    void Update()
    {
        if (playerStats == null || weaponData == null || playerTransform == null) return;

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
                // Add other archetypes here as you create them (Whip, Aura, etc.)
        }
    }

    private void ActivateOrbitingWeapon()
    {
        float finalDamage = weaponData.damage * playerStats.damageMultiplier;
        int finalAmount = weaponData.amount + playerStats.projectileCount;
        float finalSize = weaponData.area * playerStats.projectileSizeMultiplier;
        float finalSpeed = weaponData.speed * playerStats.projectileSpeedMultiplier;
        float finalDuration = weaponData.duration * playerStats.durationMultiplier;
        float finalKnockback = weaponData.knockback * playerStats.knockbackMultiplier;

        Transform orbitCenter = this.transform;
        float angleStep = 360f / finalAmount;

        for (int i = 0; i < finalAmount; i++)
        {
            float startingAngle = i * angleStep;

            GameObject orbitingWeaponObj = Instantiate(weaponData.weaponPrefab, orbitCenter.position, Quaternion.identity, orbitCenter);
            OrbitingWeapon orbiter = orbitingWeaponObj.GetComponent<OrbitingWeapon>();

            if (orbiter != null)
            {
                orbiter.Initialize(orbitCenter, startingAngle, finalDamage, finalSpeed, finalDuration, finalKnockback, finalSize);
            }
        }
    }

    private void FireProjectile()
    {
        float finalDamage = weaponData.damage * playerStats.damageMultiplier;
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
            SpawnAndInitializeProjectile(target, direction, finalDamage, finalSpeed, finalDuration, finalKnockback, finalSize);
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

            case TargetingStyle.MostGrouped:
                Transform bestTarget = null;
                int maxGroupCount = -1;
                foreach (GameObject potentialTarget in enemies)
                {
                    int currentGroupCount = 0;
                    foreach (GameObject otherEnemy in enemies)
                    {
                        if (potentialTarget == otherEnemy) continue;
                        if (Vector3.Distance(potentialTarget.transform.position, otherEnemy.transform.position) <= weaponData.groupingRange)
                        {
                            currentGroupCount++;
                        }
                    }
                    if (currentGroupCount > maxGroupCount)
                    {
                        maxGroupCount = currentGroupCount;
                        bestTarget = potentialTarget.transform;
                    }
                }
                for (int i = 0; i < amount; i++) targets[i] = bestTarget;
                return targets;

            case TargetingStyle.Mixed:
                System.Array.Sort(enemies, (a, b) => Vector3.Distance(firePoint.position, a.transform.position).CompareTo(Vector3.Distance(firePoint.position, b.transform.position)));
                for (int i = 0; i < amount; i++) targets[i] = (i < enemies.Length) ? enemies[i].transform : null;
                return targets;
        }
        return targets;
    }

    private void SpawnAndInitializeProjectile(Transform target, Vector3 direction, float damage, float speed, float duration, float knockback, float size)
    {
        GameObject projectileObj = Instantiate(weaponData.weaponPrefab, firePoint.position, Quaternion.identity);
        ProjectileWeapon projectile = projectileObj.GetComponent<ProjectileWeapon>();
        if (projectile != null)
        {
            projectile.Initialize(target, direction, damage, speed, duration, knockback, size);
        }
    }
}