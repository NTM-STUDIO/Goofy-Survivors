// FlyingPan.cs
using UnityEngine;

public class FlyingPan : MonoBehaviour
{
    public float damage = 10f;
    public float knockbackForce = 5f;
    public float lifespan = 5f; // How long the pan exists before disappearing

    private float lifetimeTimer;

    void Start()
    {
        lifetimeTimer = lifespan;
    }

    void Update()
    {
        // Countdown the lifespan timer
        lifetimeTimer -= Time.deltaTime;
        if (lifetimeTimer <= 0f)
        {
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // Check if the pan collided with an enemy
        if (other.CompareTag("Enemy"))
        {
            // Try to get the enemy's health component (you'll need to create this script)
            EnemyStats enemyStats = other.GetComponent<EnemyStats>();
            if (enemyStats != null)
            {
                enemyStats.Die();
            }

            // Apply knockback
            Rigidbody2D enemyRb = other.GetComponent<Rigidbody2D>();
            if (enemyRb != null)
            {
                Vector2 knockbackDirection = (other.transform.position - transform.position).normalized;
                enemyRb.AddForce(knockbackDirection * knockbackForce, ForceMode2D.Impulse);
            }
        }
    }
}