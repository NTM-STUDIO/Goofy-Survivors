using UnityEngine;
using System.Collections;
using Unity.Netcode;

[System.Serializable]
public class OrbDropConfig
{
    public GameObject orbPrefab;
    [Range(0f, 100f)] public float dropChance;
}

<<<<<<< Updated upstream
[RequireComponent(typeof(Rigidbody2D))] 
public class EnemyStats : MonoBehaviour
=======
public enum MutationType { None, Health, Damage, Speed }

[RequireComponent(typeof(Rigidbody))]
public class EnemyStats : NetworkBehaviour
>>>>>>> Stashed changes
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

    // Networked mutation state (stored as int because NetworkVariable doesn't directly support enums generically)
    private NetworkVariable<int> networkedMutation = new NetworkVariable<int>(
        (int)MutationType.None,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // Networked core stats
    private NetworkVariable<float> networkMaxHealth = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<float> networkCurrentHealth = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<float> networkBaseDamage = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<float> networkMoveSpeed = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

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
<<<<<<< Updated upstream
            currentHealth = baseHealth;
=======
            finalHealth = baseHealth;
        }
        MaxHealth = finalHealth;
        CurrentHealth = finalHealth;

        // Publish core stats when running as server in P2P mode so clients can read them
        if (GameManager.Instance != null && GameManager.Instance.isP2P)
        {
            if (IsServer)
            {
                networkMaxHealth.Value = MaxHealth;
                networkCurrentHealth.Value = CurrentHealth;
                networkBaseDamage.Value = baseDamage;
                networkMoveSpeed.Value = moveSpeed;
            }
        }
    }

    public void TakeDamage(float damage, bool isCritical)
    {
        // In P2P mode, only the server should process damage.
        if (GameManager.Instance != null && GameManager.Instance.isP2P)
        {
            if (!IsServer) return;
        }

        if (CurrentHealth <= 0) return;

        OnEnemyDamaged?.Invoke(this);

        CurrentHealth -= damage;
        // publish health change to clients when running as server
        if (GameManager.Instance != null && GameManager.Instance.isP2P && IsServer)
        {
            networkCurrentHealth.Value = CurrentHealth;
        }

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
        // If we're in P2P mode, only the server should decide and apply mutations.
        if (GameManager.Instance != null && GameManager.Instance.isP2P)
        {
            if (!IsServer) return; // clients will get mutation through networkedMutation
        }

        if (UnityEngine.Random.value > mutationChance) return;
        int mutationChoice = UnityEngine.Random.Range(0, 3);
        float bonusMultiplier = UnityEngine.Random.Range(minMutationBonus, maxMutationBonus);

        switch (mutationChoice)
        {
            case 0:
                CurrentMutation = MutationType.Health;
                baseHealth *= (1 + bonusMultiplier);
                if (enemyRenderer != null) enemyRenderer.color = healthMutationColor;
                break;
            case 1:
                CurrentMutation = MutationType.Damage;
                baseDamage *= (1 + bonusMultiplier);
                if (enemyRenderer != null) enemyRenderer.color = damageMutationColor;
                break;
            case 2:
                CurrentMutation = MutationType.Speed;
                moveSpeed *= (1 + bonusMultiplier);
                if (enemyRenderer != null) enemyRenderer.color = speedMutationColor;
                break;
>>>>>>> Stashed changes
        }

        // If in P2P, the server must publish the mutation to clients
        if (GameManager.Instance != null && GameManager.Instance.isP2P && IsServer)
        {
            networkedMutation.Value = (int)CurrentMutation;
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Clients should listen for changes and apply visuals when server updates mutation.
        networkedMutation.OnValueChanged += OnNetworkedMutationChanged;
        networkCurrentHealth.OnValueChanged += OnNetworkCurrentHealthChanged;
        networkMaxHealth.OnValueChanged += OnNetworkMaxHealthChanged;
        networkBaseDamage.OnValueChanged += OnNetworkBaseDamageChanged;
        networkMoveSpeed.OnValueChanged += OnNetworkMoveSpeedChanged;

        // If we are a client in P2P, apply whatever mutation value the server already set (or None).
        if (GameManager.Instance != null && GameManager.Instance.isP2P)
        {
            if (!IsServer)
            {
                ApplyMutationFromNetwork((MutationType)networkedMutation.Value);
            }
            else
            {
                // Server: ensure its local mutation is published (in case ApplyMutation ran before network variable existed)
                networkedMutation.Value = (int)CurrentMutation;
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        // Unsubscribe callbacks to avoid leaks
        try
        {
            networkedMutation.OnValueChanged -= OnNetworkedMutationChanged;
            networkCurrentHealth.OnValueChanged -= OnNetworkCurrentHealthChanged;
            networkMaxHealth.OnValueChanged -= OnNetworkMaxHealthChanged;
            networkBaseDamage.OnValueChanged -= OnNetworkBaseDamageChanged;
            networkMoveSpeed.OnValueChanged -= OnNetworkMoveSpeedChanged;
        }
        catch { }
    }

    private void OnNetworkCurrentHealthChanged(float previous, float current)
    {
        if (IsServer) return;
        CurrentHealth = current;
    }

    private void OnNetworkMaxHealthChanged(float previous, float current)
    {
        if (IsServer) return;
        MaxHealth = current;
    }

    private void OnNetworkBaseDamageChanged(float previous, float current)
    {
        if (IsServer) return;
        baseDamage = current;
    }

    private void OnNetworkMoveSpeedChanged(float previous, float current)
    {
        if (IsServer) return;
        moveSpeed = current;
    }

    private void OnNetworkedMutationChanged(int previous, int current)
    {
        // Only clients need to react to mutation changes for visuals.
        if (IsServer) return;
        ApplyMutationFromNetwork((MutationType)current);
    }

    private void ApplyMutationFromNetwork(MutationType mutation)
    {
        // Revert to originals first
        baseHealth = originalBaseHealth;
        baseDamage = originalBaseDamage;
        moveSpeed = originalMoveSpeed;

        CurrentMutation = mutation;
        if (enemyRenderer != null)
        {
            switch (CurrentMutation)
            {
                case MutationType.Health: enemyRenderer.color = healthMutationColor; break;
                case MutationType.Damage: enemyRenderer.color = damageMutationColor; break;
                case MutationType.Speed: enemyRenderer.color = speedMutationColor; break;
                default: enemyRenderer.color = originalColor; break;
            }
        }

        // Note: health values (MaxHealth/CurrentHealth) are synced separately via networkMaxHealth/networkCurrentHealth.
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

        Debug.Log(gameObject.name + " took " + damage + " damage. Remaining health: " + currentHealth);
        GetComponentInChildren<SpriteRenderer>().color = Color.red;
        Invoke("ResetColor", 0.1f);
    }

    void ResetColor()
    {
        GetComponentInChildren<SpriteRenderer>().color = Color.white;
    }

    public void Die()
    {
<<<<<<< Updated upstream
=======
        if (!gameObject.activeSelf) return;

        // Only the server should run drop logic and destroy/despawn the enemy in P2P.
        if (GameManager.Instance != null && GameManager.Instance.isP2P)
        {
            if (!IsServer) return;

            TryDropOrb();
            var netObj = GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned)
            {
                try { netObj.Despawn(true); }
                catch { Destroy(gameObject); }
            }
            else
            {
                Destroy(gameObject);
            }
            return;
        }

        // Single-player fallback
>>>>>>> Stashed changes
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
<<<<<<< Updated upstream
            if (Random.Range(0f, 100f) <= orb.dropChance)
            {
                Instantiate(orb.orbPrefab, transform.position, Quaternion.identity);
                return; 
            }
=======
            if (randomValue <= orb.dropChance)
            {
                // In P2P only the server should instantiate pickup drops
                if (GameManager.Instance != null && GameManager.Instance.isP2P)
                {
                    if (IsServer) Instantiate(orb.orbPrefab, transform.position, Quaternion.identity);
                }
                else
                {
                    Instantiate(orb.orbPrefab, transform.position, Quaternion.identity);
                }
                return;
            }
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
>>>>>>> Stashed changes
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

    /// <summary>
    /// Faz o inimigo virar-se na direção de um alvo.
    /// </summary>
    /// <param name="targetPosition">A posição do alvo para onde o inimigo deve virar.</param>
    public void FlipTowards(Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - transform.position;
        if (direction.x != 0)
        {
            Vector3 localScale = transform.localScale;
            localScale.x = Mathf.Sign(direction.x) * Mathf.Abs(localScale.x);
            transform.localScale = localScale;
        }
    }
}