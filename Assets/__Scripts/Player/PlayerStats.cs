using UnityEngine;
using Unity.Netcode;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public struct DamageResult
{
    public float damage;
    public bool isCritical;
}

public class ActiveBuff
{
    public MutationType type;
    public float timer;
}

public class PlayerStats : NetworkBehaviour
{
    // --- 1. VARIÁVEL DE REDE (A VERDADE SOBRE O ESTADO) ---
    public NetworkVariable<bool> IsDownedNetVar = new NetworkVariable<bool>(false, 
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Propriedade pública inteligente:
    // Se estivermos ligados à rede, lê da variável de rede.
    // Se for Singleplayer, lê de uma variável local interna.
    private bool _isDownedLocal = false;
    public bool IsDowned 
    {
        get 
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
                return IsDownedNetVar.Value;
            return _isDownedLocal;
        }
    }

    private bool _isLocalOwner = true;

    [Header("Item Effects: Belt Buffs")]
    public bool hasBeltBuffItem = false;
    [SerializeField] private float stolenBuffDuration = 10f;
    [SerializeField] private float beltHealthBonus = 0.2f;
    [SerializeField] private float beltDamageBonus = 0.2f;
    [SerializeField] private float beltSpeedBonus = 1.0f;
    private List<ActiveBuff> activeBuffs = new List<ActiveBuff>();

    [Header("Character Data")]
    public PlayerCharacterData characterData;

    // Stats
    [HideInInspector] public int maxHp;
    [HideInInspector] public float hpRegen;

    // --- COLA ISTO NO FINAL DO PLAYERSTATS.CS (Antes do último '}') ---

    // Métodos Decrease que faltavam para as Runas
    public void DecreaseDamageMultiplier(float amount) { damageMultiplier -= amount; }
    public void DecreaseDurationMultiplier(float amount) { durationMultiplier -= amount; }
    public void DecreasePickupRange(float amount) { pickupRange -= amount; }
    public void DecreaseHealingReceivedMultiplier(float amount) { healingReceivedMultiplier -= amount; }
    
    // Método Increase que faltava
    public void IncreaseHealingReceivedMultiplier(float amount) { healingReceivedMultiplier += amount; }
    [HideInInspector] public float damageMultiplier;
    [HideInInspector] public float critChance;
    [HideInInspector] public float critDamageMultiplier;
    [HideInInspector] public float cooldownReduction;      // NEW: reduces cooldown between attacks (0-0.9 = 0%-90%)
    [HideInInspector] public float attackSpeedMultiplier;  // CHANGED: now affects projectile speed AND aura/melee tick speed
    [HideInInspector] public int projectileCount;
    [HideInInspector] public float projectileSizeMultiplier;
    [HideInInspector] public float durationMultiplier;
    [HideInInspector] public float knockbackMultiplier;    // CHANGED: now penetrates enemy knockback resistance
    [HideInInspector] public float movementSpeed;
    [HideInInspector] public float luck;
    [HideInInspector] public float pickupRange;
    [HideInInspector] public float xpGainMultiplier;
    [HideInInspector] public int pierceCount;
    [HideInInspector] public float totalDamageDealt;
    [HideInInspector] public float totalReaperDamageDealt;

    [Header("Health & Invincibility")]
    [SerializeField] private int currentHp;
    [SerializeField] private float invincibilityDuration = 0.6f;
    [SerializeField] private bool invincible = false;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Sprite downedSprite;
    [ColorUsage(true, true)][SerializeField] private Color hurtFlashColor = new Color(1f, 0.4f, 0.4f, 1f);
    [SerializeField] private float hurtFlashTime = 0.1f;
    
    private Color _originalColor;
    private Sprite _originalSprite;
    private string _originalSortingLayer;

    // Getters para uso externo
    public Sprite DownedSprite => downedSprite;
    public Sprite OriginalSprite => _originalSprite;
    public string OriginalSortingLayer => _originalSortingLayer;

    public event Action<int, int> OnHealthChanged;

    /// <summary>
    /// Records damage dealt by this player. Called by EnemyStats when damage is applied.
    /// In multiplayer, this should be called via RecordDamageClientRpc to update the owner's stats.
    /// </summary>
    public void RecordDamageDealt(float damage)
    {
        if (damage > 0f)
        {
            totalDamageDealt += damage;
        }
    }

    /// <summary>
    /// Records damage dealt specifically to the Reaper boss.
    /// </summary>
    public void RecordReaperDamage(float damage)
    {
        if (damage > 0f)
        {
            totalReaperDamageDealt += damage;
        }
    }

    /// <summary>
    /// ClientRpc to notify the owner client to record damage dealt.
    /// Called by server when this player deals damage to an enemy.
    /// </summary>
    [ClientRpc]
    public void RecordDamageClientRpc(float damage)
    {
        // Only the owner should record their own damage
        if (IsOwner || (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening))
        {
            RecordDamageDealt(damage);
        }
    }

    /// <summary>
    /// ClientRpc to notify the owner client to record damage dealt to Reaper.
    /// Called by server when this player deals damage to the Reaper boss.
    /// </summary>
    [ClientRpc]
    public void RecordReaperDamageClientRpc(float damage)
    {
        // Only the owner should record their own damage
        if (IsOwner || (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening))
        {
            RecordReaperDamage(damage);
        }
    }

    /// <summary>
    /// ClientRpc to notify the owner client to record ability damage.
    /// Called by server when this player deals damage with an ability.
    /// </summary>
    [ClientRpc]
    public void RecordAbilityDamageClientRpc(string abilityKey, float damage, int sourceObjectHash)
    {
        // Only the owner should record their own damage
        if (IsOwner || (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening))
        {
            // Precisamos de uma referência ao GameObject da fonte
            // Em vez de enviar o GameObject (que não pode ser serializado em RPC),
            // registramos diretamente usando o abilityKey
            AbilityDamageTracker.RecordDamageDirectly(abilityKey, damage, this);
        }
    }

    public event Action OnDamaged;
    public event Action OnHealed;
    public event Action OnDeath;

    public bool IsInvincible => invincible;
    public int CurrentHp => currentHp;
    [HideInInspector] public float healingReceivedMultiplier = 1f;

    private void Awake()
    {
        InitializeStats();
        if (GetComponent<TeamWipeAbilityEnabler>() == null)
            gameObject.AddComponent<TeamWipeAbilityEnabler>();
    }

    private void Start()
    {
        StartCoroutine(HealthRegenRoutine());
    }

    // --- 2. CONFIGURAÇÃO DE REDE ---
    public override void OnNetworkSpawn()
    {
        _isLocalOwner = IsOwner;
        // Ouve mudanças na variável para atualizar o sprite
        IsDownedNetVar.OnValueChanged += OnDownedStateChanged;
        // Atualiza estado inicial
        UpdateVisuals(IsDownedNetVar.Value);
    }

    public override void OnNetworkDespawn()
    {
        IsDownedNetVar.OnValueChanged -= OnDownedStateChanged;
    }

    private void OnDownedStateChanged(bool previous, bool current)
    {
        UpdateVisuals(current);
    }

    // --- 3. MÉTODO CENTRAL DE VISUALIZAÇÃO (A Magia Acontece Aqui) ---
    private void UpdateVisuals(bool isDownedState)
    {
        // Atualiza variável local para consistência
        _isDownedLocal = isDownedState;

        if (isDownedState)
        {
            // MORTO
            if (spriteRenderer != null && downedSprite != null)
            {
                spriteRenderer.sprite = downedSprite;
                spriteRenderer.sortingLayerName = "MAPCOSMETIC";
                spriteRenderer.color = Color.gray;
            }
            
            DisableMovementAndPhysics();
        }
        else
        {
            // VIVO
            if (spriteRenderer != null && _originalSprite != null)
            {
                spriteRenderer.sprite = _originalSprite;
                spriteRenderer.color = Color.white;
                if (!string.IsNullOrEmpty(_originalSortingLayer))
                    spriteRenderer.sortingLayerName = _originalSortingLayer;
            }

            EnableMovementAndPhysics();
        }
    }

    private void DisableMovementAndPhysics()
    {
        var movement = GetComponent<Movement>();
        if (movement != null) movement.enabled = false;
        
        var rb = GetComponent<Rigidbody>();
        if (rb != null) rb.linearVelocity = Vector3.zero;

        foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
    }

    private void EnableMovementAndPhysics()
    {
        var movement = GetComponent<Movement>();
        if (movement != null) movement.enabled = true;

        foreach (var c in GetComponentsInChildren<Collider>(true)) c.enabled = true;
    }

    // --- 4. CONTROLO DE ESTADO (Chamado pelo ReviveManager) ---
    public void SetDownedState(bool state)
    {
        if (IsServer)
        {
            IsDownedNetVar.Value = state; // Dispara visual em todos
        }
        else if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            // Singleplayer fallback
            UpdateVisuals(state);
        }
    }

    // --- HANDLE DEATH ---
    private void HandleDeath()
    {
        // Don't handle death for Shadow Clone (handled by its own script)
        if (GetComponent<ShadowClone>() != null) return;

        Debug.Log("Player died.");
        OnDeath?.Invoke();

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            if (IsServer)
            {
                IsDownedNetVar.Value = true; // Server muda, todos veem
            }
            // Cliente espera que o servidor mude a variável via ApplyDamage
        }
        else
        {
            // Singleplayer
            UpdateVisuals(true);
        }

        if (GameManager.Instance != null)
        {
            ulong id = (NetworkObject != null) ? NetworkObject.OwnerClientId : 0;
            GameManager.Instance.PlayerDowned(id);
        }
    }

    // --- CORE GAMEPLAY ---
    public void Heal(int amount)
    {
        if (amount <= 0 || currentHp <= 0) return; // Não cura se estiver morto
        int scaled = Mathf.CeilToInt(amount * Mathf.Max(0f, healingReceivedMultiplier));
        currentHp = Mathf.Clamp(currentHp + scaled, 0, maxHp);
        
        OnHealed?.Invoke();
        OnHealthChanged?.Invoke(currentHp, maxHp);
        UpdateUI();
    }

    public void ApplyDamage(float amount, Vector3? hitFromWorldPos = null, float? customIFrameDuration = null) 
    { 
        if (amount <= 0f || invincible || currentHp <= 0) return; 

        // If this is a Shadow Clone, delegate damage handling to it and STOP here to avoid messing up UI/Network
        var clone = GetComponent<ShadowClone>();
        if (clone != null)
        {
            clone.TakeDamage(amount);
            
            // Visual feedback is okay, but NO networked state changes or UI updates for main player
            if (spriteRenderer != null) { StopCoroutine(nameof(FlashRoutine)); StartCoroutine(FlashRoutine()); } 
            return;
        }
        
        int damageInt = Mathf.CeilToInt(amount); 
        currentHp = Mathf.Clamp(currentHp - damageInt, 0, maxHp); 
        
        if (spriteRenderer != null) { StopCoroutine(nameof(FlashRoutine)); StartCoroutine(FlashRoutine()); } 
        
        OnDamaged?.Invoke(); 
        OnHealthChanged?.Invoke(currentHp, maxHp); 
        
        float iFrames = customIFrameDuration.HasValue ? Mathf.Max(0f, customIFrameDuration.Value) : invincibilityDuration; 
        if (iFrames > 0f) StartCoroutine(InvincibilityRoutine(iFrames)); 
        
        if (currentHp <= 0) HandleDeath(); 
        
        UpdateUI();
    }

    // --- REVIVE HELPERS (Simplificados) ---
    // Estes métodos agora só precisam de atualizar o HP, o estado visual é gerido pelo SetDownedState(false)
    
    public void ServerReviveToFixedHp(int hp)
    {
        currentHp = Mathf.Clamp(hp, 1, maxHp);
        // Nota: O ReviveManager chama SetDownedState(false) separadamente
        OnHealthChanged?.Invoke(currentHp, maxHp);
        UpdateUI();
    }

    public void ClientSyncHp(int hp, int max)
    {
        currentHp = Mathf.Clamp(hp, 0, max);
        OnHealthChanged?.Invoke(currentHp, maxHp);
        UpdateUI();
    }

    // --- COMPATIBILIDADE ---
    public void ClientApplyDownedState() { /* Deprecated, use SetDownedState */ }
    public void ClientApplyRevivedState() { /* Deprecated, use SetDownedState */ }

    // --- INITIALIZATION ---
    private void InitializeStats()
    {
        if (characterData == null) { Debug.LogError("CRITICAL: PlayerCharacterData missing!", this); return; }
        
        // Copiar valores base
        maxHp = characterData.maxHp;
        hpRegen = characterData.hpRegen;
        damageMultiplier = characterData.damageMultiplier;
        critChance = characterData.critChance;
        critDamageMultiplier = characterData.critDamageMultiplier;
        cooldownReduction = characterData.cooldownReduction;
        attackSpeedMultiplier = characterData.attackSpeedMultiplier;
        projectileCount = characterData.projectileCount;
        projectileSizeMultiplier = characterData.projectileSizeMultiplier;
        durationMultiplier = characterData.durationMultiplier;
        knockbackMultiplier = characterData.knockbackMultiplier;
        movementSpeed = characterData.movementSpeed;
        luck = characterData.luck;
        pickupRange = characterData.pickupRange;
        xpGainMultiplier = characterData.xpGainMultiplier;
        pierceCount = (int)characterData.pierceCount;
        totalDamageDealt = 0f;
        totalReaperDamageDealt = 0f;
        
        foreach (var bonus in characterData.startingBonuses) ApplyStatBonus(bonus);
        
        currentHp = maxHp;
        
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null)
        {
            _originalColor = spriteRenderer.color;
            _originalSprite = spriteRenderer.sprite;
            _originalSortingLayer = spriteRenderer.sortingLayerName;
        }
        
        OnHealthChanged?.Invoke(currentHp, maxHp);
        UpdateUI();
    }

    private void UpdateUI()
    {
        // Don't update main UI if this is a Shadow Clone
        if (GetComponent<ShadowClone>() != null) return;

        var nm = NetworkManager.Singleton;
        // In MP, checks IsOwner (and listener). In SP, usually passes as _isLocalOwner is true.
        if (nm == null || !nm.IsListening || _isLocalOwner)
        {
            var uiManager = FindFirstObjectByType<UIManager>();
            if (uiManager != null) uiManager.UpdateHealthBar(currentHp, maxHp);
        }
    }

    // --- BUFFS & SCALING ---
    void Update() { HandleBuffExpiration(); }

    public void AddTemporaryBuff(MutationType type)
    {
        if (type == MutationType.None) return;
        if (type == MutationType.Health) { Heal(20); return; }
        ApplyBuffEffect(type, true);
        activeBuffs.Add(new ActiveBuff { type = type, timer = stolenBuffDuration });
    }

    private void HandleBuffExpiration()
    {
        for (int i = activeBuffs.Count - 1; i >= 0; i--)
        {
            activeBuffs[i].timer -= Time.deltaTime;
            if (activeBuffs[i].timer <= 0)
            {
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
            case MutationType.Health: IncreaseMaxHP(Mathf.RoundToInt(characterData.maxHp * beltHealthBonus * sign)); break;
            case MutationType.Damage: damageMultiplier += beltDamageBonus * sign; break;
            case MutationType.Speed: movementSpeed += beltSpeedBonus * sign; break;
        }
    }

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

    public void ApplyLevelUpScaling()
    {
        if (characterData == null) return;
        foreach (var bonus in characterData.scalingBonusesPerLevel) ApplyStatBonus(bonus);
    }

    private void ApplyStatBonus(StatBonus bonus)
    {
        switch (bonus.stat) {
            case StatType.MaxHP: maxHp += (int)bonus.value; Heal((int)bonus.value); break;
            case StatType.HPRegen: hpRegen += bonus.value; break;
            case StatType.DamageMultiplier: damageMultiplier += bonus.value; break;
            case StatType.CritChance: critChance += bonus.value; break;
            case StatType.CritDamageMultiplier: critDamageMultiplier += bonus.value; break;
            case StatType.CooldownReduction: cooldownReduction = Mathf.Clamp(cooldownReduction + bonus.value, 0f, 0.9f); break;
            case StatType.AttackSpeedMultiplier: attackSpeedMultiplier += bonus.value; break;
            case StatType.ProjectileCount: projectileCount += (int)bonus.value; break;
            case StatType.ProjectileSizeMultiplier: projectileSizeMultiplier += bonus.value; break;
            case StatType.DurationMultiplier: durationMultiplier += bonus.value; break;
            case StatType.KnockbackMultiplier: knockbackMultiplier += bonus.value; break;
            case StatType.MovementSpeed: movementSpeed += bonus.value; break;
            case StatType.Luck: luck += bonus.value; break;
            case StatType.PickupRange: pickupRange += (int)bonus.value; break;
            case StatType.XPGainMultiplier: xpGainMultiplier += bonus.value; break;
        }
    }

    // Setters (Simplificados para poupar espaço, lógica igual)
    public void IncreaseMaxHP(int amount) { maxHp += amount; currentHp += amount; UpdateUI(); OnHealthChanged?.Invoke(currentHp, maxHp); }
    public void IncreaseHPRegen(float amount) { hpRegen += amount; }
    public void IncreaseDamageMultiplier(float amount) { damageMultiplier += amount; }
    public void IncreaseCritChance(float amount) { critChance += amount; }
    public void IncreaseCritDamageMultiplier(float amount) { critDamageMultiplier += amount; }
    public void IncreaseCooldownReduction(float amount) { cooldownReduction = Mathf.Clamp(cooldownReduction + amount, 0f, 0.9f); }
    public void IncreaseAttackSpeedMultiplier(float amount) { attackSpeedMultiplier += amount; }
    public void IncreaseProjectileCount(int amount) { projectileCount += amount; }
    public void IncreaseProjectileSizeMultiplier(float amount) { projectileSizeMultiplier += amount; }
    public void IncreaseDurationMultiplier(float amount) { durationMultiplier += amount; }
    public void IncreaseKnockbackMultiplier(float amount) { knockbackMultiplier += amount; }
    public void IncreaseMovementSpeed(float amount) { movementSpeed += amount; }
    public void IncreaseLuck(float amount) { luck += amount; }
    public void IncreasePickupRange(float amount) { pickupRange += amount; }
    public void IncreaseXPGainMultiplier(float amount)
    {
        var gm = GameManager.Instance;
        if (gm != null && gm.isP2P) gm.RequestModifySharedXpMultiplier(amount);
        else xpGainMultiplier += amount;
    }

    // Helpers
    public void TriggerDamageFeedback(Vector3 hitFromWorldPos, float iFrameDuration)
    {
        if (spriteRenderer != null) { StopCoroutine(nameof(FlashRoutine)); StartCoroutine(FlashRoutine()); }
        OnDamaged?.Invoke();
        if (iFrameDuration > 0f) StartCoroutine(InvincibilityRoutine(iFrameDuration));
    }

    private IEnumerator InvincibilityRoutine(float duration) { invincible = true; yield return new WaitForSeconds(duration); invincible = false; }
    private IEnumerator FlashRoutine() { if (spriteRenderer == null) yield break; spriteRenderer.color = hurtFlashColor; yield return new WaitForSeconds(hurtFlashTime); spriteRenderer.color = _originalColor; }
    private IEnumerator HealthRegenRoutine() { while (true) { yield return new WaitForSeconds(1f); ApplyHealthRegen(); } }
    private void ApplyHealthRegen() { if (hpRegen > 0f && currentHp > 0 && currentHp < maxHp) { Heal(Mathf.CeilToInt(hpRegen)); } }
}