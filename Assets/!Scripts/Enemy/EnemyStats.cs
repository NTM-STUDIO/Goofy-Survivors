using UnityEngine;
using System.Collections;

// A sua classe OrbDropConfig está perfeita, não precisa de alterações.
[System.Serializable]
public class OrbDropConfig
{
    public GameObject orbPrefab;
    [Range(0f, 100f)] public float dropChance;
}

[RequireComponent(typeof(Rigidbody2D))] 
public class EnemyStats : MonoBehaviour
{
    // --- MUDANÇA: Stats base ---
    [Header("Base Stats")]
    [Tooltip("A vida do inimigo no início do jogo (minuto 0).")]
    public int baseHealth = 100;
    [Tooltip("O dano do inimigo no início do jogo (minuto 0).")]
    public int baseDamage = 10; // --- NOVO: Variável de dano base ---
    public float moveSpeed = 2f;

    // --- Stats Atuais (calculados no início) ---
    public float currentHealth;

    // --- Cached Components ---
    private Rigidbody2D rb;

    // --- State Management ---
    private bool isKnockedBack = false;
    public bool IsKnockedBack { get { return isKnockedBack; } }

    // --- Experience Drop Settings (sem alterações) ---
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
            // Pega no multiplicador de dano e calcula o dano final
            float damageMultiplier = GameManager.Instance.currentEnemyDamageMultiplier;
            return baseDamage * damageMultiplier;
        }
        
        // Fallback: Se não houver GameManager, usa o dano base
        return baseDamage;
    }


    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        if (currentHealth <= 0) Die();

        Debug.Log(gameObject.name + " took " + damage + " damage. Remaining health: " + currentHealth);
        GetComponent<SpriteRenderer>().color = Color.red;
        Invoke("ResetColor", 0.1f);
    }

    void ResetColor()
    {
        GetComponent<SpriteRenderer>().color = Color.white;
    }

    public void Die()
    {
        TryDropOrb();
        Destroy(gameObject);
    }

    public void TryDropOrb()
    {
        if (Random.Range(0f, 100f) <= chanceToDropNothing)
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

    public void ApplyKnockback(float knockbackForce, Vector2 direction)
    {
        if (knockbackForce <= 0) return;
        isKnockedBack = true;
        rb.linearVelocity = Vector2.zero; 
        rb.AddForce(direction * knockbackForce, ForceMode2D.Impulse);
        StartCoroutine(KnockbackCooldown());
    }

    private IEnumerator KnockbackCooldown()
    {
        yield return new WaitForSeconds(0.2f);
        isKnockedBack = false;
    }
}