using UnityEngine;

public class WeaponController : MonoBehaviour
{
    public WeaponData weaponData;

    private float currentCooldown;
    private PlayerStats playerStats;
    private Transform playerTransform;
    private Transform firePoint;

<<<<<<< Updated upstream
    void Start()
    {
#if UNITY_2023_1_OR_NEWER
        playerStats = FindFirstObjectByType<PlayerStats>();
#else
#pragma warning disable 618
        playerStats = FindObjectOfType<PlayerStats>();
#pragma warning restore 618
#endif
=======
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
        // This is the most important log. It confirms the manager has passed the data.
        Debug.Log($"<color=teal>[WC-INIT]</color> WeaponController is being INITIALIZED with data for '{data.name}'. IsOwner: {owner}");
        
        this.weaponId = id;
        this.WeaponData = data;
        this.weaponManager = manager;
        this.playerStats = stats;
        this.isWeaponOwner = owner;
        this.weaponRegistry = registry;
>>>>>>> Stashed changes

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
            currentCooldown = Mathf.Max(currentCooldown, weaponData.cooldown);
        }
    }

    // --- All other methods are unchanged and complete ---
    #region Unchanged Full Code
    private void Attack()
    {
        switch (weaponData.archetype)
        {
<<<<<<< Updated upstream
            case WeaponArchetype.Projectile:
                FireProjectile();
                break;
            case WeaponArchetype.Whip:
                break;
            case WeaponArchetype.Orbit:
                ActivateOrbitingWeapon();
                break;
            case WeaponArchetype.Aura:
                break;
            case WeaponArchetype.Clone:
                SpawnShadowClone();
                break;
=======
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
>>>>>>> Stashed changes
        }
    }

    private void SpawnShadowClone()
    {
<<<<<<< Updated upstream
        int finalAmount = weaponData.amount + playerStats.projectileCount;
        float finalDuration = weaponData.duration * playerStats.durationMultiplier;
        float finalSize = weaponData.area * playerStats.projectileSizeMultiplier;

        for (int i = 0; i < finalAmount; i++)
        {
            Vector2 spawnPosition = (Vector2)firePoint.position + Random.insideUnitCircle * 3f;
            GameObject cloneObj = Instantiate(weaponData.weaponPrefab, spawnPosition, Quaternion.identity, playerTransform);

            ShadowClone clone = cloneObj.GetComponent<ShadowClone>();
            if (clone != null)
            {
                clone.Initialize(finalDuration, finalSize);
            }
        }
    }

    private void FireProjectile()
=======
        if (WeaponData.weaponPrefab == null || weaponManager == null || weaponRegistry == null) return;
        GameObject cloneObj = Instantiate(WeaponData.weaponPrefab, playerStats.transform.position, playerStats.transform.rotation);
        ShadowClone cloneScript = cloneObj.GetComponent<ShadowClone>();
        if (cloneScript != null)
        {
            List<WeaponData> weaponsToClone = weaponManager.GetOwnedWeapons().Where(w => w.archetype != WeaponArchetype.ShadowCloneJutsu).ToList();
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
                GameObject projectileObj = Instantiate(WeaponData.weaponPrefab, playerStats.transform.position, Quaternion.LookRotation(direction));
                var projectile = projectileObj.GetComponent<ProjectileWeapon>();
                if (projectile != null)
                {
                    projectile.Initialize(target, direction, damageResult.damage, damageResult.isCritical, finalSpeed, finalDuration, finalKnockback, finalSize);
                }
            }
        }
    }
    
    private Transform[] GetTargets(int amount)
>>>>>>> Stashed changes
    {
        float finalDamage = weaponData.damage * playerStats.damageMultiplier;
        int finalAmount = weaponData.amount + playerStats.projectileCount;
        float finalSize = weaponData.area * playerStats.projectileSizeMultiplier;
        float finalSpeed = weaponData.speed * playerStats.projectileSpeedMultiplier;
        float finalDuration = weaponData.duration * playerStats.durationMultiplier;
        float finalKnockback = weaponData.knockback * playerStats.knockbackMultiplier;
        int finalPierce = weaponData.pierce ? weaponData.pierceCount + playerStats.pierceCount : 1;

        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
<<<<<<< Updated upstream

        System.Array.Sort(enemies, (a, b) =>
            Vector3.Distance(firePoint.position, a.transform.position)
            .CompareTo(Vector3.Distance(firePoint.position, b.transform.position))
        );

        int targetsToFireAt = Mathf.Min(finalAmount, enemies.Length);

        for (int i = 0; i < targetsToFireAt; i++)
=======
        Transform[] targets = new Transform[amount];
        if (enemies.Length == 0) return targets;
        switch (WeaponData.targetingStyle)
>>>>>>> Stashed changes
        {
            Transform target = enemies[i].transform;
            Vector2 direction = (target.position - firePoint.position).normalized;
            SpawnAndInitializeProjectile(target, direction, finalDamage, finalSpeed, finalDuration, finalKnockback, finalPierce, finalSize);
        }

        int projectilesRemaining = finalAmount - targetsToFireAt;
        for (int i = 0; i < projectilesRemaining; i++)
        {
            Vector2 randomDirection = Random.insideUnitCircle.normalized;
            SpawnAndInitializeProjectile(null, randomDirection, finalDamage, finalSpeed, finalDuration, finalKnockback, finalPierce, finalSize);
        }
    }

    private void SpawnAndInitializeProjectile(Transform target, Vector2 direction, float finalDamage, float finalSpeed, float finalDuration, float finalKnockback, int finalPierce, float finalSize)
    {
        GameObject projectileObj = Instantiate(weaponData.weaponPrefab, firePoint.position, Quaternion.identity);
        ProjectileWeapon projectile = projectileObj.GetComponent<ProjectileWeapon>();

        if (projectile != null)
        {
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

        Transform orbitCenter = transform.parent;
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