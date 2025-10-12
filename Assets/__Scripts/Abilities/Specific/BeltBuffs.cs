using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BeltBuffs : MonoBehaviour
{
    // A simplified list of buffs that work with your existing stats
    public enum BuffType
    {
        SoulEater,
        Deadeye,
        Berserker,
        Rejuvenating,
        Consecrator,
        Hasted,
        Assassin
    }

    [Header("References")]
    [Tooltip("Drag the player object with the PlayerStats script here.")]
    public PlayerStats playerStats;

    [Header("Buff Configuration")]
    [Tooltip("Default duration for most buffs in seconds.")]
    public float buffDuration = 20f;

    // --- Buff Specific Values (tweakable in inspector) ---
    [Header("Soul Eater")]
    public float soulEaterSpeedPerStack = 0.05f; // 5%
    private int soulEaterStacks = 0;
    private float currentSoulEaterBonus = 0f;

    [Header("Deadeye (Modified)")]
    public float deadeyeMoveSpeedBonus = 0.5f;
    public float deadeyeProjectileSpeedBonus = 0.5f;
    
    [Header("Berserker (Modified)")]
    public float berserkerDamageBonus = 0.4f;
    public float berserkerSpeedBonus = 0.25f;

    [Header("Rejuvenating / Consecrator")]
    public float rejuvenatingRegenBonus = 20f;
    public float consecratorRegenBonus = 10f;
    public float consecratorCritChanceBonus = 0.1f; // 10% flat crit

    [Header("Hasted")]
    public float hastedSpeedBonus = 0.3f;

    [Header("Assassin")]
    public float assassinCritChanceBonus = 0.15f; // 15% flat crit
    public float assassinCritMultiBonus = 1.0f; // +100% crit multi

    // --- Internal State ---
    private Dictionary<BuffType, Coroutine> activeBuffs = new Dictionary<BuffType, Coroutine>();

    private void Start()
    {
        if (playerStats == null)
        {
            #if UNITY_2023_1_OR_NEWER
            playerStats = UnityEngine.Object.FindFirstObjectByType<PlayerStats>();
            if (playerStats == null)
                playerStats = UnityEngine.Object.FindAnyObjectByType<PlayerStats>();
            #else
            #pragma warning disable 618
            playerStats = FindObjectOfType<PlayerStats>();
            #pragma warning restore 618
            #endif
        }
    }

    /// <summary>
    /// Call this method to grant the player a random buff.
    /// </summary>
    public void GrantRandomBuff()
    {
        var buffValues = System.Enum.GetValues(typeof(BuffType));
        BuffType randomBuff = (BuffType)buffValues.GetValue(Random.Range(0, buffValues.Length));
        
        Debug.Log($"Player gained buff: {randomBuff}");
        ApplyBuff(randomBuff);
    }

    /// <summary>
    /// Called whenever an enemy is killed to stack Soul Eater.
    /// </summary>
    public void OnEnemyKilled()
    {
        if (activeBuffs.ContainsKey(BuffType.SoulEater))
        {
            soulEaterStacks++;
            ApplyBuff(BuffType.SoulEater);
        }
    }

    private void ApplyBuff(BuffType buff)
    {
        if (activeBuffs.ContainsKey(buff))
        {
            StopCoroutine(activeBuffs[buff]);
        }

        Coroutine buffCoroutine = null;
        switch (buff)
        {
            case BuffType.SoulEater:      buffCoroutine = StartCoroutine(SoulEaterCoroutine()); break;
            case BuffType.Deadeye:        buffCoroutine = StartCoroutine(DeadeyeCoroutine()); break;
            case BuffType.Berserker:      buffCoroutine = StartCoroutine(BerserkerCoroutine()); break;
            case BuffType.Rejuvenating:   buffCoroutine = StartCoroutine(RejuvenatingCoroutine()); break;
            case BuffType.Consecrator:    buffCoroutine = StartCoroutine(ConsecratorCoroutine()); break;
            case BuffType.Hasted:         buffCoroutine = StartCoroutine(HastedCoroutine()); break;
            case BuffType.Assassin:       buffCoroutine = StartCoroutine(AssassinCoroutine()); break;
        }

        if (buffCoroutine != null)
        {
            activeBuffs[buff] = buffCoroutine;
        }
    }

    #region Buff Coroutines

    private IEnumerator SoulEaterCoroutine()
    {
        playerStats.DecreaseAttackSpeedMultiplier(currentSoulEaterBonus);
        playerStats.DecreaseMovementSpeed(currentSoulEaterBonus);

        currentSoulEaterBonus = soulEaterStacks * soulEaterSpeedPerStack;
        playerStats.IncreaseAttackSpeedMultiplier(currentSoulEaterBonus);
        playerStats.IncreaseMovementSpeed(currentSoulEaterBonus);
        
        yield return new WaitForSeconds(buffDuration);
        
        playerStats.DecreaseAttackSpeedMultiplier(currentSoulEaterBonus);
        playerStats.DecreaseMovementSpeed(currentSoulEaterBonus);
        soulEaterStacks = 0;
        currentSoulEaterBonus = 0;
        activeBuffs.Remove(BuffType.SoulEater);
    }

    private IEnumerator DeadeyeCoroutine()
    {
        // Accuracy component removed as the stat doesn't exist
        playerStats.IncreaseMovementSpeed(deadeyeMoveSpeedBonus);
        playerStats.IncreaseProjectileSpeedMultiplier(deadeyeProjectileSpeedBonus);
        
        yield return new WaitForSeconds(buffDuration);

        playerStats.DecreaseMovementSpeed(deadeyeMoveSpeedBonus);
        playerStats.DecreaseProjectileSpeedMultiplier(deadeyeProjectileSpeedBonus);
        activeBuffs.Remove(BuffType.Deadeye);
    }
    
    private IEnumerator BerserkerCoroutine()
    {
        // Life Leech component removed as the stat doesn't exist
        playerStats.IncreaseDamageMultiplier(berserkerDamageBonus);
        playerStats.IncreaseAttackSpeedMultiplier(berserkerSpeedBonus);

        yield return new WaitForSeconds(buffDuration);

        playerStats.DecreaseDamageMultiplier(berserkerDamageBonus);
        playerStats.DecreaseAttackSpeedMultiplier(berserkerSpeedBonus);
        activeBuffs.Remove(BuffType.Berserker);
    }

    private IEnumerator RejuvenatingCoroutine()
    {
        // Simplified to only use hpRegen
        playerStats.IncreaseHPRegen(rejuvenatingRegenBonus);

        yield return new WaitForSeconds(buffDuration);

        playerStats.DecreaseHPRegen(rejuvenatingRegenBonus);
        activeBuffs.Remove(BuffType.Rejuvenating);
    }

    private IEnumerator ConsecratorCoroutine()
    {
        playerStats.IncreaseHPRegen(consecratorRegenBonus);
        playerStats.IncreaseCritChance(consecratorCritChanceBonus);

        yield return new WaitForSeconds(buffDuration);

        playerStats.DecreaseHPRegen(consecratorRegenBonus);
        playerStats.DecreaseCritChance(consecratorCritChanceBonus);
        activeBuffs.Remove(BuffType.Consecrator);
    }

    private IEnumerator HastedCoroutine()
    {
        playerStats.IncreaseAttackSpeedMultiplier(hastedSpeedBonus);
        playerStats.IncreaseMovementSpeed(hastedSpeedBonus);

        yield return new WaitForSeconds(buffDuration);

        playerStats.DecreaseAttackSpeedMultiplier(hastedSpeedBonus);
        playerStats.DecreaseMovementSpeed(hastedSpeedBonus);
        activeBuffs.Remove(BuffType.Hasted);
    }

    private IEnumerator AssassinCoroutine()
    {
        playerStats.IncreaseCritChance(assassinCritChanceBonus);
        playerStats.IncreaseCritDamageMultiplier(assassinCritMultiBonus);

        yield return new WaitForSeconds(buffDuration);

        playerStats.DecreaseCritChance(assassinCritChanceBonus);
        playerStats.DecreaseCritDamageMultiplier(assassinCritMultiBonus);
        activeBuffs.Remove(BuffType.Assassin);
    }

    #endregion
}