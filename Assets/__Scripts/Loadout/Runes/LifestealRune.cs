using UnityEngine;

[CreateAssetMenu(fileName = "LifestealRune", menuName = "GoofySurvivors/Loadout/Runes/Lifesteal Rune")]
public class LifestealRune : RuneEffect
{
    [Header("Lifesteal Settings")]
    [Tooltip("Fraction of damage dealt converted to healing (0.05 = 5%).")]
    [Range(0f, 1f)] public float lifestealPercent = 0.05f;
    [Tooltip("Minimum heal amount per proc (after scaling). 0 to disable floor.")]
    public int minHeal = 0;

    public override void OnAttach(PlayerStats owner)
    {
        base.OnAttach(owner);
        EnemyStats.OnEnemyDamagedWithAmount += HandleEnemyDamaged;
    }

    public override void OnDetach()
    {
        EnemyStats.OnEnemyDamagedWithAmount -= HandleEnemyDamaged;
        base.OnDetach();
    }

    private void HandleEnemyDamaged(EnemyStats enemy, float damage, PlayerStats attacker)
    {
        // Runs on server in MP and locally in SP
        if (owner == null || attacker == null) return;
        if (attacker != owner) return; // only heal our owner when they are the attacker
        if (damage <= 0f) return;

        float raw = damage * Mathf.Max(0f, lifestealPercent);
        int healAmount = Mathf.CeilToInt(raw);
        if (minHeal > 0) healAmount = Mathf.Max(minHeal, healAmount);
        if (healAmount <= 0) return;

        owner.Heal(healAmount);
    }
}
