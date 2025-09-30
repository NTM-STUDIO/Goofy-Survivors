using UnityEngine;
using System.Collections;

public class EnemyController : MonoBehaviour
{
    [Header("Stats")]
    public float baseHealth = 100f;
    public float currentHealth;
    public float speed = 3f;
    public float damage = 10f;

    [Header("Knockback Settings")]
    public float knockbackForce = 10f;
    public float knockbackDuration = 0.5f; // The "stun" duration

    private Transform player;
    private Rigidbody2D rb;
    private bool isKnockbacked = false;

    void Awake()
    {
        currentHealth = baseHealth;
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        rb = GetComponent<Rigidbody2D>();
    }

    void FixedUpdate()
    {
        // Physics-based movement should be in FixedUpdate for consistency.
        // The enemy only moves if it's not currently in a knockback state.
        if (player != null && !isKnockbacked)
        {
            Vector2 direction = (player.position - transform.position).normalized;
            rb.linearVelocity = direction * speed;
        }
    }

    /// <summary>
    /// Public method to initiate the knockback sequence.
    /// </summary>
    public void ApplyKnockback(Vector2 knockbackDirection)
    {
        // Prevent multiple knockbacks from starting at the same time.
        if (isKnockbacked) return;

        StartCoroutine(KnockbackCoroutine(knockbackDirection));
    }

    /// <summary>
    /// Handles the knockback "stun" effect.
    /// 1. Stops current movement.
    /// 2. Applies an instant knockback force.
    /// 3. Pauses for the stun duration.
    /// 4. Stops all movement again to prevent sliding.
    /// 5. Resumes normal behavior.
    /// </summary>
    private IEnumerator KnockbackCoroutine(Vector2 knockbackDirection)
    {
        isKnockbacked = true;

        // Immediately stop the enemy's current movement.
        rb.linearVelocity = Vector2.zero;

        // Apply the knockback force as a single, instant impulse.
        rb.AddForce(knockbackDirection * knockbackForce, ForceMode2D.Impulse);

        // Wait for the specified "stun" duration.
        yield return new WaitForSeconds(knockbackDuration);

        // Stop any residual sliding movement from the knockback force.
        rb.linearVelocity = Vector2.zero;

        isKnockbacked = false;
    }

    // --- Other Methods ---

    public void SetStats(float healthMultiplier)
    {
        currentHealth = baseHealth * healthMultiplier;
    }

    public void TakeDamage(float amount)
    {
        currentHealth -= amount;
        if (currentHealth <= 0)
        {
            DestroyEnemy();
        }
    }

    private void DestroyEnemy()
    {
        // You can add particle effects or sounds here before destroying.
        Destroy(gameObject);
    }
}