using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Networked Aura weapon: server applies damage; all clients render visuals. In single-player, works locally.
/// </summary>
public class AuraWeapon : NetworkBehaviour
{
    [Header("Debug & Server Settings")]
    [SerializeField] private bool debugLog = false;
    [Tooltip("If true, deactivate all Renderer components when running on server (not recommended if this object is networked)")]
    [SerializeField] private bool hideVisualsOnServer = false;
    [Tooltip("When true, MP radius is derived from a SphereCollider on the root (if present) to match visuals 1:1; otherwise uses WeaponData.area.")]
    [SerializeField] private bool useColliderRadiusInMP = true;
    // Networked configuration (server sets after spawn)
    private NetworkVariable<ulong> statsOwnerNetId = new NetworkVariable<ulong>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> weaponIdNet = new NetworkVariable<int>(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    // Pre-spawn configuration holder (set by server before Spawn; applied in OnNetworkSpawn on server)
    private int preSpawnWeaponId = -1;
    private ulong preSpawnStatsOwnerId = 0;
    private WeaponData preSpawnWeaponData = null;

    // References to live data
    private PlayerStats playerStats;
    private WeaponData weaponData;
    private NetworkedPlayerStatsTracker tracker; // optional: used for synced visuals when owner is remote

    // Internal timer for damage ticks
    private float damageTickCooldown;

    // A list to keep track of all enemies currently inside the aura's trigger (single-player only path)
    private List<EnemyStats> enemiesInRange = new List<EnemyStats>();

    // Server scans for enemies once per damage tick using Physics.OverlapSphere (no separate scan timer)

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

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        // If we are the server, apply any pre-spawn configuration into NetworkVariables now
        if (IsServer)
        {
            if (preSpawnWeaponId >= 0)
            {
                weaponIdNet.Value = preSpawnWeaponId;
            }
            if (preSpawnStatsOwnerId != 0)
            {
                statsOwnerNetId.Value = preSpawnStatsOwnerId;
            }
            // Apply direct weapon data if provided pre-spawn (server only convenience)
            if (preSpawnWeaponData != null)
            {
                weaponData = preSpawnWeaponData;
            }
        }

        // Resolve weapon data and tracker/stats owner for visuals and server logic
    TryResolveWeaponDataFromId();
        TryResolveStatsOwner();

        if (debugLog)
        {
            Debug.Log($"[AuraWeapon] OnNetworkSpawn IsServer={IsServer} wid={weaponIdNet.Value} ownerNO={statsOwnerNetId.Value} hasWD={(weaponData!=null)}");
        }

        // In MP we don't want to disable visuals on server unless explicitly chosen, but default is false.
    }

    private void TryResolveStatsOwner()
    {
        // If we already have stats/tracker, skip
        if (tracker != null || playerStats != null) return;

        // Prefer NetworkVariable reference
        ulong id = statsOwnerNetId.Value;
        if (id != 0 && NetworkManager != null)
        {
            if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(id, out var no))
            {
                var t = no.GetComponent<NetworkedPlayerStatsTracker>();
                if (t != null) { tracker = t; return; }
                var ps = no.GetComponent<PlayerStats>();
                if (ps != null) { playerStats = ps; return; }
            }
        }
        // Fallback: parent
        if (transform.parent != null)
        {
            var t = transform.parent.GetComponentInParent<NetworkedPlayerStatsTracker>();
            if (t != null) { tracker = t; return; }
            var ps = transform.parent.GetComponentInParent<PlayerStats>();
            if (ps != null) { playerStats = ps; return; }
        }
    }

    private string GetAbilityLabel()
    {
        if (weaponData != null && !string.IsNullOrWhiteSpace(weaponData.weaponName))
        {
            return weaponData.weaponName;
        }

        return gameObject.name;
    }

    private void TryResolveWeaponDataFromId()
    {
        if (weaponData != null) return;
        int wid = weaponIdNet.Value;
        if (wid < 0) return;
        // First try a global registry in scene
        var reg = Object.FindFirstObjectByType<WeaponRegistry>();
        if (reg != null)
        {
            weaponData = reg.GetWeaponData(wid);
        }
        // If not found, try resolving via the stats owner
        if (weaponData == null && NetworkManager != null)
        {
            ulong ownerId = statsOwnerNetId.Value;
            if (ownerId != 0 && NetworkManager.SpawnManager != null && NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(ownerId, out var ownerNO))
            {
                var pwm = ownerNO.GetComponent<PlayerWeaponManager>();
                if (pwm != null && pwm.Registry != null)
                {
                    weaponData = pwm.Registry.GetWeaponData(wid);
                }
            }
        }
        // As a final fallback, try parent hierarchy (if parenting already set)
        if (weaponData == null && transform.parent != null)
        {
            var pwm = transform.parent.GetComponentInParent<PlayerWeaponManager>();
            if (pwm != null && pwm.Registry != null)
            {
                weaponData = pwm.Registry.GetWeaponData(wid);
            }
        }
        if (weaponData == null && debugLog)
        {
            Debug.LogWarning($"[AuraWeapon] Failed to resolve WeaponData for id {wid}.");
        }
    }

    void Update()
    {
        // Require weapon data; allow missing playerStats when using tracker for remote-owner visuals
        if (weaponData == null)
        {
            // In networked games, we might still be waiting for NetworkVariable to apply
            if (NetworkManager != null && NetworkManager.IsListening)
            {
                TryResolveWeaponDataFromId();
                if (weaponData == null)
                {
                    return;
                }
            }
            return;
        }

        // If stats/tracker still unresolved (common when parent set after spawn), keep trying
        if (tracker == null && playerStats == null)
        {
            TryResolveStatsOwner();
        }

    // --- CONTINUOUS STAT UPDATES ---
        // Update the aura's size every frame to reflect any changes in stats (tracker preferred for remote owners).
        float sizeMult = 1f;
        if (tracker != null) sizeMult = tracker.ProjectileSize.Value;
        else if (playerStats != null) sizeMult = playerStats.projectileSizeMultiplier;
        float currentSize = weaponData.area * sizeMult;
        transform.localScale = Vector3.one * currentSize;

        // Multiplayer handling: if server, apply damage here; if client, visuals only
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            if (NetworkManager.Singleton.IsServer)
            {
                // Server applies damage; keep visuals visible for everyone.
                if (hideVisualsOnServer) { DisableAllRenderers(); }
                ServerTickDamage();
            }
            // Clients: visuals only
            return;
        }

        // --- DAMAGE TICK LOGIC (single-player only) ---
        damageTickCooldown -= Time.deltaTime;
        // Reflect visuals in SP based on downed state and pause damage if downed
        bool isDowned = (playerStats != null && playerStats.IsDowned);
        SetVisualsActive(!isDowned);
        // Do not apply aura damage while the owning player is downed in SP
        if (isDowned)
        {
            if (damageTickCooldown < 0.05f) damageTickCooldown = 0.05f; // small delay to avoid tight loop
            return;
        }

        if (damageTickCooldown <= 0f)
        {
            ApplyDamageToEnemies();

            // Reset the cooldown based on the player's current attack speed.
            float finalAttackSpeed = playerStats != null ? playerStats.attackSpeedMultiplier : 1f;
            damageTickCooldown = weaponData.cooldown / Mathf.Max(0.01f, finalAttackSpeed);
        }
    }

    private void ServerTickDamage()
    {
        // We can still operate with fallback damage if playerStats isn't resolved yet

        damageTickCooldown -= Time.deltaTime;
        if (damageTickCooldown > 0f) return;

        // If the owner is downed, pause damage application (attempt to resolve stats if missing)
        var psOwner = ResolveOwnerPlayerStats();
        if (psOwner != null && psOwner.IsDowned)
        {
            damageTickCooldown = 0.1f; // small delay to avoid tight loop
            return;
        }

        // Physics-based scan around the aura center to find enemies reliably in MP
        int hitCount = 0;
        Collider[] hits = null;

        // Prefer using a collider shape when configured
        if (useColliderRadiusInMP)
        {
            // Try Capsule first (requested behavior)
            if (TryGetCapsuleWorld(out var p0, out var p1, out var capRadius))
            {
                hits = Physics.OverlapCapsule(p0, p1, capRadius, ~0, QueryTriggerInteraction.Collide);
                if (debugLog)
                {
                    Debug.Log($"[AuraWeapon Server] Using Capsule overlap: r={capRadius:F2} p0={p0} p1={p1}");
                }
            }
            // Fallback to Sphere if present
            else if (TryGetSphereWorld(out var centerWS, out var sphereRadius))
            {
                hits = Physics.OverlapSphere(centerWS, sphereRadius, ~0, QueryTriggerInteraction.Collide);
                if (debugLog)
                {
                    Debug.Log($"[AuraWeapon Server] Using Sphere overlap: r={sphereRadius:F2} center={centerWS}");
                }
            }
        }

        // Final fallback: derive radius from WeaponData/size
        if (hits == null)
        {
            float radius = ComputeEffectiveRadius();
            Vector3 center = transform.position;
            hits = Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Collide);
            if (debugLog)
            {
                Debug.Log($"[AuraWeapon Server] Using fallback area radius: r={radius:F2} center={center}");
            }
        }

        if (hits != null && hits.Length > 0)
        {
            for (int i = 0; i < hits.Length; i++)
            {
                var col = hits[i];
                if (col == null) continue;
                // Try to resolve an EnemyStats on this collider or its parents
                EnemyStats enemy = col.GetComponentInParent<EnemyStats>();
                if (enemy == null) continue;
                // Optional tag check (only damage enemies/reaper if present)
                if (!enemy.CompareTag("Enemy") && !enemy.CompareTag("Reaper"))
                {
                    // If tags are not set consistently, still proceed since we resolved EnemyStats
                }

                // Apply damage/knockback
                DamageResult damageResult = playerStats != null
                    ? playerStats.CalculateDamage(weaponData.damage)
                    : new DamageResult { damage = weaponData.damage, isCritical = false };
                enemy.TakeDamage(damageResult.damage, damageResult.isCritical);

                AbilityDamageTracker.RecordDamage(GetAbilityLabel(), damageResult.damage, gameObject);

                if (weaponData.knockback > 0 && !enemy.CompareTag("Reaper"))
                {
                    Vector3 dir = (enemy.transform.position - transform.position); dir.y = 0f; dir.Normalize();
                    float knockbackMult = tracker != null ? tracker.Knockback.Value : (playerStats != null ? playerStats.knockbackMultiplier : 1f);
                    enemy.ApplyKnockback(weaponData.knockback * knockbackMult, 0.1f, dir);
                }
                hitCount++;
            }
        }

        if (debugLog)
        {
            Debug.Log($"[AuraWeapon Server] Damaged {hitCount} enemies at {transform.position}");
        }

        float finalAttackSpeed = Mathf.Max(0.01f, playerStats != null ? playerStats.attackSpeedMultiplier : 1f);
        damageTickCooldown = weaponData.cooldown / finalAttackSpeed;
    }

    private PlayerStats ResolveOwnerPlayerStats()
    {
        if (playerStats != null) return playerStats;
        // Try resolve via network owner id
        if (NetworkManager != null && NetworkManager.SpawnManager != null)
        {
            ulong id = statsOwnerNetId.Value;
            if (id != 0 && NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(id, out var ownerNO))
            {
                var ps = ownerNO.GetComponent<PlayerStats>();
                if (ps != null)
                {
                    playerStats = ps; // cache
                    return playerStats;
                }
            }
        }
        // Fallback: parent chain
        if (transform.parent != null)
        {
            var ps = transform.parent.GetComponentInParent<PlayerStats>();
            if (ps != null)
            {
                playerStats = ps;
                return playerStats;
            }
        }
        return null;
    }

    // Attempts to read a SphereCollider on this object and convert to world center/radius
    private bool TryGetSphereWorld(out Vector3 centerWS, out float radiusWS)
    {
        centerWS = default;
        radiusWS = 0f;
        var sc = GetComponent<SphereCollider>();
        if (sc == null) return false;
        centerWS = transform.TransformPoint(sc.center);
        var ls = transform.lossyScale;
        float scale = Mathf.Max(Mathf.Abs(ls.x), Mathf.Abs(ls.y), Mathf.Abs(ls.z));
        radiusWS = Mathf.Max(0.01f, sc.radius * scale);
        return true;
    }

    // Attempts to read a CapsuleCollider on this object and convert to OverlapCapsule parameters
    private bool TryGetCapsuleWorld(out Vector3 point0, out Vector3 point1, out float radiusWS)
    {
        point0 = default;
        point1 = default;
        radiusWS = 0f;

        var cc = GetComponent<CapsuleCollider>();
        if (cc == null) return false;

        // Determine axis scales and world axis
        Vector3 ls = transform.lossyScale;
        float sx = Mathf.Abs(ls.x);
        float sy = Mathf.Abs(ls.y);
        float sz = Mathf.Abs(ls.z);

        // Unity CapsuleCollider.direction: 0=X, 1=Y, 2=Z
        Vector3 axisLocal;
        float axisScale;
        float radiusScale;
        switch (cc.direction)
        {
            case 0: // X
                axisLocal = Vector3.right;
                axisScale = sx;
                radiusScale = Mathf.Max(sy, sz);
                break;
            case 1: // Y
                axisLocal = Vector3.up;
                axisScale = sy;
                radiusScale = Mathf.Max(sx, sz);
                break;
            case 2: // Z
            default:
                axisLocal = Vector3.forward;
                axisScale = sz;
                radiusScale = Mathf.Max(sx, sy);
                break;
        }

        // World values
        Vector3 centerWS = transform.TransformPoint(cc.center);
        Vector3 axisWS = transform.TransformDirection(axisLocal).normalized;

        // Compute world radius (scaled by perpendicular axes maximum)
        radiusWS = Mathf.Max(0.01f, cc.radius * radiusScale);

        // The line segment length excludes the hemispheres: (height/2 - radius) scaled along axis
        float halfLine = Mathf.Max(0f, (cc.height * 0.5f - cc.radius)) * axisScale;

        point0 = centerWS + axisWS * halfLine;
        point1 = centerWS - axisWS * halfLine;
        return true;
    }

    private float ComputeEffectiveRadius()
    {
        // Prefer collider radius if configured and available to match SP visuals exactly
        if (useColliderRadiusInMP)
        {
            var sc = GetComponent<SphereCollider>();
            if (sc != null)
            {
                // Account for scaling; we assume uniform X/Z scaling for 2D-ish top-down
                float scaleXZ = Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
                float r = Mathf.Max(0.01f, sc.radius * scaleXZ);
                return r;
            }
        }
        // Fallback: derive from WeaponData.area and projectileSize multiplier
        float sizeMult = tracker != null ? tracker.ProjectileSize.Value : (playerStats != null ? playerStats.projectileSizeMultiplier : 1f);
        float currentSize = weaponData != null ? weaponData.area * sizeMult : 1f;
        return Mathf.Max(0.01f, currentSize);
    }

    private void DisableAllRenderers()
    {
        var rends = GetComponentsInChildren<Renderer>(true);
        foreach (var r in rends) r.enabled = false;
        // Also disable particle systems if any
        var ps = GetComponentsInChildren<ParticleSystem>(true);
        foreach (var p in ps) p.gameObject.SetActive(false);
    }

    public void SetVisualsActive(bool active)
    {
        var rends = GetComponentsInChildren<Renderer>(true);
        foreach (var r in rends) r.enabled = active;
        var ps = GetComponentsInChildren<ParticleSystem>(true);
        foreach (var p in ps)
        {
            if (p == null) continue;
            if (active)
            {
                p.gameObject.SetActive(true);
                if (!p.isPlaying) p.Play();
            }
            else
            {
                if (p.isPlaying) p.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                p.gameObject.SetActive(false);
            }
        }
    }

    // Server-side helper to set configuration BEFORE spawning; values will be applied on OnNetworkSpawn
    public void PreSpawnConfigure(int weaponId, ulong statsOwnerNetworkObjectId)
    {
        preSpawnWeaponId = weaponId;
        preSpawnStatsOwnerId = statsOwnerNetworkObjectId;
    }

    // Overload to also pass direct WeaponData for the server instance (clients still resolve via id)
    public void PreSpawnConfigure(int weaponId, ulong statsOwnerNetworkObjectId, WeaponData directWeaponData)
    {
        preSpawnWeaponId = weaponId;
        preSpawnStatsOwnerId = statsOwnerNetworkObjectId;
        preSpawnWeaponData = directWeaponData;
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

                AbilityDamageTracker.RecordDamage(GetAbilityLabel(), damageResult.damage, gameObject);

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