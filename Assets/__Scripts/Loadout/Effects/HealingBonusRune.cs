using UnityEngine;

[CreateAssetMenu(fileName = "Rune_HealingBonus", menuName = "GoofySurvivors/Loadout/Runes/Healing Bonus")]
public class HealingBonusRune : RuneEffect
{
    [Tooltip("Additive multiplier to all healing received (0.2 = +20%).")]
    public float healingMultiplier = 0.2f;

    public override void OnAttach(PlayerStats owner)
    {
        base.OnAttach(owner);
        if (this.owner != null && healingMultiplier != 0f)
        {
            this.owner.IncreaseHealingReceivedMultiplier(healingMultiplier);
        }
    }

    public override void OnDetach()
    {
        if (owner != null && healingMultiplier != 0f)
        {
            owner.DecreaseHealingReceivedMultiplier(healingMultiplier);
        }
        base.OnDetach();
    }
}
