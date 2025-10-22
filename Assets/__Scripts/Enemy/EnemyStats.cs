using UnityEngine;
using System.Collections;

[System.Serializable]
public class OrbDropConfig
{
    public GameObject orbPrefab;
    [Tooltip("The relative chance for this orb to drop compared to others in the list.")]
    [Range(0f, 100f)] public float dropChance;
}

// The script now requires a 3D Rigidbody.
[RequireComponent(typeof(Rigidbody))]
public class EnemyStats : MonoBehaviour
{
    [Header("Base Stats")]
    [Tooltip("The enemy's health at the start of the game (minute 0).")]
    public float baseHealth = 100;
    [Tooltip("The enemy's damage at the start of the game (minute 0).")]
    public float baseDamage = 10;
    public float moveSpeed = 3f;

    // --- Public State ---
    public float MaxHealth { get; private set; } // The enemy's maximum health for its lifetime
    public float CurrentHealth { get; private set; }
    public bool IsKnockedBack { get; private set; }

    [Header("Experience Drops")]
    [Tooltip("A list of potential orbs to drop. One will always be chosen based on relative drop chances.")]
    public OrbDropConfig[] orbDrops;

    // --- Private Components & State ---
    private Rigidbody rb;
    private SpriteRenderer enemyRenderer;
    private Color originalColor;
    private Coroutine knockbackCoroutine;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        
        // Using GetComponentInChildren to find a renderer on this object or its children
        enemyRenderer = GetComponentInChildren<SpriteRenderer>();
        if (enemyRenderer != null)
        {
            // Store the original material color to revert to after taking damage
            originalColor = enemyRenderer.color;
        }
    }

    void Start()
    {
        float finalHealth;
        // Scale health based on game time if GameManager is present
        if (GameManager.Instance != null)
        {
            float healthMultiplier = GameManager.Instance.currentEnemyHealthMultiplier;
            finalHealth = baseHealth * healthMultiplier;
        }
        else
        {
            finalHealth = baseHealth;
        }
        
        // Set both MaxHealth and CurrentHealth one time upon spawning for accurate tracking.
        MaxHealth = finalHealth;
        CurrentHealth = finalHealth;
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
        enemyRenderer.color = Color.red;
        yield return new WaitForSeconds(0.15f); // Duration of the flash
        enemyRenderer.color = originalColor;
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
    /// **MODIFIED:** Guarantees one orb drop from the list based on weighted chances.
    /// </summary>
    public void TryDropOrb()
    {
        // Exit if there are no orbs configured to drop.
        if (orbDrops == null || orbDrops.Length == 0)
        {
            return;
        }

        // Calculate the sum of all drop chances to use as the total weight.
        float totalChance = 0f;
        foreach (var orb in orbDrops)
        {
            totalChance += orb.dropChance;
        }

        // If all drop chances are zero, do nothing.
        if (totalChance <= 0)
        {
            return;
        }

        // Pick a random number within the range of the total weight.
        float randomValue = Random.Range(0f, totalChance);

        // Iterate through the orbs and "spend" their chance value from the random number.
        // The first orb that the remaining randomValue falls into is the one that gets spawned.
        foreach (var orb in orbDrops)
        {
            if (randomValue <= orb.dropChance)
            {
                Instantiate(orb.orbPrefab, transform.position, Quaternion.identity);
                return; // Drop this orb and exit the method.
            }
            else
            {
                randomValue -= orb.dropChance;
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