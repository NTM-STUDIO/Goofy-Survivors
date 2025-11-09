using UnityEngine;

[CreateAssetMenu(fileName = "Rune_MoveSpeedPerConsumable", menuName = "GoofySurvivors/Loadout/Runes/Move Speed per Consumable")]
public class MoveSpeedPerConsumableRune : RuneEffect
{
    [Tooltip("Percent of base movement speed gained per consumable collected (0.01 = +1%).")]
    public float percentPerConsumable = 0.01f;

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
        // Additive bump equal to percent of the character's base speed.
        float delta = owner.characterData != null ? owner.characterData.movementSpeed * percentPerConsumable : 0f;
        if (delta > 0f)
        {
            owner.IncreaseMovementSpeed(delta);
        }
    }
}
