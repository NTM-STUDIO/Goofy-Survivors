using UnityEngine;

/// <summary>
/// This component is attached to a GameObject managed by the PlayerWeaponManager.
/// It reads the assigned WeaponData and executes the weapon's behavior.
/// It uses PlayerStats to modify its behavior by calculating final stats and passing them to the spawned weapon.
/// </summary>
public class WeaponController : MonoBehaviour
{
    public WeaponData weaponData;
    private float currentCooldown;
    private PlayerStats playerStats;

    void Start()
    {
        // Find the PlayerStats component in the scene. A more robust way is to have it assigned directly.
        #if UNITY_2023_1_OR_NEWER
        playerStats = FindFirstObjectByType<PlayerStats>();
        #else
        #pragma warning disable 618
        playerStats = FindObjectOfType<PlayerStats>();
        #pragma warning restore 618
        #endif

        if (playerStats == null)
        {
            Debug.LogError("WeaponController could not find PlayerStats in the scene!");
        }

        // Set initial cooldown to fire immediately.
        currentCooldown = 0f;
    }

    void Update()
    {
        // Safety check in case PlayerStats or WeaponData aren't set.
        if (playerStats == null || weaponData == null) return;
        
        currentCooldown -= Time.deltaTime;

        if (currentCooldown <= 0f)
        {
            Attack();
            // Calculate cooldown using the player's attack speed multiplier.
            // Higher attack speed = lower cooldown.
            float finalAttackSpeed = playerStats.attackSpeedMultiplier;
            currentCooldown = weaponData.cooldown / Mathf.Max(0.01f, finalAttackSpeed);
            
            // Cap attackspeed to be duration of cooldown at minimum
            // This prevents absurdly high attackspeeds from breaking the game.
            currentCooldown = Mathf.Max(currentCooldown, weaponData.cooldown);
        }
    }

    private void Attack()
    {
        switch (weaponData.archetype)
        {
            case WeaponArchetype.Projectile:
                FireProjectile(); // You would apply the same logic here
                break;
            case WeaponArchetype.Whip:
                // PerformWhipAttack(); // And here
                break;
            case WeaponArchetype.Orbit:
                ActivateOrbitingWeapon();
                break;
            case WeaponArchetype.Aura:
                // ActivateAura(); // And here
                break;
        }
    }
    
    // NEW METHOD FOR PROJECTILES
    private void FireProjectile()
    {
        // === STEP 1: CALCULATE ALL FINAL STATS FROM PLAYERSTATS ===
        float finalDamage = weaponData.damage * playerStats.damageMultiplier;
        int finalAmount = weaponData.amount + playerStats.projectileCount;
        float finalSize = weaponData.area * playerStats.projectileSizeMultiplier;
        float finalSpeed = weaponData.speed * playerStats.projectileSpeedMultiplier;
        float finalDuration = weaponData.duration * playerStats.durationMultiplier;
        float finalKnockback = weaponData.knockback * playerStats.knockbackMultiplier;
        int finalPierce = weaponData.pierce ? weaponData.pierceCount + playerStats.pierceCount : 1; // Default to 1 hit if pierce is false


        // === STEP 2: FIND TARGETS AND FIRE ===
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        System.Array.Sort(enemies, (a, b) => 
            Vector3.Distance(transform.position, a.transform.position)
            .CompareTo(Vector3.Distance(transform.position, b.transform.position))
        );

        int targetsToFireAt = Mathf.Min(finalAmount, enemies.Length);

        if (targetsToFireAt > 0)
        {
            for (int i = 0; i < targetsToFireAt; i++)
            {
                Vector2 direction = (enemies[i].transform.position - transform.position).normalized;
                SpawnAndInitializeProjectile(direction, finalDamage, finalSpeed, finalDuration, finalKnockback, finalPierce, finalSize);
            }
        }
        else if (finalAmount > 0) // Fallback: If no enemies are present, fire in a random direction.
        {
            for (int i = 0; i < finalAmount; i++)
            {
                Vector2 randomDirection = Random.insideUnitCircle.normalized;
                SpawnAndInitializeProjectile(randomDirection, finalDamage, finalSpeed, finalDuration, finalKnockback, finalPierce, finalSize);
            }
        }
    }

    // NEW HELPER METHOD
    private void SpawnAndInitializeProjectile(Vector2 direction, float finalDamage, float finalSpeed, float finalDuration, float finalKnockback, int finalPierce, float finalSize)
    {
        GameObject projectileObj = Instantiate(weaponData.weaponPrefab, transform.position, Quaternion.identity);
        ProjectileWeapon projectile = projectileObj.GetComponent<ProjectileWeapon>();

        if (projectile != null)
        {
            projectile.Initialize(
                direction,
                finalDamage,
                finalSpeed,
                finalDuration,
                finalKnockback,
                finalPierce,
                finalSize
            );
        }
    }

    private void ActivateOrbitingWeapon()
    {
        // === STEP 1: CALCULATE ALL FINAL STATS FROM PLAYERSTATS ===
        float finalDamage = weaponData.damage * playerStats.damageMultiplier;
        int finalAmount = weaponData.amount + playerStats.projectileCount;
        float finalSize = weaponData.area * playerStats.projectileSizeMultiplier;
        float finalSpeed = weaponData.speed * playerStats.projectileSpeedMultiplier;
        float finalDuration = weaponData.duration * playerStats.durationMultiplier;
        float finalKnockback = weaponData.knockback * playerStats.knockbackMultiplier;
        
        // === STEP 2: SPAWN THE WEAPONS AND PASS THE STATS ===
        Transform orbitCenter = transform.parent; // Assumes the weapon manager is the parent
        float angleStep = 360f / finalAmount;
        float randomGroupRotation = Random.Range(0f, 360f);

        for (int i = 0; i < finalAmount; i++)
        {
            float startingAngle = randomGroupRotation + (i * angleStep);
            
            GameObject orbitingWeaponObj = Instantiate(weaponData.weaponPrefab, orbitCenter.position, Quaternion.identity, orbitCenter);

            OrbitingWeapon orbiter = orbitingWeaponObj.GetComponent<OrbitingWeapon>();
            if (orbiter != null)
            {
                orbiter.Initialize(
                    orbitCenter,
                    startingAngle,
                    finalDamage,
                    finalSpeed,
                    finalDuration,
                    finalKnockback,
                    finalSize
                );
            }
        }
    }
}