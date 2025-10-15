using UnityEngine;
using System.Collections;

[System.Serializable]
public class OrbDropConfig
{
    public GameObject orbPrefab;
    [Range(0f, 100f)] public float dropChance;
}

// The script now requires a 3D Rigidbody.
[RequireComponent(typeof(Rigidbody))] 
public class EnemyStats : MonoBehaviour
{
    [Header("Base Stats")]
    [Tooltip("The enemy's health at the start of the game (minute 0).")]
    public int baseHealth = 100;
    [Tooltip("The enemy's damage at the start of the game (minute 0).")]
    public int baseDamage = 10;
    public float moveSpeed = 3f;

    public float currentHealth;

    // --- 3D Changes ---
    private Rigidbody rb; // Changed from Rigidbody2D
    private Renderer enemyRenderer; // Generic Renderer for Sprite or Mesh
    private Color originalColor;

    private bool isKnockedBack = false;
    public bool IsKnockedBack { get { return isKnockedBack; } }

    [Header("Experience Drops")]
    [Range(0f, 100f)] public float chanceToDropNothing = 20f;
    public OrbDropConfig[] orbDrops;

    void Awake()
    {
        // Get the 3D Rigidbody component
        rb = GetComponent<Rigidbody>(); 

        // Get the renderer (works for both 3D meshes and 2D sprites)
        enemyRenderer = GetComponentInChildren<Renderer>();
        if (enemyRenderer != null)
        {
            // Store the original material color to revert to after taking damage
            originalColor = enemyRenderer.material.color;
        }
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

        // Flash red using the cached renderer
        if (enemyRenderer != null)
        {
            enemyRenderer.material.color = Color.red;
            Invoke("ResetColor", 0.2f);
        }
    }

    void ResetColor()
    {
        // Revert to the original color
        if (enemyRenderer != null)
        {
            enemyRenderer.material.color = originalColor;
        }
    }

    public void Die()
    {
        TryDropOrb();
        Destroy(gameObject);
    }

    public void TryDropOrb()
    {
        // Logic for this function remains the same, as it's not physics-dependent
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

    // --- 3D Knockback Implementation ---
    public void ApplyKnockback(float knockbackForce, float duration, Vector3 direction)
    {
        if (knockbackForce <= 0 || isKnockedBack) return;

        isKnockedBack = true;
        
        // Ensure knockback is only on the XZ plane for isometric view
        direction.y = 0; 
        
        rb.linearVelocity = Vector3.zero; // Use Vector3.zero
        
        // Use the 3D ForceMode.Impulse
        rb.AddForce(direction.normalized * knockbackForce, ForceMode.Impulse);
        
        StartCoroutine(KnockbackCoroutine(duration));
    }

    IEnumerator KnockbackCoroutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        rb.linearVelocity = Vector3.zero; // Use Vector3.zero to stop movement
        isKnockedBack = false;
    }
}