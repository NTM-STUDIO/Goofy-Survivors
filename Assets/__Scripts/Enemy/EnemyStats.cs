using UnityEngine;
using System.Collections;

[System.Serializable]
public class OrbDropConfig
{
    public GameObject orbPrefab;
    [Tooltip("The relative chance for this orb to drop compared to others in the list.")]
    [Range(0f, 100f)] public float dropChance;
}

public enum MutationType { None, Health, Damage, Speed }

[RequireComponent(typeof(Rigidbody))]
public class EnemyStats : MonoBehaviour
{
    [Header("Visuals & Effects")]
    [Tooltip("Assign the Damage Popup prefab here.")]
    [SerializeField] private GameObject damagePopupPrefab;
    [Tooltip("The specific point where damage popups should spawn. If empty, it will spawn at the enemy's center.")]
    [SerializeField] private Transform popupSpawnPoint;

    [Header("Base Stats")]
    [Tooltip("The enemy's health at the start of the game (minute 0).")]
    public float baseHealth = 100;
    [Tooltip("The enemy's damage at the start of the game (minute 0).")]
    public float baseDamage = 10;
    public float moveSpeed = 3f;
    
    [Header("Mutations")]
    [Tooltip("Chance for this enemy to spawn with a random mutation (0 = never, 1 = always).")]
    [Range(0f, 1f)]
    [SerializeField] private float mutationChance = 0.1f;
    [Tooltip("The minimum bonus a mutation can grant (e.g., 0.2 for +20%).")]
    [SerializeField] private float minMutationBonus = 0.2f;
    [Tooltip("The maximum bonus a mutation can grant (e.g., 0.5 for +50%).")]
    [SerializeField] private float maxMutationBonus = 0.5f;

    [Header("Mutation Colors")]
    [SerializeField] private Color healthMutationColor = Color.green;
    [SerializeField] private Color damageMutationColor = Color.red;
    [SerializeField] private Color speedMutationColor = Color.blue;

    [Header("Knockback Settings")]
    [Tooltip("If checked, this enemy can never be knocked back.")]
    [SerializeField] private bool isUnknockable = false;
    [Tooltip("Base knockback resistance. 0 = no resistance, 1 = full resistance (100%).")]
    [Range(0f, 1f)]
    [SerializeField] private float knockbackResistance = 0f;
    [Tooltip("How much resistance is gained each time the enemy is knocked back. 0.1 = +10% resistance.")]
    [SerializeField] private float resistanceIncreasePerHit = 0.1f;
    [Tooltip("The maximum resistance the enemy can gain through stacking. 1 = 100%.")]
    [Range(0f, 1f)]
    [SerializeField] private float maxResistance = 0.8f;

    // --- Public State ---
    public float MaxHealth { get; private set; }
    public float CurrentHealth { get; private set; }
    public bool IsKnockedBack { get; private set; }
    public MutationType CurrentMutation { get; private set; } = MutationType.None;

    [Header("Experience Drops")]
    public OrbDropConfig[] orbDrops;

    // --- Private Components & State ---
    private Rigidbody rb;
    private SpriteRenderer enemyRenderer;
    private Color originalColor;
    private Coroutine knockbackCoroutine;
    private float currentKnockbackResistance;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        enemyRenderer = GetComponentInChildren<SpriteRenderer>();
        if (enemyRenderer != null)
        {
            originalColor = enemyRenderer.color;
        }
        currentKnockbackResistance = knockbackResistance;
        
        ApplyMutation();

        if (popupSpawnPoint == null)
        {
            popupSpawnPoint = this.transform;
        }
    }

    void Start()
    {
        float finalHealth;
        if (GameManager.Instance != null)
        {
            float healthMultiplier = GameManager.Instance.currentEnemyHealthMultiplier;
            finalHealth = baseHealth * healthMultiplier;
        }
        else
        {
            finalHealth = baseHealth;
        }
        
        MaxHealth = finalHealth;
        CurrentHealth = finalHealth;
    }

    // --- METHOD MODIFIED ---
    /// <summary>
    /// Applies damage to the enemy, triggers visual effects, and spawns a damage popup.
    /// This version now accepts a boolean to handle critical strike visuals.
    /// </summary>
    /// <param name="damage">The amount of damage to take.</param>
    /// <param name="isCritical">Was this hit a critical strike?</param>
    public void TakeDamage(float damage, bool isCritical)
    {
        if (CurrentHealth <= 0) return;

        CurrentHealth -= damage;

        // Spawn the damage popup and tell it whether the hit was critical.
        if (damagePopupPrefab != null)
        {
            GameObject popupGO = Instantiate(damagePopupPrefab, popupSpawnPoint.position, Quaternion.identity);
            // Call the Setup method that accepts the isCritical boolean
            popupGO.GetComponent<DamagePopup>().Setup(Mathf.RoundToInt(damage), isCritical);
        }

        // Trigger damage flash
        if (enemyRenderer != null)
        {
            StopCoroutine("FlashColor"); 
            StartCoroutine("FlashColor");
        }
        
        if (CurrentHealth <= 0)
        {
            Die();
        }
    }
    
    private void ApplyMutation()
    {
        if (Random.value > mutationChance) return;
        int mutationChoice = Random.Range(0, 3);
        float bonusMultiplier = Random.Range(minMutationBonus, maxMutationBonus);
        Color chosenMutationColor = originalColor;

        switch (mutationChoice)
        {
            case 0: CurrentMutation = MutationType.Health; baseHealth *= (1 + bonusMultiplier); chosenMutationColor = healthMutationColor; break;
            case 1: CurrentMutation = MutationType.Damage; baseDamage *= (1 + bonusMultiplier); chosenMutationColor = damageMutationColor; break;
            case 2: CurrentMutation = MutationType.Speed; moveSpeed *= (1 + bonusMultiplier); chosenMutationColor = speedMutationColor; break;
        }

        if (enemyRenderer != null && CurrentMutation != MutationType.None)
        {
            enemyRenderer.color = chosenMutationColor;
            originalColor = chosenMutationColor; 
        }
    }

    public float GetAttackDamage()
    {
        if (GameManager.Instance != null) { return baseDamage * GameManager.Instance.currentEnemyDamageMultiplier; }
        return baseDamage;
    }

    private IEnumerator FlashColor()
    {
        enemyRenderer.color = Color.red; 
        yield return new WaitForSeconds(0.15f);
        enemyRenderer.color = originalColor;
    }
    
    public void Die()
    {
        if (!gameObject.activeSelf) return;
        TryDropOrb();
        Destroy(gameObject);
    }

    public void TryDropOrb()
    {
        if (orbDrops == null || orbDrops.Length == 0) return;
        float totalChance = 0f;
        foreach (var orb in orbDrops) { totalChance += orb.dropChance; }
        if (totalChance <= 0) return;
        float randomValue = Random.Range(0f, totalChance);
        foreach (var orb in orbDrops)
        {
            if (randomValue <= orb.dropChance) { Instantiate(orb.orbPrefab, transform.position, Quaternion.identity); return; }
            else { randomValue -= orb.dropChance; }
        }
    }
    
    public void ApplyKnockback(float knockbackForce, float duration, Vector3 direction)
    {
        if (isUnknockable || IsKnockedBack || knockbackForce <= 0) return;
        float resistanceMultiplier = 1f - currentKnockbackResistance;
        float effectiveForce = knockbackForce * resistanceMultiplier;
        float effectiveDuration = duration * resistanceMultiplier;
        if (effectiveDuration <= 0.01f) return;
        if (knockbackCoroutine != null) StopCoroutine(knockbackCoroutine);
        knockbackCoroutine = StartCoroutine(KnockbackRoutine(effectiveForce, effectiveDuration, direction));
        if (resistanceIncreasePerHit > 0)
        {
            currentKnockbackResistance += resistanceIncreasePerHit;
            currentKnockbackResistance = Mathf.Min(currentKnockbackResistance, maxResistance);
        }
    }

    private IEnumerator KnockbackRoutine(float force, float duration, Vector3 direction)
    {
        IsKnockedBack = true;
        direction.y = 0; 
        rb.linearVelocity = Vector3.zero;
        rb.AddForce(direction.normalized * force, ForceMode.Impulse);
        yield return new WaitForSeconds(duration);
        rb.linearVelocity = Vector3.zero;
        IsKnockedBack = false;
        knockbackCoroutine = null;
    }
}