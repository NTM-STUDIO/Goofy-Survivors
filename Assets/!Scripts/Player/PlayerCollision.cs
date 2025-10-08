using UnityEngine;

public class PlayerCollision : MonoBehaviour
{
    [Header("Damage Settings")]
    [Tooltip("Additional i-frame duration after enemy trigger hits (seconds)")] public float extraIFrameOnCollision = 0.0f;

    private PlayerStats playerStats;

    private void Awake()
    {
        playerStats = GetComponent<PlayerStats>();
        if (playerStats == null)
        {
            Debug.LogWarning("PlayerCollision requires PlayerStats on the same GameObject.");
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (playerStats == null) return;

        // Enemy body trigger
        if (other.CompareTag("Enemy"))
        {
            var enemyStats = other.GetComponent<EnemyStats>();
            float dmg = enemyStats != null ? enemyStats.GetAttackDamage() : 10f;
            Vector2 from = other.transform.position;
            // Always use default i-frames from PlayerStats
            playerStats.ApplyDamage(dmg, from, null);
            Debug.Log("Player hit by enemy body for " + dmg + " damage. Player HP: " + playerStats.CurrentHp + "/" + playerStats.maxHp);
            EnemyStats enemy = other.GetComponent<EnemyStats>();
            Vector2 knockbackDirection = (transform.position - other.transform.position).normalized;
            enemy.ApplyKnockback(5f, 0.4f, -knockbackDirection);
            UIManager.Instance.UpdateHealthBar(playerStats.CurrentHp, playerStats.maxHp);
            return;
        }

        // Enemy projectile trigger
        // if (other.CompareTag("EnemyProjectile"))
        // {
        //     float dmg = 10f; // default projectile damage if not using a component
        //     Vector2 from = other.transform.position;
        //     playerStats.ApplyDamage(dmg, from, null, 0f); // no knockback

        //     // Destroy projectile on hit (optional; depends on your design)
        //     Destroy(other.gameObject);
        // }
    }
    
    private void OnTriggerStay2D(Collider2D other)
    {
        if (playerStats == null) return;

        if (other.CompareTag("Enemy"))
        {
            var enemyStats = other.GetComponent<EnemyStats>();
            float dmg = enemyStats != null ? enemyStats.GetAttackDamage() : 10f;
            Vector2 from = other.transform.position;
            playerStats.ApplyDamage(dmg, from, null);
        }
    }
}