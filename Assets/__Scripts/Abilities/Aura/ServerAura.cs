using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

/// <summary>
/// Server-only aura proxy that mirrors the owner's AuraWeapon size/behavior
/// and applies damage/knockback authoritatively on the server.
/// This object is NOT network-spawned; it exists only on the server.
/// </summary>
public class ServerAura : MonoBehaviour
{
    // Live references
    private Transform ownerTransform;
    private PlayerStats playerStats;              // server authoritative damage source
    private NetworkedPlayerStatsTracker tracker;  // synced multipliers for visuals/knockback
    private WeaponData weaponData;

    // Detection
    private SphereCollider trigger; // optional (kept for debugging/visualization); not relied upon for detection

    // Tick timer
    private float tickCooldown;

    // Y-offset for positioning (to roughly match visuals)
    private float yOffset = 1.5f;

    public void Initialize(Transform owner, PlayerStats stats, NetworkedPlayerStatsTracker syncedTracker, WeaponData data)
    {
        ownerTransform = owner;
        playerStats = stats;
        tracker = syncedTracker;
        weaponData = data;

    // Add trigger collider (optional)
    trigger = gameObject.AddComponent<SphereCollider>();
    trigger.isTrigger = true;
    trigger.radius = 0.5f; // base radius; we'll scale the transform instead

        // Position and parent to owner
        if (ownerTransform != null)
        {
            transform.SetParent(ownerTransform, false);
            transform.localPosition = Vector3.up * yOffset;
        }
    }

    void Update()
    {
        // Only meaningful on server (but this script exists only there)
        if (ownerTransform == null || playerStats == null || tracker == null || weaponData == null)
        {
            // If owner destroyed/despawned, clean up
            Destroy(gameObject);
            return;
        }

        // Keep position with owner
        transform.position = ownerTransform.position + Vector3.up * yOffset;

        // Update aura size continuously using synced projectile size
        float finalSize = weaponData.area * tracker.ProjectileSize.Value;
        transform.localScale = Vector3.one * finalSize;

        // Tick
        tickCooldown -= Time.deltaTime;
        if (tickCooldown <= 0f)
        {
            ApplyDamage();
            float finalAttackSpeed = Mathf.Max(0.01f, playerStats.attackSpeedMultiplier);
            tickCooldown = weaponData.cooldown / finalAttackSpeed;
        }
    }

    private void ApplyDamage()
    {
        // Determine effective radius (matches SphereCollider scaling: base 0.5 scaled by finalSize)
        float finalSize = weaponData.area * tracker.ProjectileSize.Value;
        float radius = Mathf.Max(0.01f, 0.5f * finalSize);

        // Gather nearby colliders and filter for enemies
        var colliders = Physics.OverlapSphere(ownerTransform.position, radius);
        if (colliders == null || colliders.Length == 0) return;

        float knockback = weaponData.knockback * tracker.Knockback.Value;
        HashSet<EnemyStats> processed = new HashSet<EnemyStats>();
        foreach (var col in colliders)
        {
            if (col == null) continue;
            // Filter by tag first for cheap check
            if (!col.CompareTag("Enemy") && !col.CompareTag("Reaper"))
            {
                // Try parent in case collider is on a child
                var parent = col.transform.parent;
                if (parent == null || (!parent.CompareTag("Enemy") && !parent.CompareTag("Reaper")))
                    continue;
            }

            var enemy = col.GetComponentInParent<EnemyStats>();
            if (enemy == null || processed.Contains(enemy)) continue;
            processed.Add(enemy);

            DamageResult dmg = playerStats.CalculateDamage(weaponData.damage);
            enemy.TakeDamage(dmg.damage, dmg.isCritical);

            if (knockback > 0 && enemy != null && !enemy.CompareTag("Reaper"))
            {
                Vector3 dir = (enemy.transform.position - ownerTransform.position);
                dir.y = 0f;
                dir.Normalize();
                enemy.ApplyKnockback(knockback, 0.1f, dir);
            }
        }
    }
}
