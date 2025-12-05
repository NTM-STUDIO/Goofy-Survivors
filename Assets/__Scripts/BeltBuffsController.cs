using UnityEngine;

/// <summary>
/// This controller listens for when enemies are damaged and handles the logic for the "Belt Buff" item.
/// It should be placed on the Player prefab, ideally on the same object as PlayerStats or a child of it.
/// </summary>
public class BeltBuffController : MonoBehaviour
{
    private PlayerStats playerStats;

    void Awake()
    {
        // Find the PlayerStats component on this object or any of its parents.
        playerStats = GetComponentInParent<PlayerStats>();

        // If no PlayerStats component is found, log an error and disable this script.
        if (playerStats == null)
        {
            Debug.LogError("BeltBuffController could not find a PlayerStats component on this object or its parents! The script will be disabled.", this);
            enabled = false;
            return;
        }
    }

    // Subscribe to the event when this component is enabled.
    void OnEnable()
    {
        EnemyStats.OnEnemyDamaged += HandleEnemyDamaged;
    }

    // Unsubscribe from the event when this component is disabled to prevent memory leaks.
    void OnDisable()
    {
        EnemyStats.OnEnemyDamaged -= HandleEnemyDamaged;
    }

    /// <summary>
    /// This method is called automatically whenever ANY enemy in the game takes damage.
    /// </summary>
    private void HandleEnemyDamaged(EnemyStats damagedEnemy)
    {
        // 1. Check if the player has the belt item. If not, do nothing.
        if (!playerStats.hasBeltBuffItem)
        {
            return;
        }

        // 2. Check if the damaged enemy has a mutation to steal.
        if (damagedEnemy.CurrentMutation != MutationType.None)
        {
            // 3. Steal the mutation and add the temporary buff to the player.
            MutationType stolenType = damagedEnemy.StealMutation();
            playerStats.AddTemporaryBuff(stolenType);
        }
    }
}