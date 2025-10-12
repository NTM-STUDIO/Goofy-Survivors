using UnityEngine;

/// <summary>
/// Handles the behavior of a projectile weapon prefab.
/// If a target is assigned, it will home in on it. Otherwise, it moves in a straight line.
/// It damages enemies on contact and is destroyed after its duration expires or its pierce count is depleted.
/// </summary>
public class ProjectileWeapon : MonoBehaviour
{
    // --- Stats passed from WeaponController ---
    private float damage;
    private float speed;
    private float knockbackForce;
    private int pierceCount;

    // --- Private variables ---
    private Vector2 direction;
    private Transform target; // The enemy to home in on
    private float lifetime; // Reintroduced to manage duration

    /// <summary>
    /// Initializes the projectile's properties.
    /// </summary>
    /// <param name="targetEnemy">The target to home towards. Can be null for a straight shot.</param>
    /// <param name="initialDirection">The direction to fly in if there is no target.</param>
    public void Initialize(Transform targetEnemy, Vector2 initialDirection, float finalDamage, float finalSpeed, float finalDuration, float finalKnockback, int finalPierce, float finalSize)
    {
        this.target = targetEnemy;
        this.direction = initialDirection.normalized;
        this.damage = finalDamage;
        this.speed = finalSpeed;
        this.knockbackForce = finalKnockback;
        this.pierceCount = finalPierce;
        this.lifetime = finalDuration; // Initialize lifetime

        // Apply the size multiplier to the projectile's scale
        transform.localScale *= finalSize;
        
        // Removed: Destroy(gameObject, finalDuration);
        // We will now handle destruction in Update()
    }

    void Update()
    {
        // === DURATION CHECK ===
        lifetime -= Time.deltaTime;
        if (lifetime <= 0f)
        {
            Destroy(gameObject);
            return;
        }
        
        // If a target is assigned and still exists, update the direction to home in on it.
        if (target != null)
        {
            direction = (target.position - transform.position).normalized;
        }

        // Move the projectile forward in its set direction every frame.
        if (speed > 0)
        {
            // Note: Using transform.position += direction * speed * Time.deltaTime is often preferred for 2D.
            transform.position += (Vector3)direction * speed * Time.deltaTime; 
        }
        
        // Rotate the projectile to face the direction it's moving.
        if (direction != Vector2.zero)
        {
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
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
                Destroy(gameObject); // Destroy the projectile after it runs out of pierce.

            }
        }
    }
}