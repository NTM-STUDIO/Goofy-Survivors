using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class ProjectileWeapon : MonoBehaviour
{
    private float damage;
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

    public void Initialize(Transform targetEnemy, Vector3 initialDirection, float finalDamage, float finalSpeed, float finalDuration, float finalKnockback, float finalSize)
    {
        this.damage = finalDamage;
        this.speed = finalSpeed;
        this.knockbackForce = finalKnockback;
        this.lifetime = 5f;
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

    void OnTriggerEnter(Collider other)
    {
        // Check if the collided object is tagged as "Enemy" or "Reaper".
        if (other.CompareTag("Enemy") || other.CompareTag("Reaper"))
        {
            // Get the EnemyStats component from the parent of the collided object.
            EnemyStats enemyStats = other.GetComponentInParent<EnemyStats>();
            if (enemyStats != null)
            {
                // Apply damage.
                enemyStats.TakeDamage((int)damage);

                // Only apply knockback if the target is NOT a Reaper.
                if (!other.CompareTag("Reaper"))
                {
                    Vector3 knockbackDirection = (other.transform.position - transform.position).normalized;
                    knockbackDirection.y = 0;
                    enemyStats.ApplyKnockback(knockbackForce, 0.4f, knockbackDirection);
                }

                // Destroy the projectile after impact.
                Destroy(gameObject);
            }
        }
    }
}