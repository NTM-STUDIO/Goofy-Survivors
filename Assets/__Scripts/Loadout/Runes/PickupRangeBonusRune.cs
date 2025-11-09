using UnityEngine;

[CreateAssetMenu(fileName = "PickupRangeBonusRune", menuName = "GoofySurvivors/Loadout/Runes/Pickup Range Bonus Rune")]
public class PickupRangeBonusRune : RuneEffect
{
    [Header("Pickup Range Bonus")]
    [Tooltip("Flat addition to the owner's pickup range radius in world units.")]
    public float pickupRangeBonus = 1.5f;

    public override void OnAttach(PlayerStats owner)
    {
        base.OnAttach(owner);
        if (this.owner != null)
        {
            this.owner.IncreasePickupRange(pickupRangeBonus);
        }
    }

    public override void OnDetach()
    {
        if (this.owner != null)
        {
            this.owner.DecreasePickupRange(pickupRangeBonus);
        }
        base.OnDetach();
    }
}
