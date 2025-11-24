using UnityEngine;
using Unity.Netcode;
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
public class EnemyStats : NetworkBehaviour
{
    public static event Action<EnemyStats> OnEnemyDamaged;
    public static event Action<EnemyStats, float, PlayerStats> OnEnemyDamagedWithAmount;

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
    private Color originalColor;
    private Coroutine flashCoroutine; // OPTIMIZATION: Cached reference to the flash coroutine.
    private Coroutine knockbackCoroutine; // OPTIMIZATION: Cached reference to the knockback coroutine.
    private float currentKnockbackResistance;
    private float originalBaseHealth;
    private float originalBaseDamage;
    private float originalMoveSpeed;

    private readonly NetworkVariable<float> netMaxHealth = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<float> netCurrentHealth = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<float> netBaseDamage = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<float> netMoveSpeed = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private readonly NetworkVariable<int> netMutation = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        enemyRenderer = GetComponentInChildren<SpriteRenderer>();
        if (enemyRenderer != null)
        {
            originalColor = enemyRenderer.color;
        }
        currentKnockbackResistance = knockbackResistance;
        
        originalBaseHealth = baseHealth;
        originalBaseDamage = baseDamage;
        originalMoveSpeed = moveSpeed;

        if (popupSpawnPoint == null)
        {
            popupSpawnPoint = transform;
        }
    }

    void Start()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            if (IsServer)
            {
                float finalHealth = baseHealth * (GameManager.Instance != null ? GameManager.Instance.currentEnemyHealthMultiplier : 1f);
                ApplyMutation();
                MaxHealth = finalHealth;
                CurrentHealth = finalHealth;

                netMaxHealth.Value = MaxHealth;
                netCurrentHealth.Value = CurrentHealth;
                netBaseDamage.Value = baseDamage;
                netMoveSpeed.Value = moveSpeed;
                netMutation.Value = (int)CurrentMutation;
            }
            else
            {
                MaxHealth = netMaxHealth.Value;
                CurrentHealth = netCurrentHealth.Value;
                baseDamage = netBaseDamage.Value;
                moveSpeed = netMoveSpeed.Value;
                CurrentMutation = (MutationType)netMutation.Value;

                netCurrentHealth.OnValueChanged += OnNetworkHealthChanged;
                netMutation.OnValueChanged += OnNetworkMutationChanged;
            }
        }
        else
        {
            float finalHealth = baseHealth * (GameManager.Instance != null ? GameManager.Instance.currentEnemyHealthMultiplier : 1f);
            MaxHealth = finalHealth;
            CurrentHealth = finalHealth;
        }
    }

    public void TakeDamage(float damage, bool isCritical)
    {
        ApplyDamage(damage, isCritical, null);
    }

    public void TakeDamageFromAttacker(float damage, bool isCritical, PlayerStats attacker)
    {
        ApplyDamage(damage, isCritical, attacker);
    }

    private void ApplyDamage(float damage, bool isCritical, PlayerStats attacker)
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            if (!IsServer) 
            {
                TakeDamageServerRpc(damage, isCritical);
                return;
            }

            if (netCurrentHealth.Value <= 0f)
            {
                if (MaxHealth > 0f && CurrentHealth == 0f)
                {
                    netMaxHealth.Value = MaxHealth;
                    netCurrentHealth.Value = MaxHealth;
                }
                else
                {
                    return;
                }
            }

            OnEnemyDamaged?.Invoke(this);
            if (damage > 0f) OnEnemyDamagedWithAmount?.Invoke(this, damage, attacker);

            float newHealth = netCurrentHealth.Value - damage;
            netCurrentHealth.Value = newHealth;

            if (damagePopupPrefab != null)
            {
                Instantiate(damagePopupPrefab, popupSpawnPoint.position, Quaternion.identity).GetComponent<DamagePopup>().Setup(Mathf.RoundToInt(damage), isCritical);
            }

            if (enemyRenderer != null)
            {
                if (flashCoroutine != null) StopCoroutine(flashCoroutine);
                flashCoroutine = StartCoroutine(FlashColor());
            }

            if (newHealth <= 0f)
            {
                Die();
            }
            return;
        }

        if (CurrentHealth <= 0) return;

        OnEnemyDamaged?.Invoke(this);
        if (damage > 0f) OnEnemyDamagedWithAmount?.Invoke(this, damage, attacker);
        CurrentHealth -= damage;

        if (damagePopupPrefab != null)
        {
            Instantiate(damagePopupPrefab, popupSpawnPoint.position, Quaternion.identity).GetComponent<DamagePopup>().Setup(Mathf.RoundToInt(damage), isCritical);
        }

        if (enemyRenderer != null)
        {
            if (flashCoroutine != null) StopCoroutine(flashCoroutine);
            flashCoroutine = StartCoroutine(FlashColor());
        }
        
        if (CurrentHealth <= 0)
        {
            Die();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void TakeDamageServerRpc(float damage, bool isCritical)
    {
        TakeDamage(damage, isCritical);
    }

    private void OnNetworkHealthChanged(float oldValue, float newValue)
    {
        float damageTaken = oldValue - newValue;
        CurrentHealth = newValue;
        if (damageTaken > 0f)
        {
            if (damagePopupPrefab != null && popupSpawnPoint != null)
            {
                Instantiate(damagePopupPrefab, popupSpawnPoint.position, Quaternion.identity).GetComponent<DamagePopup>().Setup(Mathf.RoundToInt(damageTaken), false);
            }
            if (enemyRenderer != null)
            {
                if (flashCoroutine != null) StopCoroutine(flashCoroutine);
                flashCoroutine = StartCoroutine(FlashColor());
            }
        }
        if (newValue <= 0f)
        {
            gameObject.SetActive(false);
        }
    }

    private void OnNetworkMutationChanged(int oldVal, int newVal)
    {
        CurrentMutation = (MutationType)newVal;
        if (enemyRenderer == null) return;
        switch (CurrentMutation)
        {
            case MutationType.Health: enemyRenderer.color = healthMutationColor; break;
            case MutationType.Damage: enemyRenderer.color = damageMutationColor; break;
            case MutationType.Speed: enemyRenderer.color = speedMutationColor; break;
            default: enemyRenderer.color = originalColor; break;
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
            enemyRenderer.color = originalColor;
        }
        CurrentMutation = MutationType.None;

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && IsServer)
        {
            netMutation.Value = (int)CurrentMutation;
        }

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
        return baseDamage * (GameManager.Instance != null ? GameManager.Instance.currentEnemyDamageMultiplier : 1f);
    }

    private IEnumerator FlashColor()
    {
        enemyRenderer.color = Color.red;
        yield return new WaitForSeconds(0.15f);
        
        if (CurrentMutation != MutationType.None)
        {
            switch (CurrentMutation)
            {
                case MutationType.Health: enemyRenderer.color = healthMutationColor; break;
                case MutationType.Damage: enemyRenderer.color = damageMutationColor; break;
                case MutationType.Speed: enemyRenderer.color = speedMutationColor; break;
            }
        }
        else
        {
            enemyRenderer.color = originalColor;
        }
        flashCoroutine = null;
    }
    
    public void Die()
    {
        if (!gameObject.activeSelf) return;

        // If this is the reaper, cache the damage in GameManager before despawn/destroy
        if (GameManager.Instance != null && GameManager.Instance.reaperStats == this)
        {
            GameManager.Instance.CacheReaperDamage(MaxHealth - CurrentHealth);
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            if (IsServer)
            {
                TryDropOrb();
                var netObj = GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    netObj.Despawn(true);
                }
                else
                {
                    Destroy(gameObject);
                }
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
        else
        {
            TryDropOrb();
            Destroy(gameObject);
        }
    }

    public void TryDropOrb()
    {
        if (orbDrops == null || orbDrops.Length == 0) return;
        float totalChance = 0f;
        foreach (var orb in orbDrops) { totalChance += orb.dropChance; }
        if (totalChance <= 0) return;

        float randomValue = UnityEngine.Random.Range(0f, totalChance);
        for (int i = 0; i < orbDrops.Length; i++)
        {
            var orb = orbDrops[i];
            if (randomValue <= orb.dropChance)
            {
                SpawnOrb(orb);
                return;
            }
            else
            {
                randomValue -= orb.dropChance;
            }
        }
        // If we reach here, no orb was dropped due to rounding or low totalChance. Force drop the first orb as fallback.
        SpawnOrb(orbDrops[0]);

    }

    private void SpawnOrb(OrbDropConfig orb)
    {
        bool isNetworked = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (isNetworked && IsServer)
        {
            GameObject spawned = Instantiate(orb.orbPrefab, transform.position, Quaternion.identity);
            var netObj = spawned.GetComponent<NetworkObject>();
            if (netObj == null)
            {
                netObj = spawned.AddComponent<NetworkObject>();
            }
            netObj.Spawn(true);
        }
        else if (!isNetworked)
        {
            Instantiate(orb.orbPrefab, transform.position, Quaternion.identity);
        }
    }
    
    public void ApplyKnockback(float knockbackForce, float duration, Vector3 direction)
    {
        if (!isActiveAndEnabled || CurrentHealth <= 0f) return;
        if (isUnknockable || IsKnockedBack || knockbackForce <= 0) return;
        
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            if (!IsServer)
            {
                ApplyKnockbackServerRpc(knockbackForce, duration, direction);
                return;
            }
        }
        
        if (!isActiveAndEnabled) return;

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

    [ServerRpc(RequireOwnership = false)]
    private void ApplyKnockbackServerRpc(float knockbackForce, float duration, Vector3 direction)
    {
        ApplyKnockback(knockbackForce, duration, direction);
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

    private void OnDisable()
    {
        if (NetworkManager.Singleton != null && !IsServer)
        {
            if (netCurrentHealth != null) netCurrentHealth.OnValueChanged -= OnNetworkHealthChanged;
            if (netMutation != null) netMutation.OnValueChanged -= OnNetworkMutationChanged;
        }
    }
}