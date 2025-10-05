using UnityEngine;

public class EnemyMovement : MonoBehaviour
{
    private Transform player;
    private Rigidbody2D rb;
    
    // --- NEW: Reference to the EnemyStats script ---
    private EnemyStats enemyStats;

    void Awake()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        rb = GetComponent<Rigidbody2D>();
        
        // --- NEW: Get the EnemyStats component from this same GameObject ---
        enemyStats = GetComponent<EnemyStats>();
    }

    void FixedUpdate()
    {
        // We must check if the enemyStats reference exists before using it.
        if (enemyStats == null) return;

        // --- CHANGE: The condition now checks the public property on the EnemyStats script ---
        // Instead of checking its own private variable, it asks the other script for its state.
        if (player != null && !enemyStats.IsKnockedBack)
        {
            Vector2 direction = (player.position - transform.position).normalized;
            // Use rb.velocity instead of linearVelocity for clarity and consistency
            rb.linearVelocity = direction * enemyStats.moveSpeed;
        }
    }
}