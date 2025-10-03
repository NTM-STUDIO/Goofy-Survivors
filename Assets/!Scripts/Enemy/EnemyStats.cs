using UnityEngine;
using System.Collections;

// --- ADD THIS CLASS DEFINITION ABOVE ENEMYSTATS ---
// This [System.Serializable] attribute allows you to see and edit this class in the Inspector.
[System.Serializable]
public class OrbDropConfig
{
    public GameObject orbPrefab; // The orb prefab (Common, Uncommon, etc.)
    [Range(0f, 100f)] public float dropChance; // The chance for this specific orb to drop
}


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
    public bool IsKnockedBack { get { return isKnockedBack; } }

    // --- NEW: Experience Drop Settings ---
    [Header("Experience Drops")]
    [Range(0f, 100f)] public float chanceToDropNothing = 20f; // 20% chance to drop no orb at all
    public OrbDropConfig[] orbDrops; // Array to hold all possible orb rarities


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

    // --- MODIFIED: Die() now calls the drop logic ---
    public void Die()
    {
        // Attempt to drop an orb before the enemy is destroyed.
        TryDropOrb();
        Destroy(gameObject);
    }

    // --- NEW: Orb Dropping Logic ---
    public void TryDropOrb()
    {
        // First, check if we should drop anything at all.
        if (Random.Range(0f, 100f) <= chanceToDropNothing)
        {
            return; // Dropped nothing, so we exit the function early.
        }

        // If we passed the "drop nothing" check, iterate through our possible orbs.
        // This loop checks for higher rarities first if you place them at the top of the list in the Inspector.
        foreach (var orb in orbDrops)
        {
            // For each orb type, roll the dice to see if it drops.
            if (Random.Range(0f, 100f) <= orb.dropChance)
            {
                // If the roll succeeds, spawn this orb...
                Instantiate(orb.orbPrefab, transform.position, Quaternion.identity);
                // ...and immediately exit the function so we don't drop multiple orbs.
                return; 
            }
        }
    }

    public void ApplyKnockback(float knockbackForce, Vector2 direction)
    {
        if (knockbackForce <= 0) return;

        isKnockedBack = true;
        // Use rb.velocity instead of linearVelocity for better consistency
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