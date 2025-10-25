using UnityEngine;
using System;
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
    public static event Action<EnemyStats> OnEnemyDamaged;

    [Header("Visuals & Effects")]
    [SerializeField] private GameObject damagePopupPrefab;
    [SerializeField] private Transform popupSpawnPoint;

    [Header("Base Stats")]
    public float baseHealth = 100;
    public float baseDamage = 10;
    public float moveSpeed = 3f;
    
    [Header("Mutations")]
    [Range(0f, 1f)]
    [SerializeField] private float mutationChance = 0.1f;
    [SerializeField] private float minMutationBonus = 0.2f;
    [SerializeField] private float maxMutationBonus = 0.5f;

    [Header("Mutation Colors")]
    [SerializeField] private Color healthMutationColor = Color.green;
    [SerializeField] private Color damageMutationColor = Color.red;
    [SerializeField] private Color speedMutationColor = Color.blue;

    [Header("Knockback Settings")]
    [SerializeField] private bool isUnknockable = false;
    [Range(0f, 1f)]
    [SerializeField] private float knockbackResistance = 0f;
    [SerializeField] private float resistanceIncreasePerHit = 0.1f;
    [Range(0f, 1f)]
    [SerializeField] private float maxResistance = 0.8f;

    public float MaxHealth { get; private set; }
    public float CurrentHealth { get; private set; }
    public bool IsKnockedBack { get; private set; }
    public MutationType CurrentMutation { get; private set; } = MutationType.None;

    [Header("Experience Drops")]
    public OrbDropConfig[] orbDrops;

    private Rigidbody rb;
    private SpriteRenderer enemyRenderer;
    private Color originalColor; // This is the true original color set once in Awake
    private Coroutine knockbackCoroutine;
    private float currentKnockbackResistance;
    private float originalBaseHealth;
    private float originalBaseDamage;
    private float originalMoveSpeed;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        enemyRenderer = GetComponentInChildren<SpriteRenderer>();
        if (enemyRenderer != null)
        {
            // Store the original material color to revert to after taking damage
            originalColor = enemyRenderer.color;
        }
        currentKnockbackResistance = knockbackResistance;
        
        originalBaseHealth = baseHealth;
        originalBaseDamage = baseDamage;
        originalMoveSpeed = moveSpeed;
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
            finalHealth = baseHealth * GameManager.Instance.currentEnemyHealthMultiplier;
        }
        else
        {
            finalHealth = baseHealth;
        }
        MaxHealth = finalHealth;
        CurrentHealth = finalHealth;
    }

    public void TakeDamage(float damage, bool isCritical)
    {
        if (CurrentHealth <= 0) return;

        OnEnemyDamaged?.Invoke(this);

        CurrentHealth -= damage;

        if (damagePopupPrefab != null)
        {
            GameObject popupGO = Instantiate(damagePopupPrefab, popupSpawnPoint.position, Quaternion.identity);
            popupGO.GetComponent<DamagePopup>().Setup(Mathf.RoundToInt(damage), isCritical);
        }

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

    public MutationType StealMutation()
    {
        if (CurrentMutation == MutationType.None) return MutationType.None;
        MutationType stolenType = CurrentMutation;
        baseHealth = originalBaseHealth;
        baseDamage = originalBaseDamage;
        moveSpeed = originalMoveSpeed;
        if (enemyRenderer != null)
        {
            // Revert to the true original color
            enemyRenderer.color = originalColor;
        }
        CurrentMutation = MutationType.None;
        return stolenType;
    }
    
    private void ApplyMutation()
    {
        if (UnityEngine.Random.value > mutationChance) return;
        int mutationChoice = UnityEngine.Random.Range(0, 3);
        float bonusMultiplier = UnityEngine.Random.Range(minMutationBonus, maxMutationBonus);

        switch (mutationChoice)
        {
            case 0: CurrentMutation = MutationType.Health; baseHealth *= (1 + bonusMultiplier); if (enemyRenderer != null) enemyRenderer.color = healthMutationColor; break;
            case 1: CurrentMutation = MutationType.Damage; baseDamage *= (1 + bonusMultiplier); if (enemyRenderer != null) enemyRenderer.color = damageMutationColor; break;
            case 2: CurrentMutation = MutationType.Speed; moveSpeed *= (1 + bonusMultiplier); if (enemyRenderer != null) enemyRenderer.color = speedMutationColor; break;
        }
    }

    public float GetAttackDamage()
    {
        if (GameManager.Instance != null) { return baseDamage * GameManager.Instance.currentEnemyDamageMultiplier; }
        return baseDamage;
    }

    // --- YOUR ORIGINAL COROUTINE RESTORED ---
    private IEnumerator FlashColor()
    {
        enemyRenderer.color = Color.red;
        yield return new WaitForSeconds(0.15f); // Duration of the flash
        
        // This is the key difference: it checks the mutation status AFTER the flash.
        if (CurrentMutation != MutationType.None)
        {
            // If it's still mutated, revert to the correct mutation color.
            switch (CurrentMutation)
            {
                case MutationType.Health: enemyRenderer.color = healthMutationColor; break;
                case MutationType.Damage: enemyRenderer.color = damageMutationColor; break;
                case MutationType.Speed: enemyRenderer.color = speedMutationColor; break;
            }
        }
        else
        {
            // If it's not mutated (or was just stolen), revert to the true original color.
            enemyRenderer.color = originalColor;
        }
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
        float randomValue = UnityEngine.Random.Range(0f, totalChance);
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