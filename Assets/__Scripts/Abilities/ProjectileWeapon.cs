using UnityEngine;

/// <summary>
/// Handles the behavior of a projectile weapon prefab.
/// It moves in a straight line, damages enemies on contact, and is destroyed
/// after its duration expires or its pierce count is depleted.
/// </summary>
public class ProjectileWeapon : MonoBehaviour
{
    // --- Stats passed from WeaponController ---
    private float damage;
    private float speed;
    private float knockbackForce;
    private int pierceCount;
    private float lifetime;

    // --- Private variables ---
    private Vector2 direction;

    /// <summary>
    /// Initializes the projectile's properties based on the final calculated stats from the WeaponController.
    /// </summary>
    public void Initialize(Vector2 dir, float finalDamage, float finalSpeed, float finalDuration, float finalKnockback, int finalPierce, float finalSize)
    {
        this.direction = dir.normalized;
        this.damage = finalDamage;
        this.speed = finalSpeed;
        this.lifetime = finalDuration;
        this.knockbackForce = finalKnockback;
        this.pierceCount = finalPierce;

        // Apply the size multiplier to the projectile's scale
        transform.localScale *= finalSize;

        // Ensure the projectile is destroyed after its duration expires to prevent it from flying forever.
        Destroy(gameObject, this.lifetime);
    }

    void Update()
    {
        // Move the projectile forward in its set direction every frame.
        if (speed > 0)
        {
            transform.Translate(direction * speed * Time.deltaTime);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // Check if the object we collided with has the "Enemy" tag.
        if (other.CompareTag("Enemy"))
        {
            EnemyStats enemyStats = other.GetComponent<EnemyStats>();
            if (enemyStats != null)
            {
                // Apply damage to the enemy.
                enemyStats.TakeDamage((int)damage);
                Debug.Log($"Projectile hit {other.name} for {damage} damage.");

                // Calculate knockback direction away from the projectile and apply it.
                Vector2 knockbackDirection = (other.transform.position - transform.position).normalized;
                enemyStats.ApplyKnockback(knockbackForce, 0.4f, knockbackDirection);

                // Handle piercing logic.
                pierceCount--;
                if (pierceCount < 0) // Use < 0 to allow for 0 pierce (1 hit).
                {
                    Destroy(gameObject); // Destroy the projectile after it runs out of pierce.
                }
            }
        }
    }
}