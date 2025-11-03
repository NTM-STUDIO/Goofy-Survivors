using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

[RequireComponent(typeof(NetworkObject))]
public class OrbitingWeapon : NetworkBehaviour
{
    // --- Live Data References ---
    private PlayerStats playerStats; // Used for server-authoritative damage calculation
    private NetworkedPlayerStatsTracker statsTracker; // Used for all synced visual/gameplay stats
    private WeaponData weaponData;
    
    // --- Core Properties ---
    private Transform orbitCenter;
    private float lifetime;
    private float currentAngle;
    private bool isInitialized = false;
    private bool isP2P = false;

    // --- Networked configuration for late-joiners ---
    private NetworkVariable<ulong> ownerNetId = new NetworkVariable<ulong>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<int> netWeaponId = new NetworkVariable<int>(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<float> netStartAngle = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    private NetworkVariable<double> netSpawnTime = new NetworkVariable<double>(0.0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Weapon Settings")]
    [Range(0f, 1f)]
    [Tooltip("Controls the size of the safe zone as a RATIO of the total radius.")]
    public float innerRadiusRatio = 0.8f;
    private float yOffset = 1.5f;

    // --- Hit Detection ---
    private HashSet<GameObject> hitEnemies = new HashSet<GameObject>();
    private float hitResetTime = 1.0f;
    private float nextResetTime;

    #region Initialization
    /// <summary>
    /// FOR SINGLE-PLAYER: Called by PlayerWeaponManager.
    /// </summary>
    public void LocalInitialize(Transform center, float startAngle, PlayerStats stats, WeaponData data)
    {
        this.isP2P = false;
        this.orbitCenter = center;
        this.currentAngle = startAngle;
        this.playerStats = stats;
        this.statsTracker = stats.GetComponent<NetworkedPlayerStatsTracker>();
        this.weaponData = data;

        if (this.statsTracker != null)
        {
            this.lifetime = data.duration * stats.durationMultiplier; // Read local stat for initial lifetime
            this.isInitialized = true;
            this.nextResetTime = Time.time + hitResetTime;
        }
        else
        {
            Debug.LogError("OrbitingWeapon could not find NetworkedPlayerStatsTracker on its owner in single-player!");
        }
    }

    /// <summary>
    /// FOR MULTIPLAYER: Called by a ClientRpc after the object is spawned.
    /// </summary>
    // New overload: separate orbit center (ownerRef) from stats owner (statsOwnerRef)
    public void NetworkInitialize(NetworkObjectReference ownerRef, NetworkObjectReference statsOwnerRef, WeaponData data, float startAngle)
    {
        this.isP2P = true;
        this.weaponData = data;

        if (ownerRef.TryGet(out NetworkObject ownerNetObj))
        {
            // For normal cases, owner is the player. For shadow clones, owner is the clone.
            this.orbitCenter = ownerNetObj.transform;
            // Prefer explicit stats owner when provided
            NetworkObject statsOwnerNetObj = null;
            if (statsOwnerRef.TryGet(out statsOwnerNetObj) && statsOwnerNetObj != null)
            {
                this.playerStats = statsOwnerNetObj.GetComponent<PlayerStats>();
                this.statsTracker = statsOwnerNetObj.GetComponent<NetworkedPlayerStatsTracker>();
            }
            else
            {
                // Fallback to owner (works when owner is the player)
                this.playerStats = ownerNetObj.GetComponent<PlayerStats>();
                this.statsTracker = ownerNetObj.GetComponent<NetworkedPlayerStatsTracker>();
            }
            // Parent under the owner so orbit uses localPosition and follows immediately
            try { transform.SetParent(orbitCenter, false); } catch {}

            if (this.statsTracker != null)
            {
                this.lifetime = data.duration * statsTracker.Duration.Value; // Read synced stat for initial lifetime
                // If we have server spawn time, compute angle with elapsed time; else use provided startAngle
                if (netSpawnTime.Value > 0.0)
                {
                    float rotationSpeed = weaponData.speed * statsTracker.ProjectileSpeed.Value;
                    double elapsed = NetworkManager.Singleton.ServerTime.Time - netSpawnTime.Value;
                    this.currentAngle = startAngle + (float)elapsed * rotationSpeed;
                    if (this.currentAngle > 360f || this.currentAngle < -360f) this.currentAngle = Mathf.Repeat(this.currentAngle, 360f);
                }
                else
                {
                    this.currentAngle = startAngle;
                }
                this.isInitialized = true;
                this.nextResetTime = Time.time + hitResetTime;
            }
            else
            {
                Debug.LogError("OrbitingWeapon could not find NetworkedPlayerStatsTracker on its owner in multiplayer!");
            }
        }
        else
        {
            if (IsServer)
            {
                var no = GetComponent<NetworkObject>();
                if (no != null && no.IsSpawned) no.Despawn(true); else Destroy(gameObject);
            }
        }
    }
    #endregion

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        // Watch for config arriving over the network
        netWeaponId.OnValueChanged += OnConfigChanged;
        ownerNetId.OnValueChanged += OnConfigChangedOwner;
        netStartAngle.OnValueChanged += OnConfigChangedAngle;
        // Late-join support: if this object existed before client connected, use networked config to initialize
        if (!isInitialized && ownerNetId.Value != 0 && netWeaponId.Value != -1)
        {
            TryInitializeFromNetworkConfig();
        }
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        netWeaponId.OnValueChanged -= OnConfigChanged;
        ownerNetId.OnValueChanged -= OnConfigChangedOwner;
        netStartAngle.OnValueChanged -= OnConfigChangedAngle;
    }

    private void OnConfigChanged(int previous, int current)
    {
        if (!isInitialized && ownerNetId.Value != 0 && netWeaponId.Value != -1)
            TryInitializeFromNetworkConfig();
    }

    private void OnConfigChangedOwner(ulong previous, ulong current)
    {
        if (!isInitialized && ownerNetId.Value != 0 && netWeaponId.Value != -1)
            TryInitializeFromNetworkConfig();
    }

    private void OnConfigChangedAngle(float previous, float current)
    {
        if (!isInitialized && ownerNetId.Value != 0 && netWeaponId.Value != -1)
            TryInitializeFromNetworkConfig();
    }

    private void TryInitializeFromNetworkConfig()
    {
        this.isP2P = true;

        var nm = NetworkManager.Singleton;
        if (nm == null) return;
        if (nm.SpawnManager.SpawnedObjects.TryGetValue(ownerNetId.Value, out var ownerNetObj))
        {
            this.orbitCenter = ownerNetObj.transform;
            this.playerStats = ownerNetObj.GetComponent<PlayerStats>();
            this.statsTracker = ownerNetObj.GetComponent<NetworkedPlayerStatsTracker>();
            try { transform.SetParent(orbitCenter, false); } catch {}

            var registry = FindFirstObjectByType<WeaponRegistry>();
            if (registry != null)
            {
                this.weaponData = registry.GetWeaponData(netWeaponId.Value);
            }

            if (this.statsTracker != null && this.weaponData != null)
            {
                this.lifetime = weaponData.duration * statsTracker.Duration.Value;
                // Sync angle based on server spawn time for consistent phase
                float rotationSpeed = weaponData.speed * statsTracker.ProjectileSpeed.Value;
                double elapsed = nm.ServerTime.Time - netSpawnTime.Value;
                this.currentAngle = netStartAngle.Value + (float)elapsed * rotationSpeed;
                if (this.currentAngle > 360f || this.currentAngle < -360f) this.currentAngle = Mathf.Repeat(this.currentAngle, 360f);
                this.isInitialized = true;
                this.nextResetTime = Time.time + hitResetTime;
            }
        }
    }

    /// <summary>
    /// SERVER-ONLY: Ensures the server initializes orbiters so authoritative hit/damage uses the correct owner's stats.
    /// Clients still receive NetworkInitialize via ClientRpc for their local copies.
    /// </summary>
    public void ServerInitialize(NetworkObject ownerNetObj, WeaponData data, float startAngle)
    {
        // Only meaningful on the server; safe to call anywhere, but do nothing if missing context.
        this.isP2P = true;
        this.weaponData = data;
        this.currentAngle = startAngle;

        if (ownerNetObj != null)
        {
            this.orbitCenter = ownerNetObj.transform;
            this.playerStats = ownerNetObj.GetComponent<PlayerStats>();
            this.statsTracker = ownerNetObj.GetComponent<NetworkedPlayerStatsTracker>();

            if (this.statsTracker != null && this.weaponData != null)
            {
                this.lifetime = weaponData.duration * statsTracker.Duration.Value; // Use synced stat for consistency
                this.isInitialized = true;
                this.nextResetTime = Time.time + hitResetTime;
            }
            else
            {
                Debug.LogError("OrbitingWeapon ServerInitialize: Missing NetworkedPlayerStatsTracker or WeaponData.");
            }
        }
    }

    // Server-only: write networked config so late-joiners can initialize without RPC history
    [ServerRpc(RequireOwnership = false)]
    public void ServerSetNetworkConfigServerRpc(ulong ownerId, int weaponId, float startAngle)
    {
        ownerNetId.Value = ownerId;
        netWeaponId.Value = weaponId;
        netStartAngle.Value = startAngle;
        var nm = NetworkManager.Singleton;
        netSpawnTime.Value = nm != null ? nm.ServerTime.Time : 0.0;
    }

    void Update()
    {
        if (!isInitialized || orbitCenter == null || statsTracker == null)
        {
            // Failsafe cleanup for orphaned objects that fail to initialize.
            if (lifetime < -5f && (!isP2P || IsServer))
            {
                var no = GetComponent<NetworkObject>();
                if (isP2P && IsServer && no != null && no.IsSpawned) no.Despawn(true); else Destroy(gameObject);
            }
            lifetime -= Time.deltaTime;
            return;
        }

        // Server: if the clone/owner we are centered on is gone, despawn this orbiter
        if (IsServer && ownerNetId.Value != 0)
        {
            var nm = NetworkManager.Singleton;
            if (nm != null)
            {
                bool hasOwner = nm.SpawnManager.SpawnedObjects.ContainsKey(ownerNetId.Value);
                if (!hasOwner)
                {
                    var no = GetComponent<NetworkObject>();
                    if (no != null && no.IsSpawned) no.Despawn(true); else Destroy(gameObject);
                    return;
                }
            }
        }

        lifetime -= Time.deltaTime;
        if (lifetime <= 0f)
        {
            if (!isP2P || IsServer)
            {
                var no = GetComponent<NetworkObject>();
                if (isP2P && IsServer && no != null && no.IsSpawned) no.Despawn(true); else Destroy(gameObject);
            }
            return;
        }
        // movement and visuals are applied in LateUpdate to follow after player movement
    }

    void LateUpdate()
    {
        if (!isInitialized || orbitCenter == null || statsTracker == null) return;

        // --- DYNAMIC STAT CALCULATION ---
        float finalSize = weaponData.area * statsTracker.ProjectileSize.Value;
        float rotationSpeed = weaponData.speed * statsTracker.ProjectileSpeed.Value;
        float orbitRadius = finalSize * 16f;

        // Apply visual stats every frame
        transform.localScale = Vector3.one * finalSize;

        // --- Movement Logic (run after player moved this frame) ---
        currentAngle += rotationSpeed * Time.deltaTime;
        if (currentAngle > 360f || currentAngle < -360f) currentAngle = Mathf.Repeat(currentAngle, 360f);

        float x = Mathf.Cos(currentAngle * Mathf.Deg2Rad) * orbitRadius;
        float z = Mathf.Sin(currentAngle * Mathf.Deg2Rad) * orbitRadius;
        float finalYOffset = yOffset * transform.localScale.y + 1.5f;

        // If parented, always orbit around the parent (works for clones and players)
        if (transform.parent != null)
        {
            transform.localPosition = new Vector3(x, finalYOffset, z);
        }
        else
        {
            transform.position = orbitCenter.position + new Vector3(x, finalYOffset, z);
        }

        // --- Hit Cooldown Logic ---
        if (Time.time >= nextResetTime)
        {
            hitEnemies.Clear();
            nextResetTime = Time.time + hitResetTime;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // Damage logic is authoritative (runs in SP, or on Server in MP).
        if (!isP2P || IsServer)
        {
            if (playerStats == null || weaponData == null || statsTracker == null) return;
            if (playerStats.IsDowned) return; // do not damage while owner is downed

            EnemyStats enemyStats = other.GetComponentInParent<EnemyStats>();
            if (enemyStats == null || hitEnemies.Contains(enemyStats.gameObject)) return;

            float orbitRadius = weaponData.area * statsTracker.ProjectileSize.Value * 16f;
            Vector3 enemyPos2D = new Vector3(other.transform.position.x, 0, other.transform.position.z);
            Vector3 centerPos2D = new Vector3(orbitCenter.position.x, 0, orbitCenter.position.z);
            float distanceToCenter = Vector3.Distance(enemyPos2D, centerPos2D);
            float effectiveInnerRadius = orbitRadius * innerRadiusRatio;

            if (distanceToCenter >= effectiveInnerRadius)
            {
                hitEnemies.Add(enemyStats.gameObject);

                // Use the local PlayerStats to calculate damage (which is server-authoritative).
                DamageResult damageResult = playerStats.CalculateDamage(weaponData.damage);
                enemyStats.TakeDamage(damageResult.damage, damageResult.isCritical);

                if (!enemyStats.CompareTag("Reaper"))
                {
                    // Use the synced tracker to get the knockback value.
                    float knockbackForce = weaponData.knockback * statsTracker.Knockback.Value;
                    Vector3 knockbackDir = (other.transform.position - orbitCenter.position).normalized;
                    knockbackDir.y = 0;
                    enemyStats.ApplyKnockback(knockbackForce, 0.4f, knockbackDir);
                }
            }
        }
    }

    #region Gizmos
    private void OnDrawGizmosSelected()
    {
        Transform center = orbitCenter != null ? orbitCenter : (Application.isPlaying ? null : transform.parent);
        if (center != null)
        {
            float radius = Application.isPlaying ? (weaponData.area * statsTracker.ProjectileSize.Value * 16f) : 5f;
            Vector3 gizmoCenter = center.position + new Vector3(0, yOffset, 0);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(gizmoCenter, radius);

            float effectiveInnerRadius = radius * innerRadiusRatio;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(gizmoCenter, effectiveInnerRadius);
        }
    }
    #endregion
}