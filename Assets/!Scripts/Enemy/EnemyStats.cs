using UnityEngine;

public class EnemyStats : MonoBehaviour
{
    public int maxHealth = 100;
    private int currentHealth;

    public float moveSpeed = 2f;

    void Start()
    {
        currentHealth = maxHealth;
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    public void Die()
    {
        Debug.Log($"{gameObject.name} has died.");
        Destroy(gameObject);
    }
}
