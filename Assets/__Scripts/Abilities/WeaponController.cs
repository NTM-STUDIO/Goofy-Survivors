using UnityEngine;

public class WeaponController : MonoBehaviour
{
    public WeaponData weaponData;

    private float currentCooldown;
    private PlayerStats playerStats;
    private Transform playerTransform;
    private Transform firePoint;

    void Start()
    {
#if UNITY_2023_1_OR_NEWER
        playerStats = FindFirstObjectByType<PlayerStats>();
#else
#pragma warning disable 618
        playerStats = FindObjectOfType<PlayerStats>();
#pragma warning restore 618
#endif

        if (playerStats == null)
        {
            Debug.LogError("WeaponController could not find a PlayerStats component in the scene!");
        }

        GameObject playerObject = GameObject.FindWithTag("Player");
        if (playerObject != null)
        {
            playerTransform = playerObject.transform;
            firePoint = playerTransform.Find("FirePoint");

            if (firePoint == null)
            {
                Debug.LogWarning("Could not find a 'FirePoint' child on the Player. Defaulting to the Player's main transform.");
                firePoint = playerTransform;
            }
        }
        else
        {
            Debug.LogError("WeaponController could not find a GameObject with the 'Player' tag! Make sure your player prefab is tagged correctly.");
        }

        currentCooldown = 0f;
    }

    void Update()
    {
        if (playerStats == null || weaponData == null || firePoint == null) return;

        currentCooldown -= Time.deltaTime;

        if (currentCooldown <= 0f)
        {
            Attack();

            float finalAttackSpeed = playerStats.attackSpeedMultiplier;
            currentCooldown = weaponData.cooldown / Mathf.Max(0.01f, finalAttackSpeed);
        }
    }

    private void Attack()
    {
        switch (weaponData.archetype)
        {
            case WeaponArchetype.Projectile:
                FireProjectile();
                break;
            case WeaponArchetype.Whip:
                // Add 3D Whip logic here
                break;
            case WeaponArchetype.Orbit:
                ActivateOrbitingWeapon();
                break;
            case WeaponArchetype.Aura:
                // Add 3D Aura logic here
                break;
            case WeaponArchetype.Clone:
                SpawnShadowClone();
                break;
        }
    }

    private void SpawnShadowClone()
    {
        int finalAmount = weaponData.amount + playerStats.projectileCount;
        float finalDuration = weaponData.duration * playerStats.durationMultiplier;
        float finalSize = weaponData.area * playerStats.projectileSizeMultiplier;

        for (int i = 0; i < finalAmount; i++)
        {
            Vector2 randomCirclePos = Random.insideUnitCircle * 3f;
            Vector3 spawnPosition = firePoint.position + new Vector3(randomCirclePos.x, 0, randomCirclePos.y);
            
            GameObject cloneObj = Instantiate(weaponData.weaponPrefab, spawnPosition, Quaternion.identity, playerTransform);

            ShadowClone clone = cloneObj.GetComponent<ShadowClone>();
            if (clone != null)
            {
                clone.Initialize(finalDuration, finalSize);
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
        int finalPierce = weaponData.pierce ? weaponData.pierceCount + playerStats.pierceCount : 1;

        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");

        System.Array.Sort(enemies, (a, b) =>
            Vector3.Distance(firePoint.position, a.transform.position)
            .CompareTo(Vector3.Distance(firePoint.position, b.transform.position))
        );

        int targetsToFireAt = Mathf.Min(finalAmount, enemies.Length);

        for (int i = 0; i < targetsToFireAt; i++)
        {
            Transform target = enemies[i].transform;
            Vector3 direction = (target.position - firePoint.position);
            direction.y = 0;
            direction.Normalize();

            SpawnAndInitializeProjectile(target, direction, finalDamage, finalSpeed, finalDuration, finalKnockback, finalPierce, finalSize);
        }

        int projectilesRemaining = finalAmount - targetsToFireAt;
        for (int i = 0; i < projectilesRemaining; i++)
        {
            Vector2 randomCircleDir = Random.insideUnitCircle.normalized;
            Vector3 randomDirection = new Vector3(randomCircleDir.x, 0, randomCircleDir.y);
            SpawnAndInitializeProjectile(null, randomDirection, finalDamage, finalSpeed, finalDuration, finalKnockback, finalPierce, finalSize);
        }
    }
    
    private void SpawnAndInitializeProjectile(Transform target, Vector3 direction, float finalDamage, float finalSpeed, float finalDuration, float finalKnockback, int finalPierce, float finalSize)
    {
        GameObject projectileObj = Instantiate(weaponData.weaponPrefab, firePoint.position, Quaternion.identity);
        ProjectileWeapon projectile = projectileObj.GetComponent<ProjectileWeapon>();

        if (projectile != null)
        {
            // You will need to make sure your ProjectileWeapon's Initialize method also accepts a Vector3
            projectile.Initialize(target, direction, finalDamage, finalSpeed, finalDuration, finalKnockback, finalPierce, finalSize);
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

        // --- THIS IS THE FIXED LINE ---
        // The WeaponController's game object is parented to the "weaponParent" transform,
        // so we can access it using transform.parent.
        Transform orbitCenter = transform.parent;
        // -----------------------------

        float angleStep = 360f / finalAmount;
        float randomGroupRotation = Random.Range(0f, 360f);

        for (int i = 0; i < finalAmount; i++)
        {
            float startingAngle = randomGroupRotation + (i * angleStep);
            GameObject orbitingWeaponObj = Instantiate(weaponData.weaponPrefab, orbitCenter.position, Quaternion.identity, orbitCenter);
            OrbitingWeapon orbiter = orbitingWeaponObj.GetComponent<OrbitingWeapon>();
            if (orbiter != null)
            {
                orbiter.Initialize(orbitCenter, startingAngle, finalDamage, finalSpeed, finalDuration, finalKnockback, finalSize);
            }
        }
    }
}