using UnityEngine;

/// <summary>
/// Handles the behavior of an orbiting weapon prefab.
/// This version is persistent and hits all enemies it touches until its duration ends.
/// Pierce functionality has been removed.
/// </summary>
public class OrbitingWeapon : MonoBehaviour
{
    // --- Stats passed from WeaponData ---
    public float damage;
    public float knockbackForce; // Renamed from knockback for clarity

    [HideInInspector] public float rotationSpeed;
    [HideInInspector] public float orbitRadius;
    [HideInInspector] public Transform orbitCenter;

    PlayerStats playerStats;

    // --- Private variables ---
    private float currentAngle;
    private float lifetime;

    /// <summary>
    /// Initializes the weapon's perties based on the WeaponData ScriptableObject.
    /// Note: This method uses the weapon's base stats and does not account for PlayerStats multipliers.
    /// </summary>
    // Place this inside your OrbitingWeapon.cs script, replacing the old Initialize method.
    public void Initialize(Transform center, float startAngle, float finalDamage, float finalSpeed, float finalDuration, float finalKnockback, float finalSize)
    {
        
        this.orbitCenter = center;
        this.currentAngle = startAngle;

        // Assign all the calculated stats
        this.damage = finalDamage;
        this.rotationSpeed = finalSpeed;
        this.lifetime = finalDuration;
        this.knockbackForce = finalKnockback;

        // Use finalSize to determine the weapon's scale and orbit distance
        this.orbitRadius = finalSize * 4f; // Adjust the '4f' multiplier to change how far the weapon orbits
        transform.localScale = (Vector3.one * finalSize) / 4.5f;
    }
    void Update()
    {
        // Safety check: if the center point is destroyed, destroy the weapon too.
        if (orbitCenter == null)
        {
            Destroy(gameObject);
            return;
        }

        // Countdown the weapon's lifespan and destroy it when time is up.
        lifetime -= Time.deltaTime;
        if (lifetime <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        // Calculate the new position in the orbit based on rotation speed.
        currentAngle += rotationSpeed * Time.deltaTime;
        if (currentAngle > 360f) currentAngle -= 360f; // Keep the angle tidy

        float x = Mathf.Cos(currentAngle * Mathf.Deg2Rad) * orbitRadius;
        float y = Mathf.Sin(currentAngle * Mathf.Deg2Rad) * orbitRadius;
        transform.position = orbitCenter.position + new Vector3(x, y, 0);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // Check if the object we collided with has the "Enemy" tag.
        if (other.CompareTag("Enemy"))
        {
            var enemyStats = other.GetComponent<EnemyStats>();
            if (enemyStats != null)
            {
                // Apply damage to the enemy.
                enemyStats.TakeDamage((int)damage);
                Debug.Log($"Orbiting weapon hit {other.name} for {damage} damage.");

                // Calculate knockback direction away from the orbit center and apply it.
                Vector2 knockbackDirection = (other.transform.position - orbitCenter.position).normalized;
                enemyStats.ApplyKnockback(knockbackForce, 0.4f, knockbackDirection);

                // Pierce logic has been removed. The weapon will persist and can hit other enemies.
            }
        }
    }
}