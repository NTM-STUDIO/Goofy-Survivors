using UnityEngine;
using System.Collections.Generic;

public class AuraWeapon : MonoBehaviour
{
    // References to live data
    private PlayerStats playerStats;
    private WeaponData weaponData;

    // Internal timer for damage ticks
    private float damageTickCooldown;

    // A list to keep track of all enemies currently inside the aura's trigger
    private List<EnemyStats> enemiesInRange = new List<EnemyStats>();

    /// <summary>
    /// Called ONCE by the WeaponController to link the aura to the player's stats and its data.
    /// </summary>
    public void Initialize(PlayerStats stats, WeaponData data)
    {
        this.playerStats = stats;
        this.weaponData = data;
    }

    void Update()
    {
        // If the references are not set, do nothing.
        if (playerStats == null || weaponData == null)
        {
            return;
        }

        // --- CONTINUOUS STAT UPDATES ---
        // Update the aura's size every frame to reflect any changes in player stats (e.g., from an upgrade).
        float currentSize = weaponData.area * playerStats.projectileSizeMultiplier;
        transform.localScale = Vector3.one * currentSize;
        
        // --- DAMAGE TICK LOGIC ---
        // Countdown the timer for the next damage tick.
        damageTickCooldown -= Time.deltaTime;
        if (damageTickCooldown <= 0f)
        {
            ApplyDamageToEnemies();
            
            // Reset the cooldown based on the weapon's base cooldown and the player's current attack speed.
            // A higher attack speed results in a lower cooldown between damage ticks.
            float finalAttackSpeed = playerStats.attackSpeedMultiplier;
            damageTickCooldown = weaponData.cooldown / Mathf.Max(0.01f, finalAttackSpeed);
        }
    }

    /// <summary>
    /// Applies damage to all enemies currently within the aura's trigger.
    /// This method now includes critical strike calculations for each enemy hit.
    /// </summary>
    private void ApplyDamageToEnemies()
    {
        // Calculate the knockback once, as it will be the same for all enemies this tick.
        float finalKnockback = weaponData.knockback * playerStats.knockbackMultiplier;

        // Iterate backwards through the list. This is safer if an enemy is destroyed and removed.
        for (int i = enemiesInRange.Count - 1; i >= 0; i--)
        {
            EnemyStats enemy = enemiesInRange[i];
            if (enemy != null)
            {
                // --- CRITICAL STRIKE CALCULATION ---
                // For each enemy in range, perform a new, independent damage calculation.
                // This gives each enemy its own chance to be critically hit on this tick.
                DamageResult damageResult = playerStats.CalculateDamage(weaponData.damage);
                
                // Pass the final damage and the crit status to the enemy.
                enemy.TakeDamage(damageResult.damage, damageResult.isCritical);

                if (finalKnockback > 0 && !enemy.CompareTag("Reaper"))
                {
                    Vector3 knockbackDirection = (enemy.transform.position - transform.position).normalized;
                    knockbackDirection.y = 0;
                    enemy.ApplyKnockback(finalKnockback, 0.1f, knockbackDirection);
                }
            }
            else
            {
                // If an enemy was destroyed by another source, remove its null reference from the list.
                enemiesInRange.RemoveAt(i);
            }
        }
    }

    // When an enemy enters the trigger, add it to our list.
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy") || other.CompareTag("Reaper"))
        {
            EnemyStats enemyStats = other.GetComponentInParent<EnemyStats>();
            if (enemyStats != null && !enemiesInRange.Contains(enemyStats))
            {
                enemiesInRange.Add(enemyStats);
            }
        }
    }

    // When an enemy leaves the trigger, remove it from our list.
    void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Enemy") || other.CompareTag("Reaper"))
        {
            EnemyStats enemyStats = other.GetComponentInParent<EnemyStats>();
            if (enemyStats != null)
            {
                enemiesInRange.Remove(enemyStats);
            }
        }
    }
}