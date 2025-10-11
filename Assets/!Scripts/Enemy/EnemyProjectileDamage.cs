using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class EnemyProjectileDamage : MonoBehaviour
{

    //HARDCODED FOR NOW - TO CHANGE LATER
    public float damageAmount = 10f;

    public EnemyStats enemyStats;
    private void OnTriggerEnter2D(Collider2D other)
    {
        // S� aplica dano se colidir com o player
        if (other.CompareTag("Player"))
        {
            var playerStats = other.GetComponent<PlayerStats>();
            if (playerStats != null)
            {
                playerStats.ApplyDamage(damageAmount, transform.position, 0f);
            }

            // Destruir o proj�til ap�s colidir com o player
            Destroy(gameObject);
        }
    }
}