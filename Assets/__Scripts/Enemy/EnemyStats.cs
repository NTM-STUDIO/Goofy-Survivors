using UnityEngine;
using System.Collections;

[System.Serializable]
public class OrbDropConfig
{
    public GameObject orbPrefab;
    [Range(0f, 100f)] public float dropChance;
}

[RequireComponent(typeof(Rigidbody2D))] 
public class EnemyStats : MonoBehaviour
{
    [Header("Base Stats")]
    [Tooltip("A vida do inimigo no início do jogo (minuto 0).")]
    public int baseHealth = 100;
    [Tooltip("O dano do inimigo no início do jogo (minuto 0).")]
    public int baseDamage = 10;
    public float moveSpeed = 3f;

    public float currentHealth;

    private Rigidbody2D rb;

    private bool isKnockedBack = false;
    public bool IsKnockedBack { get { return isKnockedBack; } }

    [Header("Experience Drops")]
    [Range(0f, 100f)] public float chanceToDropNothing = 20f;
    public OrbDropConfig[] orbDrops;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Start()
    {
        if (GameManager.Instance != null)
        {
            float healthMultiplier = GameManager.Instance.currentEnemyHealthMultiplier;
            currentHealth = baseHealth * healthMultiplier;
        }
        else
        {
            currentHealth = baseHealth;
        }
    }

    public float GetAttackDamage()
    {
        if (GameManager.Instance != null)
        {
            float damageMultiplier = GameManager.Instance.currentEnemyDamageMultiplier;
            return baseDamage * damageMultiplier;
        }
        
        return baseDamage;
    }


    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        if (currentHealth <= 0) Die();

        GetComponentInChildren<SpriteRenderer>().color = Color.red;
        Invoke("ResetColor", 0.2f);
    }

    void ResetColor()
    {
        GetComponentInChildren<SpriteRenderer>().color = Color.white;
    }

    public void Die()
    {
        TryDropOrb();
        Destroy(gameObject);
    }

    public void TryDropOrb()
    {
        if (Random.Range(1f, 100f) <= chanceToDropNothing)
        {
            return;
        }
        
        foreach (var orb in orbDrops)
        {
            if (Random.Range(0f, 100f) <= orb.dropChance)
            {
                Instantiate(orb.orbPrefab, transform.position, Quaternion.identity);
                return; 
            }
        }
    }

    public void ApplyKnockback(float knockbackForce, float duration, Vector2 direction)
    {
        if (knockbackForce <= 0 || isKnockedBack) return; // Prevent new knockback while already stunned

        isKnockedBack = true;
        rb.linearVelocity = Vector2.zero; // Use rb.velocity to be consistent with Rigidbody2D properties
        rb.AddForce(direction.normalized * knockbackForce, ForceMode2D.Impulse);
        StartCoroutine(KnockbackCoroutine(duration));
    }

    IEnumerator KnockbackCoroutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        rb.linearVelocity = Vector2.zero; // Stop movement after knockback duration
        isKnockedBack = false;
    }
}