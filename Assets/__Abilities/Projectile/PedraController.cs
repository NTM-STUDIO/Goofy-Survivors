using UnityEngine;

public class PedraController : MonoBehaviour
{
    public WeaponData weaponData;

    private float currentCooldown;
    private PlayerStats playerStats;
    private Transform firePoint;

    void Start()
    {
        playerStats = FindFirstObjectByType<PlayerStats>();
        GameObject playerObject = GameObject.FindWithTag("Player");
        if (playerObject != null)
        {
            firePoint = playerObject.transform.Find("Visuals/FirePoint");
            if (firePoint == null)
            {
                firePoint = playerObject.transform;
            }
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
            float cooldownBasedOnSpeed = weaponData.cooldown / Mathf.Max(0.01f, finalAttackSpeed);
            float finalDuration = weaponData.duration * playerStats.durationMultiplier;
            currentCooldown = Mathf.Max(cooldownBasedOnSpeed, finalDuration);
        }
    }

    private void Attack()
    {
        float finalDamage = weaponData.damage * playerStats.damageMultiplier;
        int finalAmount = weaponData.amount + playerStats.projectileCount;
        float finalSize = weaponData.area * playerStats.projectileSizeMultiplier;
        float finalSpeed = weaponData.speed * playerStats.projectileSpeedMultiplier;
        float finalDuration = weaponData.duration * playerStats.durationMultiplier;
        float finalKnockback = weaponData.knockback * playerStats.knockbackMultiplier;

        for (int i = 0; i < finalAmount; i++)
        {
            Vector2 randomCircleDir = Random.insideUnitCircle.normalized;
            Vector3 randomDirection = new Vector3(randomCircleDir.x, 0, randomCircleDir.y);
            
            SpawnAndInitializeProjectile(randomDirection, finalDamage, finalSpeed, finalDuration, finalKnockback, finalSize);
        }
    }
    
    private void SpawnAndInitializeProjectile(Vector3 direction, float finalDamage, float finalSpeed, float finalDuration, float finalKnockback, float finalSize)
    {
        GameObject projectileObj = Instantiate(weaponData.weaponPrefab, firePoint.position, Quaternion.identity);
        ProjectileWeapon projectile = projectileObj.GetComponent<ProjectileWeapon>();

        if (projectile != null)
        {
            projectile.Initialize(null, direction, finalDamage, finalSpeed, finalDuration, finalKnockback, finalSize);
        }
    }
}