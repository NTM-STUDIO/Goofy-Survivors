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
    public float baseHealth = 100; // Changed to float for consistency
    [Tooltip("The enemy's damage at the start of the game (minute 0).")]
    public float baseDamage = 10; // Changed to float for consistency
    public float moveSpeed = 3f;

    // --- Public State ---
    public float CurrentHealth { get; private set; }
    public bool IsKnockedBack { get; private set; }

    [Header("Experience Drops")]
    [Tooltip("The chance (out of 100) that this enemy will drop *any* orb.")]
    [Range(0f, 100f)] public float chances = 75f;
    public OrbDropConfig[] orbDrops;

    // --- Private Components & State ---
    private Rigidbody rb;
    private Renderer enemyRenderer; // Using Renderer to work with both Sprites and Meshes
    private Color originalColor;
    private Coroutine knockbackCoroutine;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        
        // Using GetComponentInChildren to find a renderer on this object or its children
        enemyRenderer = GetComponentInChildren<Renderer>();
        if (enemyRenderer != null)
        {
            // Store the original material color to revert to after taking damage
            originalColor = enemyRenderer.material.color;
        }
    }

    void Start()
    {
        // Scale health based on game time if GameManager is present
        if (GameManager.Instance != null)
        {
            float healthMultiplier = GameManager.Instance.currentEnemyHealthMultiplier;
            CurrentHealth = baseHealth * healthMultiplier;
        }
        else
        {
            CurrentHealth = baseHealth;
        }
    }

    /// <summary>
    /// Calculates the enemy's current attack damage, scaled by the GameManager.
    /// </summary>
    public float GetAttackDamage()
    {
        if (GameManager.Instance != null)
        {
            float damageMultiplier = GameManager.Instance.currentEnemyDamageMultiplier;
            return baseDamage * damageMultiplier;
        }
        return baseDamage;
    }

    /// <summary>
    /// Applies damage to the enemy, triggers a visual effect, and checks for death.
    /// </summary>
    public void TakeDamage(float damage)
    {
        if (CurrentHealth <= 0) return; // Already dead

        CurrentHealth -= damage;

        // Trigger damage flash effect
        if (enemyRenderer != null)
        {
            // Stop any previous flash to reset its timer
            StopCoroutine("FlashColor"); 
            StartCoroutine("FlashColor");
        }
        
        if (CurrentHealth <= 0)
        {
            Die();
        }
    }

    // Coroutine for the damage flash effect
    private IEnumerator FlashColor()
    {
        enemyRenderer.material.color = Color.red;
        yield return new WaitForSeconds(0.15f); // Duration of the flash
        enemyRenderer.material.color = originalColor;
    }
    
    /// <summary>
    /// Handles the enemy's death, dropping orbs and destroying the GameObject.
    /// </summary>
    public void Die()
    {
        if (!gameObject.activeSelf) return; // Prevent multiple deaths

        TryDropOrb();
        Destroy(gameObject);
    }

    /// <summary>
    /// Rolls for a chance to drop a configured experience orb.
    /// </summary>
    public void TryDropOrb()
    {
        // The first roll determines IF an orb drops at all
        if (Random.Range(0f, 100f) > chances)
        {
            return; // Failed the initial drop chance
        }

        // If successful, iterate through possible orbs and roll for each one
        foreach (var orb in orbDrops)
        {
            if (Random.Range(0f, 100f) <= orb.dropChance)
            {
                Instantiate(orb.orbPrefab, transform.position, Quaternion.identity);
                return; // Drop the first one that succeeds and exit
            }
        }
    }
    
    /// <summary>
    /// Applies a knockback force to the enemy, interrupting its current state.
    /// </summary>
    public void ApplyKnockback(float knockbackForce, float duration, Vector3 direction)
    {
        if (knockbackForce <= 0) return;

        // Stop the previous knockback coroutine if it exists, allowing a new one to take over
        if (knockbackCoroutine != null)
        {
            StopCoroutine(knockbackCoroutine);
        }

        knockbackCoroutine = StartCoroutine(KnockbackRoutine(knockbackForce, duration, direction));
    }

    private IEnumerator KnockbackRoutine(float force, float duration, Vector3 direction)
    {
        IsKnockedBack = true;

        // Ensure knockback is purely horizontal
        direction.y = 0; 

        // Apply the force
        rb.linearVelocity = Vector3.zero; // Stop current movement
        rb.AddForce(direction.normalized * force, ForceMode.Impulse);

        // Wait for the stun duration
        yield return new WaitForSeconds(duration);

        // Reset state after knockback ends
        rb.linearVelocity = Vector3.zero; // Stop the knockback movement
        IsKnockedBack = false;
        knockbackCoroutine = null;
    }
}