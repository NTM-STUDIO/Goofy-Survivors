using UnityEngine;
using System.Linq;
using System.Collections.Generic;

public class WeaponController : MonoBehaviour
{
    public WeaponData weaponData;

    private float currentCooldown;
    private PlayerStats playerStats;
    private Transform firePoint;
    private Transform playerTransform;

    private ShieldWeapon activeShield;

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

        if (weaponData.archetype == WeaponArchetype.Aura) { ActivateAura(); }
        else if (weaponData.archetype == WeaponArchetype.Shield)
        {
            SpawnPermanentShield();
            EnemyStats.OnEnemyDamaged += HandleEnemyDamagedForShield;
        }
    }

    void OnDestroy()
    {
        if (weaponData.archetype == WeaponArchetype.Shield)
        {
            EnemyStats.OnEnemyDamaged -= HandleEnemyDamagedForShield;
        }
    }

// Substitua o seu método Update() inteiro por este no WeaponController.cs
void Update()
{
    if (playerStats == null || weaponData == null || playerTransform == null) return;

    // A lógica para Auras e Escudos não muda.
    if (weaponData.archetype == WeaponArchetype.Aura || weaponData.archetype == WeaponArchetype.Shield)
    {
        return;
    }

    currentCooldown -= Time.deltaTime;
    if (currentCooldown <= 0f)
    {
        Attack();

        float finalAttackSpeed = playerStats.attackSpeedMultiplier;
        float cooldownBasedOnSpeed = weaponData.cooldown / Mathf.Max(0.01f, finalAttackSpeed);

        // --- NOVA LÓGICA PARA LIMITAR A RECARGA PELA DURAÇÃO ---
        // Verifica se a arma é do tipo Orbital.
        if (weaponData.archetype == WeaponArchetype.Orbit)
        {
            // Calcula a duração final da habilidade.
            float durationCooldown = weaponData.duration * playerStats.durationMultiplier;

            // Define a recarga como o MAIOR valor entre a recarga baseada na velocidade e a duração.
            // Isso "limita" a velocidade de ataque à duração da habilidade.
            currentCooldown = Mathf.Max(cooldownBasedOnSpeed, durationCooldown);
        }
        else
        {
            // Para todos os outros tipos de arma (Projéteis, etc.), a lógica continua a mesma.
            currentCooldown = cooldownBasedOnSpeed;
        }
        // --- FIM DA NOVA LÓGICA ---
    }
}

    private void HandleEnemyDamagedForShield(EnemyStats damagedEnemy)
    {
        // If the shield doesn't exist or the player stats aren't found, do nothing.
        if (activeShield == null || playerStats == null) return;

        if (damagedEnemy.CurrentMutation != MutationType.None)
        {
            MutationType stolenType = damagedEnemy.StealMutation();
            if (stolenType != MutationType.None)
            {
                // The duration for the visual effect
                float finalDuration = weaponData.duration * playerStats.durationMultiplier;

                // --- DO BOTH ACTIONS ---
                // 1. Tell the PlayerStats to add the temporary stat bonus.
                playerStats.AddTemporaryBuff(stolenType);

                // 2. Tell our active shield to start its visual color change.
                activeShield.AbsorbBuff(stolenType, finalDuration);
            }
        }
    }

    // Adicione este método inteiro dentro da sua classe WeaponController
    // Substitua o método inteiro no seu WeaponController.cs
    // Substitua o método inteiro no seu WeaponController.cs
    private void SpawnShadowClone()
    {
        // O prefab do clone é o "weaponPrefab" nos dados desta arma
        if (weaponData.weaponPrefab == null)
        {
            Debug.LogError("O prefab do Shadow Clone não está atribuído no WeaponData!");
            return;
        }

        // --- LÓGICA DE SPAWN COM VALORES FIXOS (HARDCODED) ---
        float minSpawnRadius = 10f; // Distância mínima do jogador
        float maxSpawnRadius = 40f; // Distância máxima do jogador

        // 1. Pega uma direção 2D aleatória e a normaliza.
        Vector2 randomDirection2D = Random.insideUnitCircle.normalized;
        Vector3 randomDirection = new Vector3(randomDirection2D.x, 0, randomDirection2D.y);

        // 2. Pega uma distância aleatória entre os valores fixos.
        float randomDistance = Random.Range(minSpawnRadius, maxSpawnRadius);

        // 3. Calcula a posição final de spawn.
        Vector3 spawnPosition = playerTransform.position + randomDirection * randomDistance;
        // --- FIM DA LÓGICA DE SPAWN ---

        GameObject cloneObj = Instantiate(weaponData.weaponPrefab, spawnPosition, Quaternion.identity);
        ShadowClone cloneScript = cloneObj.GetComponent<ShadowClone>();

        if (cloneScript != null)
        {
            // O resto do método continua o mesmo
            PlayerStats player = GetComponentInParent<PlayerStats>();
            if (player == null)
            {
                Debug.LogError("WeaponController não conseguiu encontrar o PlayerStats nos seus pais!", this);
                Destroy(cloneObj); // Destroi o clone se o jogador não for encontrado
                return;
            }

            WeaponController[] playerWeaponControllers = player.GetComponentsInChildren<WeaponController>();
            List<WeaponData> activeWeapons = new List<WeaponData>();

            foreach (var wc in playerWeaponControllers)
            {
                if (wc.weaponData.archetype != WeaponArchetype.ShadowCloneJutsu)
                {
                    activeWeapons.Add(wc.weaponData);
                }
            }

            cloneScript.Initialize(activeWeapons);
        }
    }



    private void Attack()
    {
        switch (weaponData.archetype)
        {
            case WeaponArchetype.Projectile: FireProjectile(); break;
            case WeaponArchetype.Orbit: ActivateOrbitingWeapon(); break;
            case WeaponArchetype.ShadowCloneJutsu: SpawnShadowClone(); break;
        }
    }

    private void SpawnPermanentShield()
    {
        GameObject shieldObj = Instantiate(weaponData.weaponPrefab, transform.position, Quaternion.identity, transform);
        activeShield = shieldObj.GetComponentInChildren<ShieldWeapon>();

        if (activeShield == null)
        {
            Debug.LogWarning($"Shield prefab for {weaponData.weaponName} is missing the ShieldWeapon script!");
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
            float startingAngle = i * angleStep;
            GameObject orbitingWeaponObj = Instantiate(weaponData.weaponPrefab, orbitCenter.position, Quaternion.identity, orbitCenter);
            OrbitingWeapon orbiter = orbitingWeaponObj.GetComponent<OrbitingWeapon>();
            if (orbiter != null)
            {
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
            DamageResult damageResult = playerStats.CalculateDamage(weaponData.damage);

            Vector3 direction;
            if (target != null)
            {
                direction = (target.position - firePoint.position).normalized;
                direction.y = 0;
            }
            else
            {
                Vector2 randomCircleDir = UnityEngine.Random.insideUnitCircle.normalized;
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