using UnityEngine;
using System;
using System.Collections;

// A struct to hold the result of a damage calculation, including the final damage and whether it was a critical hit.
public struct DamageResult
{
    public float damage;
    public bool isCritical;
}

public class PlayerStats : MonoBehaviour
{
    [Header("Character Data")]
    public PlayerCharacterData characterData;

    // --- Your Original Stat Fields ---
    [HideInInspector] public int maxHp;
    [HideInInspector] public float hpRegen;
    [HideInInspector] public float damageMultiplier;
    [HideInInspector] public float critChance;
    [HideInInspector] public float critDamageMultiplier;
    [HideInInspector] public float attackSpeedMultiplier;
    [HideInInspector] public int projectileCount;
    [HideInInspector] public float projectileSizeMultiplier;
    [HideInInspector] public float projectileSpeedMultiplier;
    [HideInInspector] public float durationMultiplier;
    [HideInInspector] public float knockbackMultiplier;
    [HideInInspector] public float movementSpeed;
    [HideInInspector] public float luck;
    [HideInInspector] public float pickupRange;
    [HideInInspector] public float xpGainMultiplier;
    [HideInInspector] public int pierceCount;

    [Header("Health & Invincibility")]
    [Tooltip("Current HP of the player (clamped to [0, maxHp])")]
    [SerializeField] private int currentHp;
    [Tooltip("Duration of invincibility frames after taking a hit (seconds)")]
    [Min(0f)]
    [SerializeField] private float invincibilityDuration = 0.6f;
    [Tooltip("Whether the player is currently invincible (i-frames)")]
    [SerializeField] private bool invincible = false;
    [Tooltip("Optional flash on damage")][SerializeField] private SpriteRenderer spriteRenderer;
    [ColorUsage(true, true)][SerializeField] private Color hurtFlashColor = new Color(1f, 0.4f, 0.4f, 1f);
    [SerializeField] private float hurtFlashTime = 0.1f;
    private Color _originalColor;

    // Events for UI and gameplay hooks
    public event Action<int, int> OnHealthChanged; // (current, max)
    public event Action OnDamaged;
    public event Action OnHealed;
    public event Action OnDeath;

    public bool IsInvincible => invincible;
    public int CurrentHp => currentHp;

    private void Awake()
    {
        InitializeStats();
    }

    private void Start()
    {
        StartCoroutine(HealthRegenRoutine());
    }

    /// <summary>
    /// Calculates final damage based on player stats, including a check for critical strikes.
    /// This is the central point for all outgoing player damage.
    /// </summary>
    /// <param name="baseDamage">The base damage of the weapon dealing the hit.</param>
    /// <returns>A DamageResult containing the final damage and a bool indicating if it was a crit.</returns>
    public DamageResult CalculateDamage(float baseDamage)
    {
        float finalDamage = baseDamage * this.damageMultiplier;
        bool isCritical = false;

        // Roll for a critical strike using the player's current crit chance.
        if (UnityEngine.Random.value <= this.critChance)
        {
            isCritical = true;
            finalDamage *= this.critDamageMultiplier;
        }

        return new DamageResult { damage = finalDamage, isCritical = isCritical };
    }

    #region Initialization and Stat Modifiers
    private void InitializeStats()
    {
        if (characterData == null)
        {
            Debug.LogError("CRITICAL: PlayerCharacterData is not assigned in the PlayerStats component!", this);
            return;
        }

        maxHp = characterData.maxHp;
        hpRegen = characterData.hpRegen;
        damageMultiplier = characterData.damageMultiplier;
        critChance = characterData.critChance;
        critDamageMultiplier = characterData.critDamageMultiplier;
        attackSpeedMultiplier = characterData.attackSpeedMultiplier;
        projectileCount = characterData.projectileCount;
        projectileSizeMultiplier = characterData.projectileSizeMultiplier;
        projectileSpeedMultiplier = characterData.projectileSpeedMultiplier;
        durationMultiplier = characterData.durationMultiplier;
        knockbackMultiplier = characterData.knockbackMultiplier;
        movementSpeed = characterData.movementSpeed;
        luck = characterData.luck;
        pickupRange = characterData.pickupRange;
        xpGainMultiplier = characterData.xpGainMultiplier;
        pierceCount = (int)characterData.pierceCount;

        foreach (var bonus in characterData.startingBonuses)
        {
            ApplyStatBonus(bonus);
        }

        currentHp = maxHp;
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null) _originalColor = spriteRenderer.color;

        OnHealthChanged?.Invoke(currentHp, maxHp);
        var uiManager = FindFirstObjectByType<UIManager>();
        if (uiManager != null) uiManager.UpdateHealthBar(currentHp, maxHp);
    }

    public void ApplyLevelUpScaling()
    {
        if (characterData == null) return;
        foreach (var bonus in characterData.scalingBonusesPerLevel)
        {
            ApplyStatBonus(bonus);
        }
        Debug.Log("Applied level up scaling bonuses.");
    }

    private void ApplyStatBonus(StatBonus bonus)
    {
        switch (bonus.stat)
        {
            case StatType.MaxHP:
                maxHp += (int)bonus.value;
                Heal((int)bonus.value);
                break;
            case StatType.HPRegen:
                hpRegen += bonus.value;
                break;
            case StatType.DamageMultiplier:
                damageMultiplier += bonus.value;
                break;
            case StatType.CritChance:
                critChance = Mathf.Clamp01(critChance + bonus.value);
                break;
            case StatType.CritDamageMultiplier:
                critDamageMultiplier += bonus.value;
                break;
            case StatType.AttackSpeedMultiplier:
                attackSpeedMultiplier += bonus.value;
                break;
            case StatType.ProjectileCount:
                projectileCount += (int)bonus.value;
                break;
            case StatType.ProjectileSizeMultiplier:
                projectileSizeMultiplier += bonus.value;
                break;
            case StatType.ProjectileSpeedMultiplier:
                projectileSpeedMultiplier += bonus.value;
                break;
            case StatType.DurationMultiplier:
                durationMultiplier += bonus.value;
                break;
            case StatType.KnockbackMultiplier:
                knockbackMultiplier += bonus.value;
                break;
            case StatType.MovementSpeed:
                movementSpeed += bonus.value;
                break;
            case StatType.Luck:
                luck += bonus.value;
                break;
            case StatType.PickupRange:
                pickupRange += (int)bonus.value;
                break;
            case StatType.XPGainMultiplier:
                xpGainMultiplier += bonus.value;
                break;
            // NOTE: Remember to add a case for PierceCount if it's in your StatType enum
            default:
                 Debug.LogWarning($"Stat bonus for {bonus.stat} not implemented in ApplyStatBonus method.");
                 break;
        }
    }

    public void IncreaseMaxHP(int amount) { maxHp += amount; currentHp += amount; var uiManager = FindFirstObjectByType<UIManager>(); if (uiManager != null) uiManager.UpdateHealthBar(currentHp, maxHp); OnHealthChanged?.Invoke(currentHp, maxHp); }
    public void IncreaseHPRegen(float amount) { hpRegen += amount; }
    public void IncreaseDamageMultiplier(float amount) { damageMultiplier += amount; }
    public void IncreaseCritChance(float amount) { critChance += amount; }
    public void IncreaseCritDamageMultiplier(float amount) { critDamageMultiplier += amount; }
    public void IncreaseAttackSpeedMultiplier(float amount) { attackSpeedMultiplier += amount; }
    public void IncreaseProjectileCount(int amount) { projectileCount += amount; }
    public void IncreaseProjectileSizeMultiplier(float amount) { projectileSizeMultiplier += amount; }
    public void IncreaseProjectileSpeedMultiplier(float amount) { projectileSpeedMultiplier += amount; }
    public void IncreaseDurationMultiplier(float amount) { durationMultiplier += amount; }
    public void IncreaseKnockbackMultiplier(float amount) { knockbackMultiplier += amount; }
    public void IncreaseMovementSpeed(float amount) { movementSpeed += amount; }
    public void IncreaseLuck(float amount) { luck += amount; }
    public void IncreasePickupRange(float amount) { pickupRange += amount; }
    public void IncreaseXPGainMultiplier(float amount) { xpGainMultiplier += amount; }
    public void DecreaseMaxHP(int amount) { maxHp -= amount; }
    public void DecreaseHPRegen(float amount) { hpRegen -= amount; }
    public void DecreaseDamageMultiplier(float amount) { damageMultiplier -= amount; }
    public void DecreaseCritChance(float amount) { critChance -= amount; }
    public void DecreaseCritDamageMultiplier(float amount) { critDamageMultiplier -= amount; }
    public void DecreaseAttackSpeedMultiplier(float amount) { attackSpeedMultiplier -= amount; }
    public void DecreaseProjectileCount(int amount) { projectileCount -= amount; }
    public void DecreaseProjectileSizeMultiplier(float amount) { projectileSizeMultiplier -= amount; }
    public void DecreaseProjectileSpeedMultiplier(float amount) { projectileSpeedMultiplier -= amount; }
    public void DecreaseDurationMultiplier(float amount) { durationMultiplier -= amount; }
    public void DecreaseKnockbackMultiplier(float amount) { knockbackMultiplier -= amount; }
    public void DecreaseMovementSpeed(float amount) { movementSpeed -= amount; }
    public void DecreaseLuck(float amount) { luck -= amount; }
    public void DecreasePickupRange(float amount) { pickupRange -= amount; }
    public void DecreaseXPGainMultiplier(float amount) { xpGainMultiplier -= amount; }
    #endregion

    #region Core Gameplay Logic
    public void PrintStats()
    {
        Debug.Log("Stats Initialized from Character Data");
        Debug.Log($"MaxHP: {maxHp}, HPRegen: {hpRegen}, DamageMultiplier: {damageMultiplier}");
        Debug.Log($"CritChance: {critChance}, CritDamageMultiplier: {critDamageMultiplier}, AttackSpeedMultiplier: {attackSpeedMultiplier}");
        Debug.Log($"ProjectileCount: {projectileCount}, ProjectileSizeMultiplier: {projectileSizeMultiplier}, ProjectileSpeedMultiplier: {projectileSpeedMultiplier}");
        Debug.Log($"DurationMultiplier: {durationMultiplier}, KnockbackMultiplier: {knockbackMultiplier}, MovementSpeed: {movementSpeed}");
        Debug.Log($"Luck: {luck}, PickupRange: {pickupRange}, XPGainMultiplier: {xpGainMultiplier}");
    }
    
    public void Heal(int amount)
    {
        if (amount <= 0 || currentHp <= 0) return;
        int prev = currentHp;
        currentHp = Mathf.Clamp(currentHp + amount, 0, maxHp);
        if (currentHp != prev)
        {
            OnHealed?.Invoke();
            OnHealthChanged?.Invoke(currentHp, maxHp);
            var uiManager = FindFirstObjectByType<UIManager>();
            if (uiManager != null)
            {
                uiManager.UpdateHealthBar(currentHp, maxHp);
            }
        }
    }

    public void ApplyDamage(float amount, Vector3? hitFromWorldPos = null, float? customIFrameDuration = null)
    {
        if (amount <= 0f || invincible || currentHp <= 0) return;
        int damageInt = Mathf.CeilToInt(amount);
        currentHp = Mathf.Clamp(currentHp - damageInt, 0, maxHp);

        if (spriteRenderer != null)
        {
            StopCoroutine(nameof(FlashRoutine));
            StartCoroutine(FlashRoutine());
        }
        OnDamaged?.Invoke();
        OnHealthChanged?.Invoke(currentHp, maxHp);

        float iFrames = customIFrameDuration.HasValue ? Mathf.Max(0f, customIFrameDuration.Value) : invincibilityDuration;
        if (iFrames > 0f) StartCoroutine(InvincibilityRoutine(iFrames));

        if (currentHp <= 0)
        {
            HandleDeath();
        }

        var uiManager = FindFirstObjectByType<UIManager>();
        if (uiManager != null)
            uiManager.UpdateHealthBar(currentHp, maxHp);
    }

    public void BeginInvincibility(float duration)
    {
        if (duration <= 0f) return;
        StopCoroutine(nameof(InvincibilityRoutine));
        StartCoroutine(InvincibilityRoutine(duration));
    }

    private IEnumerator InvincibilityRoutine(float duration)
    {
        invincible = true;
        yield return new WaitForSeconds(duration);
        invincible = false;
    }

    private IEnumerator FlashRoutine()
    {
        if (spriteRenderer == null) yield break;
        spriteRenderer.color = hurtFlashColor;
        yield return new WaitForSeconds(hurtFlashTime);
        spriteRenderer.color = _originalColor;
    }

    private void HandleDeath()
    {
        Debug.Log("Player died.");
        OnDeath?.Invoke();

        var movement = GetComponent<Movement>();
        if (movement != null)
        {
            movement.enabled = false;
        }

        var colls = GetComponentsInChildren<Collider>();
        foreach (var c in colls) c.enabled = false;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.PlayerDied();
        }
    }

    private IEnumerator HealthRegenRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);
            ApplyHealthRegen();
        }
    }

    private void ApplyHealthRegen()
    {
        if (hpRegen > 0f && currentHp > 0 && currentHp < maxHp)
        {
            Heal(Mathf.CeilToInt(hpRegen));
        }
    }
    #endregion
}