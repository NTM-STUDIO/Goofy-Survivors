using UnityEngine;

[CreateAssetMenu(fileName = "Rune_HealOnConsumable", menuName = "GoofySurvivors/Loadout/Runes/Heal on Consumable")]
public class HealingBurstOnConsumableRune : RuneEffect
{
    [Tooltip("Heals this percent of max HP on each consumable pickup (0.2 = 20% of max HP).")]
    public float percentOfMaxHp = 0.1f;

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
        if (percentOfMaxHp <= 0f) return;
        int amount = Mathf.CeilToInt(owner.maxHp * percentOfMaxHp);
        if (amount > 0) owner.Heal(amount);
    }
}
