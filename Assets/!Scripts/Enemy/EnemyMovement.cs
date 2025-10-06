using UnityEngine;

public class EnemyMovement : MonoBehaviour
{
    private Transform player;
    private Rigidbody2D rb;

    private EnemyStats enemyStats;

    void Awake()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        rb = GetComponent<Rigidbody2D>();
        
        enemyStats = GetComponent<EnemyStats>();
    }

    void FixedUpdate()
    {
        if (enemyStats == null) return;

        if (player != null && !enemyStats.IsKnockedBack)
        {
            Vector2 direction = (player.position - transform.position).normalized;
            rb.linearVelocity = direction * enemyStats.moveSpeed;
        }
    }
}