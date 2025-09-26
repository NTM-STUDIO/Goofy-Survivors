using UnityEngine;

public class EnemyController : MonoBehaviour
{
    [Header("Stats")]
    public float baseHealth = 100f;
    public float currentHealth;
    public float speed = 3f;
    public float damage = 10f;

    private Transform player;

    void Awake()
    {
        currentHealth = baseHealth;
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
    }

    void Update()
    {
        // Basic movement towards the player (temporary)
        if (player != null)
        {
            Vector2 direction = (player.position - transform.position).normalized;
            transform.position += (Vector3)direction * speed * Time.deltaTime;
        }
    }

    /// Idea
    /// <summary>
    /// Sets the enemy's stats, applying multipliers.
    /// </summary>
    /// <param name="healthMultiplier">The multiplier to apply to the base health.</param>
    public void SetStats(float healthMultiplier)
    {
        currentHealth = baseHealth * healthMultiplier;
    }

    public void TakeDamage(float amount)
    {
        currentHealth -= amount;
        if (currentHealth <= 0)
        {
            Destroy();
        }
    }

    private void Destroy()
    {
        // efects here
        Destroy(gameObject);
    }
}