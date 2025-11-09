using UnityEngine;

[CreateAssetMenu(fileName = "DurationBonusRune", menuName = "GoofySurvivors/Loadout/Runes/Duration Bonus Rune")]
public class DurationBonusRune : RuneEffect
{
    [Header("Duration Bonus")]
    [Tooltip("Flat addition to the owner's duration multiplier (e.g., 0.20 = +20%).")]
    public float durationBonus = 0.20f;

    public override void OnAttach(PlayerStats owner)
    {
        base.OnAttach(owner);
        if (this.owner != null)
        {
            this.owner.IncreaseDurationMultiplier(durationBonus);
        }
    }

    public override void OnDetach()
    {
        if (this.owner != null)
        {
            this.owner.DecreaseDurationMultiplier(durationBonus);
        }
        base.OnDetach();
    }
}
