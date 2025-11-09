using UnityEngine;

[CreateAssetMenu(fileName = "Rune_LuckPerConsumable", menuName = "GoofySurvivors/Loadout/Runes/Luck per Consumable")]
public class LuckPerConsumableRune : RuneEffect
{
    [Tooltip("Luck added to the owner each time they collect a consumable.")]
    public float luckPerPickup = 1f;

    public override void OnAttach(PlayerStats owner)
    {
        base.OnAttach(owner);
        MapConsumable.OnAnyConsumableCollected += HandleConsumableCollected;
    }

    public override void OnDetach()
    {
        MapConsumable.OnAnyConsumableCollected -= HandleConsumableCollected;
        base.OnDetach();
    }

    private void HandleConsumableCollected(PlayerStats collector)
    {
        if (collector == null || owner == null) return;
        if (collector != owner) return;
        if (luckPerPickup != 0f)
        {
            owner.IncreaseLuck(luckPerPickup);
        }
    }
}
