using UnityEngine;
using Unity.Netcode;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

// A struct to hold the result of a damage calculation, including the final damage and whether it was a critical hit.
public struct DamageResult
{
    public float damage;
    public bool isCritical;
}

// A simple class to keep track of a temporary buff and its timer.
public class ActiveBuff
{
    public MutationType type;
    public float timer;
}

public class PlayerStats : MonoBehaviour
{
    private bool _isLocalOwner = true; // default true for single-player
    // --- NEW SECTION: BELT BUFFS (STACKING) ---
    [Header("Item Effects: Belt Buffs")]
    [Tooltip("Set to true by your UpgradeManager when the player acquires the belt item.")]
    public bool hasBeltBuffItem = false;

    [Tooltip("The duration of a single stolen buff stack in seconds.")]
    [SerializeField] private float stolenBuffDuration = 10f;

    [Header("Stolen Buff Bonuses (Per Stack)")]
    [Tooltip("The percentage bonus to max health when a Health buff is stolen (0.2 = +20%).")]
    [SerializeField] private float beltHealthBonus = 0.2f;
    [Tooltip("The percentage bonus to damage when a Damage buff is stolen (0.2 = +20%).")]
    [SerializeField] private float beltDamageBonus = 0.2f;
    [Tooltip("The additive bonus to move speed when a Speed buff is stolen.")]
    [SerializeField] private float beltSpeedBonus = 1.0f;

    // This list will now hold every individual stack of a buff.
    private List<ActiveBuff> activeBuffs = new List<ActiveBuff>();
    // --- END OF NEW SECTION ---

    [Header("Character Data")]
    public PlayerCharacterData characterData;

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
    [SerializeField] private int currentHp;
    [SerializeField] private float invincibilityDuration = 0.6f;
    [SerializeField] private bool invincible = false;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [Tooltip("Sprite shown while the player is downed (0 HP). Leave empty to keep current sprite.")]
    [SerializeField] private Sprite downedSprite;
    [ColorUsage(true, true)][SerializeField] private Color hurtFlashColor = new Color(1f, 0.4f, 0.4f, 1f);
    [SerializeField] private float hurtFlashTime = 0.1f;
    private Color _originalColor;
    private Sprite _originalSprite;
    private string _originalSortingLayer;

    // Public getters for visual assets used by GameManager in MP
    public Sprite DownedSprite => downedSprite;
    public Sprite OriginalSprite => _originalSprite;
    public string OriginalSortingLayer => _originalSortingLayer;

    public event Action<int, int> OnHealthChanged;
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
        // Determine local ownership if Netcode is active
        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening)
        {
            var netObj = GetComponent<NetworkObject>();
            _isLocalOwner = (netObj != null && netObj.IsOwner);
        }
        StartCoroutine(HealthRegenRoutine());
    }

    // --- NEW: UPDATE METHOD TO HANDLE BUFF TIMERS ---
    void Update()
    {
        HandleBuffExpiration();
    }

    // --- NEW: METHODS FOR THE STACKING BUFF SYSTEM ---
    public void AddTemporaryBuff(MutationType type)
    {
        if (type == MutationType.None) return;

        // --- NOVA LÓGICA: Lida com a Mutação de Vida como uma cura permanente ---
        if (type == MutationType.Health)
        {
            int healAmount = 20;
            Heal(healAmount);
            Debug.Log($"Stole Health Mutation! Healed for {healAmount} HP permanently.");
            return; // IMPORTANTE: Sai do método para não ser tratado como um buff temporário.
        }
        // --- FIM DA NOVA LÓGICA ---

        // A lógica original para os buffs de Dano e Velocidade continua a mesma.
        ApplyBuffEffect(type, true);
        ActiveBuff newBuffStack = new ActiveBuff { type = type, timer = stolenBuffDuration };
        activeBuffs.Add(newBuffStack);
        int stackCount = activeBuffs.Count(b => b.type == type);
        Debug.Log($"Gained {type} buff! Now at {stackCount} stacks.");
    }

    private void HandleBuffExpiration()
    {
        for (int i = activeBuffs.Count - 1; i >= 0; i--)
        {
            activeBuffs[i].timer -= Time.deltaTime;
            if (activeBuffs[i].timer <= 0)
            {
                Debug.Log($"A {activeBuffs[i].type} buff stack has expired.");
                ApplyBuffEffect(activeBuffs[i].type, false);
                activeBuffs.RemoveAt(i);
            }
        }
    }

    private void ApplyBuffEffect(MutationType type, bool apply)
    {
        int sign = apply ? 1 : -1;
        switch (type)
        {
            case MutationType.Health:
                int healthChange = Mathf.RoundToInt(characterData.maxHp * beltHealthBonus * sign);
                IncreaseMaxHP(healthChange);
                break;
            case MutationType.Damage:
                damageMultiplier += beltDamageBonus * sign;
                break;
            case MutationType.Speed:
                movementSpeed += beltSpeedBonus * sign;
                break;
        }
    }
    // --- END OF NEW METHODS ---

    public DamageResult CalculateDamage(float baseDamage)
    {
        float finalDamage = baseDamage * this.damageMultiplier;
        bool isCritical = false;
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
        if (characterData == null) { Debug.LogError("CRITICAL: PlayerCharacterData is not assigned!", this); return; }
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
        foreach (var bonus in characterData.startingBonuses) { ApplyStatBonus(bonus); }
        currentHp = maxHp;
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            _originalColor = spriteRenderer.color;
            _originalSprite = spriteRenderer.sprite;
            _originalSortingLayer = spriteRenderer.sortingLayerName;
        }
        OnHealthChanged?.Invoke(currentHp, maxHp);
        // Update local HUD only for the owning client (or in single-player)
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening || _isLocalOwner)
        {
            var uiManager = FindFirstObjectByType<UIManager>();
            if (uiManager != null) uiManager.UpdateHealthBar(currentHp, maxHp);
        }
    }

    public void ApplyLevelUpScaling()
    {
        if (characterData == null) return;
        foreach (var bonus in characterData.scalingBonusesPerLevel) { ApplyStatBonus(bonus); }
        Debug.Log("Applied level up scaling bonuses.");
    }

    private void ApplyStatBonus(StatBonus bonus)
    {
        switch (bonus.stat)
        {
            case StatType.MaxHP: maxHp += (int)bonus.value; Heal((int)bonus.value); break;
            case StatType.HPRegen: hpRegen += bonus.value; break;
            case StatType.DamageMultiplier: damageMultiplier += bonus.value; break;
            case StatType.CritChance: critChance = Mathf.Clamp01(critChance + bonus.value); break;
            case StatType.CritDamageMultiplier: critDamageMultiplier += bonus.value; break;
            case StatType.AttackSpeedMultiplier: attackSpeedMultiplier += bonus.value; break;
            case StatType.ProjectileCount: projectileCount += (int)bonus.value; break;
            case StatType.ProjectileSizeMultiplier: projectileSizeMultiplier += bonus.value; break;
            case StatType.ProjectileSpeedMultiplier: projectileSpeedMultiplier += bonus.value; break;
            case StatType.DurationMultiplier: durationMultiplier += bonus.value; break;
            case StatType.KnockbackMultiplier: knockbackMultiplier += bonus.value; break;
            case StatType.MovementSpeed: movementSpeed += bonus.value; break;
            case StatType.Luck: luck += bonus.value; break;
            case StatType.PickupRange: pickupRange += (int)bonus.value; break;
            case StatType.XPGainMultiplier: xpGainMultiplier += bonus.value; break;
            default: Debug.LogWarning($"Stat bonus for {bonus.stat} not implemented."); break;
        }
    }

    public void IncreaseMaxHP(int amount)
    {
        maxHp += amount;
        currentHp += amount;
        // Update HUD only for local owner (or SP)
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening || _isLocalOwner)
        {
            var uiManager = FindFirstObjectByType<UIManager>();
            if (uiManager != null) uiManager.UpdateHealthBar(currentHp, maxHp);
        }
        OnHealthChanged?.Invoke(currentHp, maxHp);
    }
    public void IncreaseHPRegen(float amount) { hpRegen += amount; }
    public void IncreaseDamageMultiplier(float amount) { damageMultiplier += amount; }
    public void IncreaseCritChance(float amount) { critChance += amount; }
    public void IncreaseCritDamageMultiplier(float amount) { critDamageMultiplier += amount; }
    public void IncreaseAttackSpeedMultiplier(float amount) { attackSpeedMultiplier += amount; }
    public void IncreaseProjectileCount(int amount) { projectileCount += amount; }
    public void IncreaseProjectileSizeMultiplier(float amount) { projectileSizeMultiplier += amount; if (projectileSizeMultiplier > 4f) projectileSizeMultiplier = 4f; Debug.Log($"Projectile Size Multiplier increased to {projectileSizeMultiplier}"); }
    public void IncreaseProjectileSpeedMultiplier(float amount) { projectileSpeedMultiplier += amount; }
    public void IncreaseDurationMultiplier(float amount) { durationMultiplier += amount; }
    public void IncreaseKnockbackMultiplier(float amount) { knockbackMultiplier += amount; }
    public void IncreaseMovementSpeed(float amount) { movementSpeed += amount; }
    public void IncreaseLuck(float amount) {  luck += amount; if (luck > 500) luck = 500; }
    public void IncreasePickupRange(float amount) { pickupRange += amount; }
    public void IncreaseXPGainMultiplier(float amount)
    {
        var gm = GameManager.Instance;
        if (gm != null && gm.isP2P)
        {
            // In multiplayer, XP gain is shared for all players: update team multiplier on server
            gm.RequestModifySharedXpMultiplier(amount);
            // Optionally mirror into local field for UI display; will be overridden by StatsPanel in P2P
            xpGainMultiplier += amount;
        }
        else
        {
            // Single-player: local only
            xpGainMultiplier += amount;
        }
    }
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
    public void PrintStats() { /* ... */ }
    public void Heal(int amount) { if (amount <= 0 || currentHp <= 0) return; int prev = currentHp; currentHp = Mathf.Clamp(currentHp + amount, 0, maxHp); if (currentHp != prev) { OnHealed?.Invoke(); OnHealthChanged?.Invoke(currentHp, maxHp); var nm = NetworkManager.Singleton; if (nm == null || !nm.IsListening || _isLocalOwner) { var uiManager = FindFirstObjectByType<UIManager>(); if (uiManager != null) { uiManager.UpdateHealthBar(currentHp, maxHp); } } } }
    public void ApplyDamage(float amount, Vector3? hitFromWorldPos = null, float? customIFrameDuration = null) { if (amount <= 0f || invincible || currentHp <= 0) return; int damageInt = Mathf.CeilToInt(amount); currentHp = Mathf.Clamp(currentHp - damageInt, 0, maxHp); if (spriteRenderer != null) { StopCoroutine(nameof(FlashRoutine)); StartCoroutine(FlashRoutine()); } OnDamaged?.Invoke(); OnHealthChanged?.Invoke(currentHp, maxHp); float iFrames = customIFrameDuration.HasValue ? Mathf.Max(0f, customIFrameDuration.Value) : invincibilityDuration; if (iFrames > 0f) StartCoroutine(InvincibilityRoutine(iFrames)); if (currentHp <= 0) { HandleDeath(); } var nm = NetworkManager.Singleton; if (nm == null || !nm.IsListening || _isLocalOwner) { var uiManager = FindFirstObjectByType<UIManager>(); if (uiManager != null) uiManager.UpdateHealthBar(currentHp, maxHp); } }
    public void BeginInvincibility(float duration) { if (duration <= 0f) return; StopCoroutine(nameof(InvincibilityRoutine)); StartCoroutine(InvincibilityRoutine(duration)); }
    private IEnumerator InvincibilityRoutine(float duration) { invincible = true; yield return new WaitForSeconds(duration); invincible = false; }
    private IEnumerator FlashRoutine() { if (spriteRenderer == null) yield break; spriteRenderer.color = hurtFlashColor; yield return new WaitForSeconds(hurtFlashTime); spriteRenderer.color = _originalColor; }
    public bool IsDowned { get; private set; } = false;
    private void HandleDeath()
    {
        Debug.Log("Player died.");
        OnDeath?.Invoke();
        IsDowned = true;
        // Immediately zero movement to avoid continued sliding this frame
    var rb = GetComponent<Rigidbody>();
    if (rb != null) rb.linearVelocity = Vector3.zero;
        if (spriteRenderer != null && downedSprite != null)
        {
            spriteRenderer.sprite = downedSprite;
            // Move to map cosmetic sorting layer while downed for clarity
            spriteRenderer.sortingLayerName = "MAPCOSMETIC";
        }
        var movement = GetComponent<Movement>();
        if (movement != null) { movement.enabled = false; }
        var colls = GetComponentsInChildren<Collider>();
        foreach (var c in colls) c.enabled = false;
        // Notify GameManager for revive/gameover logic
        var netObj = GetComponent<NetworkObject>();
        if (GameManager.Instance != null && netObj != null)
        {
            GameManager.Instance.PlayerDowned(netObj.OwnerClientId);
        }
    }

    // CLIENT-SIDE helpers to keep local owner in sync with server downed state
    public void ClientApplyDownedState()
    {
        IsDowned = true;
        // Immediately zero movement to avoid continued sliding this frame (owner client)
    var rb = GetComponent<Rigidbody>();
    if (rb != null) rb.linearVelocity = Vector3.zero;
        var movement = GetComponent<Movement>();
        if (movement != null) { movement.enabled = false; }
        var colls = GetComponentsInChildren<Collider>();
        foreach (var c in colls) c.enabled = false;
    }

    public void ClientApplyRevivedState()
    {
        IsDowned = false;
        var movement = GetComponent<Movement>();
        if (movement != null) { movement.enabled = true; }
        var colls = GetComponentsInChildren<Collider>(true);
        foreach (var c in colls) c.enabled = true;
    }

    // Server-only revive helper
    public void ServerReviveToPercent(float percent)
    {
        percent = Mathf.Clamp01(percent);
        currentHp = Mathf.Max(1, Mathf.RoundToInt(maxHp * percent));
        IsDowned = false;
        if (spriteRenderer != null && _originalSprite != null)
        {
            spriteRenderer.sprite = _originalSprite;
            if (!string.IsNullOrEmpty(_originalSortingLayer))
            {
                spriteRenderer.sortingLayerName = _originalSortingLayer;
            }
        }
        var movement = GetComponent<Movement>();
        if (movement != null) { movement.enabled = true; }
        var colls = GetComponentsInChildren<Collider>(true);
        foreach (var c in colls) c.enabled = true;
        OnHealed?.Invoke();
        OnHealthChanged?.Invoke(currentHp, maxHp);
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsListening || _isLocalOwner)
        {
            var uiManager = FindFirstObjectByType<UIManager>();
            if (uiManager != null) uiManager.UpdateHealthBar(currentHp, maxHp);
        }
    }
    private IEnumerator HealthRegenRoutine() { while (true) { yield return new WaitForSeconds(1f); ApplyHealthRegen(); } }
    private void ApplyHealthRegen() { if (hpRegen > 0f && currentHp > 0 && currentHp < maxHp) { Heal(Mathf.CeilToInt(hpRegen)); } }
    #endregion
}