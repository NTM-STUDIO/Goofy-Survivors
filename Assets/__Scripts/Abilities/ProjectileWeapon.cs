using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ProjectileWeapon : MonoBehaviour
{
    private float damage;
    private bool wasCritical; // NEW: Stores whether this instance is a critical hit
    private float speed;
    private float knockbackForce;

    private Rigidbody rb;
    private float lifetime;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = false;
        }
    }

    // --- MODIFIED: Initialize method now accepts the isCritical flag ---
    public void Initialize(Transform targetEnemy, Vector3 initialDirection, float finalDamage, bool isCritical, float finalSpeed, float finalDuration, float finalKnockback, float finalSize)
    {
        this.damage = finalDamage;
        this.wasCritical = isCritical; // Store the crit status
        this.speed = finalSpeed;
        this.knockbackForce = finalKnockback;
        this.lifetime = 3f; // Or you could use finalDuration
        transform.localScale *= finalSize;

        if (rb != null)
        {
            rb.linearVelocity = initialDirection.normalized * speed;
        }
    }

    void Update()
    {
        lifetime -= Time.deltaTime;
        if (lifetime <= 0f)
        {
            Destroy(gameObject);
        }
    }

    // --- MODIFIED: Trigger detection now passes the crit status ---
    void OnTriggerEnter(Collider other)
    {
        // Check if the collided object is tagged as "Enemy" or "Reaper".
        if (other.CompareTag("Enemy") || other.CompareTag("Reaper"))
        {
            // Get the EnemyStats component from the parent of the collided object.
            EnemyStats enemyStats = other.GetComponentInParent<EnemyStats>();
            if (enemyStats != null)
            {
                // Apply damage, passing along the stored critical hit status.
                enemyStats.TakeDamage(damage, wasCritical);

                // Only apply knockback if the target is NOT a Reaper.
                if (!other.CompareTag("Reaper"))
                {
                    Vector3 knockbackDirection = (other.transform.position - transform.position).normalized;
                    knockbackDirection.y = 0;
                    enemyStats.ApplyKnockback(knockbackForce, 0.4f, knockbackDirection);
                }

                // Destroy the projectile after impact.
                // You might want to add a pierce mechanic here in the future,
                // which would prevent the projectile from being destroyed immediately.
                Destroy(gameObject);
            }
        }
    }
}