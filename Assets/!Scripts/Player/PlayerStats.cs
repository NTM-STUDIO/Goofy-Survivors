using UnityEngine;
using System;

public class PlayerStats : MonoBehaviour
{
    [Header("Character Data")]
    public PlayerCharacterData characterData;

    // --- Your Original Stat Fields ---
    [HideInInspector] public int maxHp;
    [Tooltip("HP regenerated per second from stats and upgrades")]
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

    [Header("Health & Invincibility")]
    [Tooltip("Current HP of the player (clamped to [0, maxHp])")]
    [SerializeField] private int currentHp;
    [Tooltip("Duration of invincibility frames after taking a hit (seconds)")]
    [Min(0f)]
    [SerializeField] private float invincibilityDuration = 0.6f;
    [Tooltip("Whether the player is currently invincible (i-frames)")]
    [SerializeField] private bool invincible = false;
    [Tooltip("Optional flash on damage")] [SerializeField] private SpriteRenderer spriteRenderer;
    [ColorUsage(true,true)] [SerializeField] private Color hurtFlashColor = new Color(1f, 0.4f, 0.4f, 1f);
    [SerializeField] private float hurtFlashTime = 0.1f;
    private Color _originalColor;

    [Header("Health Regeneration")]
    [Tooltip("Enable or disable passive health regeneration")]
    [SerializeField] private bool enableHealthRegen = true;

    [Tooltip("Flat health restored each tick before applying hpRegen stat (useful for designer tweaks)")]
    [Min(0f)]
    [SerializeField] public float hpRegen = 1f;
        /*[SerializeField] private float baseRegenPerTick = 0f;
    [Tooltip("Seconds between regeneration ticks")]
    [Min(0.1f)]*/

    [Tooltip("Seconds between regeneration ticks")]
    [Min(0.1f)]
    
    [SerializeField] private float regenTickInterval = 1f;
    private Coroutine _regenRoutine;
    private float _regenOverflow;

    // Events for UI and gameplay hooks
    public event Action<int, int> OnHealthChanged; // (current, max)
    public event Action OnDamaged;
    public event Action OnHealed;
    public event Action OnDeath;

    public bool IsInvincible => invincible;
    public int CurrentHp => currentHp;

    private void Awake()
    {
        if (characterData != null)
        {
            maxHp = characterData.maxHp; //Blouso
            hpRegen = characterData.hpRegen; //Blouso
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
        }

        // Initialize health and visuals
        currentHp = Mathf.Max(0, maxHp);
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null) _originalColor = spriteRenderer.color;

        // Raise initial health event
        OnHealthChanged?.Invoke(currentHp, maxHp);

        TryStartHealthRegen();
    }

    // --- Public Methods to INCREASE Stats ---
    public void IncreaseMaxHP(int amount) { maxHp += amount; }
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

    // --- Public Methods to DECREASE Stats (Essential for removing buffs) ---
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


    public void PrintStats()
    {
        Debug.Log("Stats Initialized from Character Data");
        Debug.Log($"MaxHP: {maxHp}, HPRegen: {hpRegen}, DamageMultiplier: {damageMultiplier}");
        Debug.Log($"CritChance: {critChance}, CritDamageMultiplier: {critDamageMultiplier}, AttackSpeedMultiplier: {attackSpeedMultiplier}");
        Debug.Log($"ProjectileCount: {projectileCount}, ProjectileSizeMultiplier: {projectileSizeMultiplier}, ProjectileSpeedMultiplier: {projectileSpeedMultiplier}");
        Debug.Log($"DurationMultiplier: {durationMultiplier}, KnockbackMultiplier: {knockbackMultiplier}, MovementSpeed: {movementSpeed}");
        Debug.Log($"Luck: {luck}, PickupRange: {pickupRange}, XPGainMultiplier: {xpGainMultiplier}");
    }

    // --- Health API ---
    public void Heal(int amount)
    {
        if (amount <= 0 || currentHp <= 0) return;
        int prev = currentHp;
        currentHp = Mathf.Clamp(currentHp + amount, 0, maxHp);
        if (currentHp != prev)
        {
            Debug.Log($"Player healed {currentHp - prev} HP (current: {currentHp}/{maxHp})");
            OnHealed?.Invoke();
            OnHealthChanged?.Invoke(currentHp, maxHp);
        }
    }

    public void ApplyDamage(float amount, Vector2? hitFromWorldPos = null, float? customIFrameDuration = null)
    {
        if (amount <= 0f) return;
        if (invincible) return;
        if (currentHp <= 0) return;

        int damageInt = Mathf.CeilToInt(amount);
        currentHp = Mathf.Clamp(currentHp - damageInt, 0, maxHp);

        // Feedback
        if (spriteRenderer != null)
        {
            StopCoroutine(nameof(FlashRoutine));
            StartCoroutine(FlashRoutine());
        }
        OnDamaged?.Invoke();
        OnHealthChanged?.Invoke(currentHp, maxHp);
        
        // Begin invincibility frames
        float iFrames = customIFrameDuration.HasValue ? Mathf.Max(0f, customIFrameDuration.Value) : invincibilityDuration;
        if (iFrames > 0f) BeginInvincibility(iFrames);

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

    private void TryStartHealthRegen()
    {
        if (_regenRoutine != null)
        {
            StopCoroutine(_regenRoutine);
            _regenRoutine = null;
        }

        if (!enableHealthRegen || regenTickInterval <= 0f) return;
        if (!isActiveAndEnabled) return;

        _regenRoutine = StartCoroutine(HealthRegenRoutine());
    }

    private void OnEnable()
    {
        TryStartHealthRegen();
    }

    private void OnDisable()
    {
        if (_regenRoutine != null)
        {
            StopCoroutine(_regenRoutine);
            _regenRoutine = null;
        }
    }

    private void OnValidate()
    {
        regenTickInterval = Mathf.Max(0.1f, regenTickInterval);
        //baseRegenPerTick = Mathf.Max(0f, baseRegenPerTick);

        if (Application.isPlaying)
        {
            TryStartHealthRegen();
        }
    }

    private System.Collections.IEnumerator HealthRegenRoutine()
    {
        var wait = new WaitForSeconds(regenTickInterval);

        while (true)
        {
            yield return wait;
            ProcessRegenTick();
        }
    }

    private void ProcessRegenTick()
    {
        if (currentHp <= 0 || currentHp >= maxHp) return;

        float regenFromStatsPerTick = Mathf.Max(0f, hpRegen) * regenTickInterval;
        float totalRegen = /*baseRegenPerTick + */regenFromStatsPerTick;
        if (totalRegen <= 0f) return;

        _regenOverflow += totalRegen;
        int healAmount = Mathf.FloorToInt(_regenOverflow);
        if (healAmount <= 0) return;

        _regenOverflow -= healAmount;
        Debug.Log($"Health regen tick (+{healAmount} HP) | basePerTick={hpRegen} | statPerSec={hpRegen} | interval={regenTickInterval}s");
        Heal(healAmount);
    }

    public float GetRegenPerSecond()
    {
        if (!enableHealthRegen || regenTickInterval <= 0f) return 0f;
        return (hpRegen / regenTickInterval) + Mathf.Max(0f, hpRegen);
    }

    private System.Collections.IEnumerator InvincibilityRoutine(float duration)
    {
        invincible = true;
        yield return new WaitForSeconds(duration);
        invincible = false;
    }

    private System.Collections.IEnumerator FlashRoutine()
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

        // Optionally disable movement/collisions here; GameManager can listen to OnDeath to handle game over
        var movement = GetComponent<Movement>();
        if (movement != null)
        {
            movement.enabled = false;
        }

        var colls = GetComponentsInChildren<Collider2D>();
        foreach (var c in colls) c.enabled = false;

        UIManager uiManager = FindFirstObjectByType<UIManager>();
        if (uiManager != null)
        {
            uiManager.ShowEndGamePanel(true);
        }
    }

}