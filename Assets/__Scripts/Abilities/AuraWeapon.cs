using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// This is a LOCAL-ONLY script. It is instantiated by the owner's WeaponController and
/// continuously updates its size and damage based on the owner's PlayerStats.
/// It does NOT need to be a NetworkObject.
/// </summary>
public class AuraWeapon : MonoBehaviour
{
    // References to live data
    private PlayerStats playerStats;
    private WeaponData weaponData;
    private NetworkedPlayerStatsTracker tracker; // optional: used for synced visuals when owner is remote

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

    // Overload for remote-owner visuals: use tracker for size/knockback multipliers
    public void Initialize(NetworkedPlayerStatsTracker syncedTracker, WeaponData data)
    {
        this.tracker = syncedTracker;
        this.weaponData = data;
    }

    void Update()
    {
        // Require weapon data; allow missing playerStats when using tracker for remote-owner visuals
        if (weaponData == null)
        {
            return;
        }

        // --- CONTINUOUS STAT UPDATES ---
        // Update the aura's size every frame to reflect any changes in stats (tracker preferred for remote owners).
        float sizeMult = 1f;
        if (tracker != null) sizeMult = tracker.ProjectileSize.Value;
        else if (playerStats != null) sizeMult = playerStats.projectileSizeMultiplier;
        float currentSize = weaponData.area * sizeMult;
        transform.localScale = Vector3.one * currentSize;

        // In multiplayer, the server-only ServerAura applies damage.
        // Local AuraWeapon should be visual-only when a network session is active.
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            return;
        }

        // --- DAMAGE TICK LOGIC (single-player only) ---
        damageTickCooldown -= Time.deltaTime;
        if (damageTickCooldown <= 0f)
        {
            ApplyDamageToEnemies();

            // Reset the cooldown based on the player's current attack speed.
            float finalAttackSpeed = playerStats != null ? playerStats.attackSpeedMultiplier : 1f;
            damageTickCooldown = weaponData.cooldown / Mathf.Max(0.01f, finalAttackSpeed);
        }
    }

    /// <summary>
    /// Applies damage to all enemies currently within the aura's trigger.
    /// </summary>
    private void ApplyDamageToEnemies()
    {
        // Remove any null (destroyed) enemies from the list before applying damage.
        enemiesInRange.RemoveAll(item => item == null);

        if (enemiesInRange.Any())
        {
            float knockbackMult = tracker != null ? tracker.Knockback.Value : (playerStats != null ? playerStats.knockbackMultiplier : 1f);
            float finalKnockback = weaponData.knockback * knockbackMult;

            // Apply damage to every enemy currently in the list.
            foreach (EnemyStats enemy in enemiesInRange)
            {
                // For each enemy, perform a new, independent damage calculation.
                DamageResult damageResult = playerStats.CalculateDamage(weaponData.damage);
                
                enemy.TakeDamage(damageResult.damage, damageResult.isCritical);

                if (finalKnockback > 0 && !enemy.CompareTag("Reaper"))
                {
                    Vector3 knockbackDirection = (enemy.transform.position - transform.position).normalized;
                    knockbackDirection.y = 0;
                    enemy.ApplyKnockback(finalKnockback, 0.1f, knockbackDirection);
                }
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