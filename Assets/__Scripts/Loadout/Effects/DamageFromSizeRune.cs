using UnityEngine;

[CreateAssetMenu(fileName = "Rune_DamageFromSize", menuName = "GoofySurvivors/Loadout/Runes/Damage From Ability Size")]
public class DamageFromSizeRune : RuneEffect
{
    [Tooltip("Additional damage multiplier granted per +100% ability size (e.g., 0.2 = +20% damage for doubling size).")]
    public float damagePer100PercentSize = 0.2f;

    private float _baseSize = 1f;
    private float _appliedBonus = 0f;

    public override void OnAttach(PlayerStats owner)
    {
        base.OnAttach(owner);
        _appliedBonus = 0f;
        _baseSize = (owner != null && owner.projectileSizeMultiplier > 0f) ? owner.projectileSizeMultiplier : 1f;
        // Apply immediately at attach
        RecomputeAndApply();
    }

    public override void OnDetach()
    {
        // Remove any applied bonus
        if (owner != null && _appliedBonus != 0f)
        {
            owner.DecreaseDamageMultiplier(_appliedBonus);
        }
        _appliedBonus = 0f;
        base.OnDetach();
    }

    public override void OnTick(float deltaTime)
    {
        // In case other systems modify size during the run, keep damage linked
        RecomputeAndApply();
    }

    private void RecomputeAndApply()
    {
        if (owner == null) return;
        if (_baseSize <= 0f) _baseSize = 1f;
        float sizeNow = Mathf.Max(0.0001f, owner.projectileSizeMultiplier);
        float scale = sizeNow / _baseSize; // 1.0 = baseline
        float sizeDeltaRatio = Mathf.Max(0f, scale - 1f); // only reward increases
        float desiredBonus = sizeDeltaRatio * damagePer100PercentSize; // linear mapping
        float diff = desiredBonus - _appliedBonus;
        if (Mathf.Abs(diff) > 0.0001f)
        {
            if (diff > 0f) owner.IncreaseDamageMultiplier(diff); else owner.DecreaseDamageMultiplier(-diff);
            _appliedBonus = desiredBonus;
        }
    }
}
