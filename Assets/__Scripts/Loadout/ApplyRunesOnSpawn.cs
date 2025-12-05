using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

// Attach to the player prefab (or added at runtime by GameManager) to apply selected rune bonuses once on spawn.
public class ApplyRunesOnSpawn : NetworkBehaviour
{
    private bool _applied = false;

    void Start()
    {
        TryApply();
    }

    private void TryApply()
    {
        if (_applied) return;

        var gm = GameManager.Instance;
        bool isP2P = (gm != null && gm.isP2P);

        // In MP, only apply on server for authority; in SP, apply locally.
        if (isP2P && !IsServer)
        {
            return;
        }

        var ps = GetComponent<PlayerStats>();
        if (ps == null) return;

        var runes = LoadoutSelections.SelectedRunes;
        if (runes == null || runes.Count == 0) { _applied = true; return; }

        foreach (var rune in runes)
        {
            if (rune == null || rune.bonuses == null) continue;
            foreach (var bonus in rune.bonuses)
            {
                ApplyBonus(ps, bonus);
            }
        }
        // Attach runtime effects (event-driven)
        var runtime = GetComponent<RuneRuntime>();
        if (runtime == null) runtime = gameObject.AddComponent<RuneRuntime>();
        runtime.Initialize(ps, runes);
        _applied = true;
    }

    private void ApplyBonus(PlayerStats ps, StatBonus bonus)
    {
        switch (bonus.stat)
        {
            case StatType.MaxHP: ps.IncreaseMaxHP(Mathf.RoundToInt(bonus.value)); break;
            case StatType.HPRegen: ps.IncreaseHPRegen(bonus.value); break;
            case StatType.DamageMultiplier: ps.IncreaseDamageMultiplier(bonus.value); break;
            case StatType.CritChance: ps.IncreaseCritChance(bonus.value); break;
            case StatType.CritDamageMultiplier: ps.IncreaseCritDamageMultiplier(bonus.value); break;
            case StatType.AttackSpeedMultiplier: ps.IncreaseAttackSpeedMultiplier(bonus.value); break;
            case StatType.ProjectileCount: ps.IncreaseProjectileCount(Mathf.RoundToInt(bonus.value)); break;
            case StatType.ProjectileSizeMultiplier: ps.IncreaseProjectileSizeMultiplier(bonus.value); break;
            case StatType.CooldownReduction: ps.IncreaseCooldownReduction(bonus.value); break;
            case StatType.DurationMultiplier: ps.IncreaseDurationMultiplier(bonus.value); break;
            case StatType.KnockbackMultiplier: ps.IncreaseKnockbackMultiplier(bonus.value); break;
            case StatType.MovementSpeed: ps.IncreaseMovementSpeed(bonus.value); break;
            case StatType.Luck: ps.IncreaseLuck(bonus.value); break;
            case StatType.PickupRange: ps.IncreasePickupRange(Mathf.RoundToInt(bonus.value)); break;
            case StatType.XPGainMultiplier: ps.IncreaseXPGainMultiplier(bonus.value); break;
        }
    }
}
