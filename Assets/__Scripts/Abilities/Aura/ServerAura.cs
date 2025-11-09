using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Server-only aura proxy that mirrors the owner's AuraWeapon size/behavior
/// and applies damage/knockback authoritatively on the server.
/// This object is NOT network-spawned; it exists only on the server.
/// </summary>
public class ServerAura : MonoBehaviour
{
    [Header("Debug & Tuning")]
    [SerializeField] private bool debugLog = false;
    [Tooltip("Extra scale applied to computed radius to match visuals 1:1 if needed")]
    [SerializeField] private float radiusScale = 2.0f;
    [Tooltip("Base sphere radius before size multipliers. Default 0.5 to match a unit sphere scaled by area.")]
    [SerializeField] private float baseRadius = 1.0f;

    [Header("Enemy Scan (no physics)")]
    [Tooltip("Intervalo de atualização da cache de inimigos para varredura por distância (segundos)")]
    [SerializeField] private float enemyScanInterval = 0.4f;

    // Live references
    private Transform ownerTransform;
    private PlayerStats playerStats;              // server authoritative damage source
    private NetworkedPlayerStatsTracker tracker;  // synced multipliers
    private WeaponData weaponData;

    // Tick timer and placement
    private float tickCooldown;
    private float yOffset = 1.5f;

    // Enemy cache for distance-based scan
    private List<EnemyStats> enemyCache = new List<EnemyStats>();
    private float nextEnemyScanTime = 0f;

    private string GetAbilityLabel()
    {
        if (weaponData != null && !string.IsNullOrWhiteSpace(weaponData.weaponName))
        {
            return weaponData.weaponName;
        }

        return gameObject.name;
    }

    public void Initialize(Transform owner, PlayerStats stats, NetworkedPlayerStatsTracker syncedTracker, WeaponData data)
    {
        ownerTransform = owner;
        playerStats = stats;
        tracker = syncedTracker;
        weaponData = data;

        if (ownerTransform != null)
        {
            transform.SetParent(ownerTransform, false);
            transform.localPosition = Vector3.up * yOffset;
        }
    }

    void Update()
    {
        if (ownerTransform == null || playerStats == null || weaponData == null)
        {
            Destroy(gameObject);
            return;
        }

        // Do not apply aura damage while the owning player is downed
        if (playerStats.IsDowned)
        {
            return;
        }

        // Follow owner position
        transform.position = ownerTransform.position + Vector3.up * yOffset;

        // Update aura size (visual aid on server)
        float sizeMult = tracker != null ? tracker.ProjectileSize.Value : (playerStats != null ? playerStats.projectileSizeMultiplier : 1f);
        float finalSize = weaponData.area * sizeMult;
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
        // Effective radius independent of colliders/layers
        float sizeMult = tracker != null ? tracker.ProjectileSize.Value : (playerStats != null ? playerStats.projectileSizeMultiplier : 1f);
        float finalSize = weaponData.area * sizeMult;
        float radius = Mathf.Max(0.01f, baseRadius * finalSize * Mathf.Max(0.01f, radiusScale));

        // Refresh enemy cache periodically
        if (Time.time >= nextEnemyScanTime || enemyCache == null)
        {
            enemyCache = Object.FindObjectsByType<EnemyStats>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).ToList();
            nextEnemyScanTime = Time.time + Mathf.Max(0.05f, enemyScanInterval);
        }
        if (enemyCache == null || enemyCache.Count == 0)
        {
            if (debugLog) Debug.Log("[ServerAura] No enemies cached.");
            return;
        }

        float radiusSqr = radius * radius;
        float knockbackMult = tracker != null ? tracker.Knockback.Value : (playerStats != null ? playerStats.knockbackMultiplier : 1f);
        float knockback = weaponData.knockback * knockbackMult;
        int hitCount = 0;
        Vector3 ownerPos = ownerTransform.position; ownerPos.y = 0f;

        for (int i = 0; i < enemyCache.Count; i++)
        {
            var enemy = enemyCache[i];
            if (enemy == null) continue;
            Vector3 epos = enemy.transform.position; epos.y = 0f;
            if ((epos - ownerPos).sqrMagnitude <= radiusSqr)
            {
                DamageResult dmg = playerStats.CalculateDamage(weaponData.damage);
                enemy.TakeDamageFromAttacker(dmg.damage, dmg.isCritical, playerStats);
                hitCount++;

                AbilityDamageTracker.RecordDamage(GetAbilityLabel(), dmg.damage, gameObject);

                if (knockback > 0 && !enemy.CompareTag("Reaper"))
                {
                    Vector3 dir = (enemy.transform.position - ownerTransform.position); dir.y = 0f; dir.Normalize();
                    enemy.ApplyKnockback(knockback, 0.1f, dir);
                }
            }
        }

        if (debugLog)
        {
            Debug.Log($"[ServerAura] Damaged {hitCount} enemies at pos {ownerTransform.position} with radius {radius}");
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!debugLog || ownerTransform == null || weaponData == null) return;
        float sizeMult = tracker != null ? tracker.ProjectileSize.Value : (playerStats != null ? playerStats.projectileSizeMultiplier : 1f);
        float finalSize = weaponData.area * sizeMult;
        float radius = Mathf.Max(0.01f, baseRadius * finalSize * Mathf.Max(0.01f, radiusScale));
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(ownerTransform.position, radius);
    }
}
