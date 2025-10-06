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
        if (enemyStats == null || player == null || enemyStats.IsKnockedBack)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }


        Vector2 direction = ((Vector2)player.position - rb.position).normalized;
        Vector2 newPosition = rb.position + direction * enemyStats.moveSpeed * Time.fixedDeltaTime;
        
        rb.MovePosition(newPosition);
    }
}