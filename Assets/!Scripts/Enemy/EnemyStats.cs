using UnityEngine;
using System.Collections;

[RequireComponent(typeof(Rigidbody2D))] 
public class EnemyStats : MonoBehaviour
{
    // --- Stats ---
    public int health = 100;
    public float moveSpeed = 2f;

    // --- Cached Components ---
    private Rigidbody2D rb;

    // --- State Management ---
    private bool isKnockedBack = false;
    
    // --- NEW: PUBLIC GETTER ---
    // This allows other scripts (like EnemyMovement) to read the value of isKnockedBack
    // without being able to change it. This is a safe way to share state.
    public bool IsKnockedBack { get { return isKnockedBack; } }

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    public void TakeDamage(int damage)
    {
        health -= damage;
        if (health <= 0) Die();

        Debug.Log(gameObject.name + " took " + damage + " damage. Remaining health: " + health);
        GetComponent<SpriteRenderer>().color = Color.red;
        Invoke("ResetColor", 0.1f);
    }

    void ResetColor()
    {
        GetComponent<SpriteRenderer>().color = Color.white;
    }

    public void Die()
    {
        Destroy(gameObject);
    }

    public void ApplyKnockback(float knockbackForce, Vector2 direction)
    {
        if (knockbackForce <= 0) return;

        isKnockedBack = true;
        rb.linearVelocity = Vector2.zero;
        rb.AddForce(direction * knockbackForce, ForceMode2D.Impulse);
        Debug.Log("Knockback applied to " + gameObject.name);
        StartCoroutine(KnockbackCooldown());
    }

    private IEnumerator KnockbackCooldown()
    {
        yield return new WaitForSeconds(0.2f);
        isKnockedBack = false;
    }
}