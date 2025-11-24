using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(PlayerStats))]
public class PlayerWeaponManager : NetworkBehaviour
{
    [Header("Required Assets")]
    [SerializeField] private WeaponRegistry weaponRegistry;
    [SerializeField] public Transform weaponParent;

    [Header("Game Settings")]
    [SerializeField] private WeaponData startingWeapon;

    private readonly NetworkList<int> networkWeaponIDs = new NetworkList<int>();
    private readonly List<int> localWeaponIDs = new List<int>();
    private readonly List<WeaponController> localWeaponControllers = new List<WeaponController>();

    private PlayerStats playerStats;
    private NetworkedPlayerStatsTracker statsTracker;
    private bool isP2P = false;
    // Server-only: track created aura logic instances per weapon id (AuraWeapon on server)
    private readonly Dictionary<int, GameObject> serverAuraLogic = new Dictionary<int, GameObject>();
    private readonly Dictionary<int, NetworkObject> serverShieldObjects = new Dictionary<int, NetworkObject>();

    // Local single-player references
    private readonly Dictionary<int, GameObject> localShieldObjects = new Dictionary<int, GameObject>();

    private bool shieldSubscribed = false;

    #region Initialization and Lifecycle
    void Awake()
    {
        playerStats = GetComponent<PlayerStats>();
        statsTracker = GetComponent<NetworkedPlayerStatsTracker>();
        
        // Ensure NetworkedPlayerStatsTracker exists for multiplayer sync
        if (statsTracker == null)
        {
            statsTracker = gameObject.AddComponent<NetworkedPlayerStatsTracker>();
        }
        
        // If a loadout-chosen starting weapon exists (single-player), override before Start() adds default
        try
        {
            var gm = GameManager.Instance;
            if (gm != null && !gm.isP2P)
            {
                var chosen = LoadoutSelections.SelectedWeapon;
                if (chosen != null)
                {
                    startingWeapon = chosen;
                }
            }
        }
        catch { }
    }

    void Start()
    {
        if (GameManager.Instance != null) { isP2P = GameManager.Instance.isP2P; }
        // Single-player starting weapon is simple and handled here.
        if (!isP2P)
        {
            AddStartingWeapon();
        }
    }

    // OnNetworkSpawn is now only responsible for cleanup and late-joiners.
    public override void OnNetworkSpawn()
    {
        if (!isP2P) return;
        base.OnNetworkSpawn();
        ClearAllWeaponControllers();
        InitializeExistingWeaponsForLateJoin();
        // All starting weapon logic has been REMOVED from here.
    }

    public override void OnNetworkDespawn()
    {
        base.OnNetworkDespawn();
        UnsubscribeShieldEvents();
    }
    #endregion

    #region Public API
    /// <summary>
    /// This is the new command that ONLY the GameManager (on the server) will call.
    /// </summary>
    public void Server_GiveStartingWeapon()
    {
        // This must only ever be run on the server.
        if (!IsServer) return;

        // Check if LoadoutSync provided an override
        int overrideWeaponId = -1;
        if (LoadoutSync.TryGetSelectionFor(OwnerClientId, out var sel) && sel.weaponId >= 0)
        {
            overrideWeaponId = sel.weaponId;
        }

        WeaponData weaponToGrant = startingWeapon;
        if (overrideWeaponId >= 0 && weaponRegistry != null)
        {
            var overrideData = weaponRegistry.GetWeaponData(overrideWeaponId);
            if (overrideData != null) weaponToGrant = overrideData;
        }

        if (weaponToGrant != null)
        {
            int weaponId = weaponRegistry.GetWeaponId(weaponToGrant);
            if (weaponId != -1)
            {
                if (!networkWeaponIDs.Contains(weaponId))
                {
                    networkWeaponIDs.Add(weaponId);
                }

                // Send a direct command to the player who owns this object.
                ClientRpcParams clientRpcParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { OwnerClientId } }
                };
                AddWeaponClientRpc(weaponId, clientRpcParams);

                // If this starting weapon is an Aura, ensure the server-side AuraWeapon (logic-only) exists.
                var data = weaponRegistry.GetWeaponData(weaponId);
                if (data != null && data.archetype == WeaponArchetype.Aura)
                {
                    CreateServerAuraLogic(weaponId);
                }
            }
        }
    }

    // Expose registry for systems like AuraWeapon to resolve WeaponData when global lookup isn't available
    public WeaponRegistry Registry => weaponRegistry;

    public void AddWeapon(WeaponData weaponData)
    {
        int weaponId = weaponRegistry.GetWeaponId(weaponData);
        if (weaponId == -1) return;

        if (isP2P)
        {
            if (IsOwner) AddWeaponServerRpc(weaponId);
        }
        else
        {
            if (!localWeaponIDs.Contains(weaponId))
            {
                localWeaponIDs.Add(weaponId);
                InstantiateWeaponController(weaponId);
            }
        }
    }

    public List<WeaponData> GetOwnedWeapons()
    {
        var ownedWeapons = new List<WeaponData>();
        List<int> idList;
        if (isP2P)
        {
            idList = new List<int>();
            foreach (int id in networkWeaponIDs) { idList.Add(id); }
        }
        else { idList = localWeaponIDs; }

        foreach (int id in idList)
        {
            WeaponData data = weaponRegistry.GetWeaponData(id);
            if (data != null) ownedWeapons.Add(data);
        }
        return ownedWeapons;
    }

    public void PerformAttack(int weaponId, Transform[] targets)
    {
        WeaponData data = weaponRegistry.GetWeaponData(weaponId);
        if (data == null) return;

        if (isP2P)
        {
            // IMPORTANT: Only send ONE primary target to the server; the server will fan-out the
            // final projectile count (amount + projectileCount). Sending multiple targets here would
            // result in server spawning the full count PER target (over-spawning).
            NetworkObjectReference[] targetRefs = System.Array.Empty<NetworkObjectReference>();
            if (targets != null && targets.Length > 0)
            {
                Transform best = null;
                float bestDist = float.MaxValue;
                foreach (var t in targets)
                {
                    if (t == null) continue;
                    var no = t.GetComponent<NetworkObject>();
                    if (no == null) continue;
                    float d = Vector3.Distance(transform.position, t.position);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = t;
                    }
                }
                if (best != null)
                {
                    var no = best.GetComponent<NetworkObject>();
                    targetRefs = new NetworkObjectReference[] { (NetworkObjectReference)no };
                }
            }
            // Send our current origin (prefer weaponParent if assigned) so the server spawns from the shooter’s actual firepoint
            Vector3 origin = (weaponParent != null) ? weaponParent.position : transform.position;
            RequestAttackServerRpc(weaponId, origin, targetRefs);
        }
        else
        {
            switch (data.archetype)
            {
                case WeaponArchetype.Projectile: SpawnProjectiles_Local(data, targets); break;
                case WeaponArchetype.Orbit: SpawnOrbitingWeapons_Local(data); break;
            }
        }
    }

    // Called by WeaponController when an Aura is activated locally
    public void NotifyAuraActivated(int weaponId)
    {
        if (isP2P)
        {
            if (IsOwner)
            {
                RequestCreateAuraServerRpc(weaponId);
            }
        }
        else
        {
            // Single-player: aura is local-only and already active via AuraWeapon
        }
    }

    // Called by WeaponController when a Shield weapon is obtained by the owner
    public void NotifyShieldActivated(int weaponId)
    {
        WeaponData data = weaponRegistry.GetWeaponData(weaponId);
        if (data == null || data.archetype != WeaponArchetype.Shield) return;

        if (isP2P)
        {
            if (IsOwner)
            {
                RequestCreateShieldServerRpc(weaponId);
            }
        }
        else
        {
            if (!localShieldObjects.ContainsKey(weaponId))
            {
                GameObject shieldObj = Instantiate(data.weaponPrefab, transform.position, Quaternion.identity, weaponParent != null ? weaponParent : transform);
                localShieldObjects[weaponId] = shieldObj;
                SubscribeShieldEvents();
            }
        }
    }

    // Called by WeaponController when a Shadow Clone is activated
    public void NotifyShadowCloneActivated(int cloneWeaponId, int[] weaponIdsToClone)
    {
        if (isP2P)
        {
            if (IsOwner)
            {
                RequestSpawnShadowCloneServerRpc(cloneWeaponId, weaponIdsToClone);
            }
        }
        else
        {
            // Single-player handled directly by WeaponController
        }
    }
    #endregion

    #region RPCs for Gameplay Actions
    [ServerRpc]
    private void AddWeaponServerRpc(int weaponId, ServerRpcParams rpcParams = default)
    {
        if (!networkWeaponIDs.Contains(weaponId))
        {
            networkWeaponIDs.Add(weaponId);
        }

        ClientRpcParams clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { rpcParams.Receive.SenderClientId }
            }
        };

        AddWeaponClientRpc(weaponId, clientRpcParams);

        // If the granted weapon is an Aura, create the server-side AuraWeapon now
        var data = weaponRegistry.GetWeaponData(weaponId);
        if (data != null && data.archetype == WeaponArchetype.Aura)
        {
            CreateServerAuraLogic(weaponId);
        }
    }

    [ClientRpc]
    private void AddWeaponClientRpc(int weaponId, ClientRpcParams clientRpcParams = default)
    {
        InstantiateWeaponController(weaponId);
    }

    // Server-side API for other systems (e.g., chests) to grant a weapon to this player only
    // Awards to this specific owner's client without requiring the owner to issue the RPC.
    public void Server_GiveWeaponToOwner(int weaponId)
    {
        if (!IsServer) return;
        if (weaponId == -1) return;

        if (!networkWeaponIDs.Contains(weaponId))
        {
            networkWeaponIDs.Add(weaponId);
        }

        ClientRpcParams clientRpcParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { OwnerClientId } }
        };
        AddWeaponClientRpc(weaponId, clientRpcParams);
        // Also show award UI only on the owning client's screen
        ShowAwardUIClientRpc(weaponId, clientRpcParams);

        // If the granted weapon is an Aura, create the server-side AuraWeapon now
        var data = weaponRegistry.GetWeaponData(weaponId);
        if (data != null && data.archetype == WeaponArchetype.Aura)
        {
            CreateServerAuraLogic(weaponId);
        }
    }

    [ClientRpc]
    private void ShowAwardUIClientRpc(int weaponId, ClientRpcParams clientRpcParams = default)
    {
        var data = weaponRegistry != null ? weaponRegistry.GetWeaponData(weaponId) : null;
        if (data == null) return;
        var ui = Object.FindFirstObjectByType<UIManager>();
        if (ui != null)
        {
            ui.OpenNewWeaponPanel(data);
        }
    }

    // Allows clients to request interaction with a consumable/chest via their own player object (always spawned)
    [ServerRpc(RequireOwnership = false)]
    public void RequestInteractWithConsumableServerRpc(ulong consumableNetId, ServerRpcParams rpcParams = default)
    {
        if (!IsServer) return;
        var spawnMgr = NetworkManager.Singleton?.SpawnManager;
        if (spawnMgr == null) return;

        if (!spawnMgr.SpawnedObjects.TryGetValue(consumableNetId, out var consumableNO)) return;
        var consumable = consumableNO.GetComponent<MapConsumable>();
        if (consumable == null) return;

        // Resolve the requesting player's NetworkObject
        var senderClientId = rpcParams.Receive.SenderClientId;
        var playerNO = spawnMgr.GetPlayerNetworkObject(senderClientId);
        if (playerNO == null) return;

        consumable.ServerProcessInteraction(playerNO.NetworkObjectId);
    }

    [ServerRpc]
    private void RequestAttackServerRpc(int weaponId, Vector3 origin, NetworkObjectReference[] targetRefs)
    {
        WeaponData data = weaponRegistry.GetWeaponData(weaponId);
        if (data == null) return;

        switch (data.archetype)
        {
            case WeaponArchetype.Projectile: SpawnProjectiles_Host(data, origin, targetRefs); break;
            case WeaponArchetype.Orbit: SpawnOrbitingWeapons_Host(data, weaponId); break;
        }
    }

    [ServerRpc]
    private void RequestCreateAuraServerRpc(int weaponId)
    {
        if (!IsServer) return;
        CreateServerAuraLogic(weaponId);
    }

    // Server-only: Instantiate AuraWeapon as a NetworkObject so everyone sees it; server applies damage
    private void CreateServerAuraLogic(int weaponId)
    {
        if (serverAuraLogic.ContainsKey(weaponId) && serverAuraLogic[weaponId] != null)
            return;

        WeaponData data = weaponRegistry.GetWeaponData(weaponId);
        if (data == null || data.archetype != WeaponArchetype.Aura || data.weaponPrefab == null) return;

        TryRegisterNetworkPrefab(data.weaponPrefab);
        Debug.Log($"[PWM] Attempting to spawn Aura '{data.name}' (weaponId={weaponId}) for owner client {OwnerClientId}.");
        GameObject auraObj = Instantiate(data.weaponPrefab, transform.position, Quaternion.identity);
        auraObj.name = $"Aura_{data.name}";
    var auraNO = auraObj.GetComponent<NetworkObject>();
        if (auraNO == null)
        {
            Debug.LogWarning($"Aura prefab '{data.name}' has no NetworkObject. It will not sync in MP.");
            Destroy(auraObj);
            return;
        }
        var aura = auraObj.GetComponent<AuraWeapon>();
        if (aura != null)
        {
            // Set pre-spawn config; applied on OnNetworkSpawn (server). Also pass direct WeaponData.
            int wid = weaponRegistry.GetWeaponId(data);
            aura.PreSpawnConfigure(wid, this.NetworkObjectId, data);
        }
        auraNO.Spawn(true);
        Debug.Log($"[PWM] Spawned Aura NetworkObjectId={auraNO.NetworkObjectId} for owner client {OwnerClientId}.");
    // Keep world position on parent to avoid any snap/offset across clients
    try { auraNO.TrySetParent(this.NetworkObject, true); } catch {}
        Debug.Log($"[PWM] Parent set to player NO={this.NetworkObjectId} for Aura NO={auraNO.NetworkObjectId}.");
        serverAuraLogic[weaponId] = auraObj;
    }

    [ServerRpc]
    private void RequestCreateShieldServerRpc(int weaponId)
    {
        if (!IsServer) return;
        if (serverShieldObjects.ContainsKey(weaponId) && serverShieldObjects[weaponId] != null) return;

        WeaponData data = weaponRegistry.GetWeaponData(weaponId);
        if (data == null) return;

    TryRegisterNetworkPrefab(data.weaponPrefab);
        GameObject shieldObj = Instantiate(data.weaponPrefab, transform.position, Quaternion.identity);
        var netObj = shieldObj.GetComponent<NetworkObject>();
        if (netObj == null)
        {
            Debug.LogWarning($"Shield prefab '{data.name}' has no NetworkObject. It will not sync in MP.");
            Destroy(shieldObj);
            return;
        }
        // Spawn and parent to this player
        netObj.Spawn(true);
        if (this.NetworkObject != null)
        {
            // Preserve world position when parenting to avoid visual pops
            try { netObj.TrySetParent(this.NetworkObject, true); }
            catch { /* fallback: leave unparented */ }
        }

        serverShieldObjects[weaponId] = netObj;
        SubscribeShieldEvents();
    }

    [ClientRpc]
    private void ShieldAbsorbClientRpc(ulong shieldNetId, int mutation, float duration)
    {
        if (NetworkManager.Singleton == null) return;
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(shieldNetId, out var netObj))
        {
            var shield = netObj.GetComponentInChildren<ShieldWeapon>();
            if (shield != null)
            {
                shield.AbsorbBuff((MutationType)mutation, duration);
            }
        }
    }

    // Removed: auras are now spawned as networked objects by the server
    #endregion

    #region Host-Side Spawning
    private void SpawnProjectiles_Host(WeaponData data, Vector3 origin, NetworkObjectReference[] targetRefs)
    {
        DamageResult damageResult = playerStats.CalculateDamage(data.damage);
        int weaponId = weaponRegistry.GetWeaponId(data);

        // Determine final projectile count using synced tracker when available (authoritative on server)
        int extraProjectiles = statsTracker != null ? (int)statsTracker.ProjectileCount.Value : playerStats.projectileCount;
        int finalAmount = data.amount + Mathf.Max(0, extraProjectiles);

        if (targetRefs.Length > 0)
        {
            foreach (var targetRef in targetRefs)
            {
                if (targetRef.TryGet(out NetworkObject targetObject))
                {
                    Vector3 baseDirection = (targetObject.transform.position - origin).normalized;
                    baseDirection.y = 0;

                    // Use same computed finalAmount for per-target spawn (apply spread if >1)
                    if (finalAmount <= 1)
                    {
                        SpawnSingleProjectile_Host(data, weaponId, origin, baseDirection, damageResult);
                    }
                    else
                    {
                        float totalSpread = Mathf.Min(45f, 6f * (finalAmount - 1));
                        float step = (finalAmount > 1) ? (totalSpread * 2f) / (finalAmount - 1) : 0f;
                        for (int i = 0; i < finalAmount; i++)
                        {
                            float offset = -totalSpread + (i * step);
                            Vector3 dir = Quaternion.Euler(0f, offset, 0f) * baseDirection;
                            SpawnSingleProjectile_Host(data, weaponId, origin, dir, damageResult);
                        }
                    }
                }
            }
        }
        else
        {
            for (int i = 0; i < finalAmount; i++)
            {
                Vector2 randomDir = UnityEngine.Random.insideUnitCircle.normalized;
                Vector3 direction = new Vector3(randomDir.x, 0, randomDir.y);
                SpawnSingleProjectile_Host(data, weaponId, origin, direction, damageResult);
            }
        }
    }

    private void SpawnSingleProjectile_Host(WeaponData data, int weaponId, Vector3 spawnPosition, Vector3 direction, DamageResult damageResult)
    {
        // Use identity rotation; projectile motion is controlled by Rigidbody velocity.
    Vector3 spawnPosWithOffset = spawnPosition + Vector3.up * 2f;
        TryRegisterNetworkPrefab(data.weaponPrefab);
        GameObject projectileObj = Instantiate(data.weaponPrefab, spawnPosWithOffset, Quaternion.identity);
        var projNO = projectileObj.GetComponent<NetworkObject>();
        if (projNO == null)
        {
            // Add a NetworkObject dynamically so projectiles are replicated to clients
            projNO = projectileObj.AddComponent<NetworkObject>();
        }
        var projectileComponent = projectileObj.GetComponent<ProjectileWeapon>();
        if (projectileComponent != null)
        {
            projectileComponent.ConfigureSource(weaponId, data.weaponName);
        }
        projNO.Spawn(true);
        // Attribute owner on the server instance for attacker-aware effects
        var projComp = projectileObj.GetComponent<ProjectileWeapon>();
        if (projComp != null && this.NetworkObject != null)
        {
            projComp.SetOwnerNetworkId(this.NetworkObjectId);
        }
        // Compute final values on the server using authoritative stats to ensure consistency on all clients
        float finalSpeed = data.speed * playerStats.projectileSpeedMultiplier;
        float finalDuration = data.duration * playerStats.durationMultiplier;
        float finalKnockback = data.knockback * playerStats.knockbackMultiplier;
        float finalSize = data.area * playerStats.projectileSizeMultiplier;
        InitializeProjectileClientRpc(
            projNO.NetworkObjectId,
            direction,
            damageResult.damage,
            damageResult.isCritical,
            weaponId,
            data.weaponName,
            finalSpeed,
            finalDuration,
            finalKnockback,
            finalSize
        );
    }

    [ClientRpc]
    private void InitializeProjectileClientRpc(ulong networkObjectId, Vector3 direction, float damage, bool isCritical, int weaponId, string weaponName, float finalSpeed, float finalDuration, float finalKnockback, float finalSize)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject netObj))
        {
            var projectile = netObj.GetComponent<ProjectileWeapon>();
            if (projectile != null)
            {
                projectile.ConfigureSource(weaponId, weaponName);
                // Server already computed the final values using the owner's stats; just apply
                projectile.Initialize(null, direction, damage, isCritical, finalSpeed, finalDuration, finalKnockback, finalSize);
            }
        }
    }

    private void SpawnOrbitingWeapons_Host(WeaponData data, int weaponId)
    {
        int extraProjectiles = statsTracker != null ? (int)statsTracker.ProjectileCount.Value : playerStats.projectileCount;
        int finalAmount = Mathf.Max(1, data.amount + Mathf.Max(0, extraProjectiles));
        float angleStep = 360f / finalAmount;

        for (int i = 0; i < finalAmount; i++)
        {
            TryRegisterNetworkPrefab(data.weaponPrefab);
            GameObject orbitingObj = Instantiate(data.weaponPrefab, transform.position, Quaternion.identity);
            var orbitNetObj = orbitingObj.GetComponent<NetworkObject>();
            orbitNetObj.Spawn(true);
            // Parent to the player on server so hierarchy follows owner immediately
            // Preserve world position when parenting
            try { orbitNetObj.TrySetParent(this.NetworkObject, true); } catch {}
            // Server-side init to ensure authoritative logic has correct owner stats immediately
            var serverOrbiter = orbitingObj.GetComponent<OrbitingWeapon>();
            if (serverOrbiter != null)
            {
                serverOrbiter.ServerInitialize(this.NetworkObject, data, i * angleStep);
                // Persist config for late joiners
                serverOrbiter.ServerSetNetworkConfigServerRpc(this.NetworkObjectId, weaponId, i * angleStep);
            }
            InitializeOrbitingWeaponClientRpc(orbitingObj.GetComponent<NetworkObject>().NetworkObjectId, this.NetworkObject, this.NetworkObject, weaponId, i * angleStep);
        }
    }

    [ClientRpc]
    private void InitializeOrbitingWeaponClientRpc(ulong networkObjectId, NetworkObjectReference ownerRef, NetworkObjectReference statsOwnerRef, int weaponId, float startAngle)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject netObj))
        {
            var orbiter = netObj.GetComponent<OrbitingWeapon>();
            if (orbiter != null)
            {
                orbiter.NetworkInitialize(ownerRef, statsOwnerRef, weaponRegistry.GetWeaponData(weaponId), startAngle);
            }
        }
    }

    [ServerRpc]
    private void RequestSpawnShadowCloneServerRpc(int cloneWeaponId, int[] weaponIdsToClone)
    {
        if (!IsServer) return;
        WeaponData cloneData = weaponRegistry.GetWeaponData(cloneWeaponId);
        if (cloneData == null || cloneData.weaponPrefab == null) return;

        // Spawn clone as a networked object so everyone sees it
    TryRegisterNetworkPrefab(cloneData.weaponPrefab);
    // Spawn the clone near the player, not on top and NOT parented (so it doesn't follow)
    Vector2 rand = UnityEngine.Random.insideUnitCircle.normalized * 1.5f;
    Vector3 spawnPos = transform.position + new Vector3(rand.x, 0, rand.y);
    GameObject cloneObj = Instantiate(cloneData.weaponPrefab, spawnPos, transform.rotation);
        var cloneNO = cloneObj.GetComponent<NetworkObject>();
        if (cloneNO == null)
        {
            Debug.LogWarning($"ShadowClone prefab '{cloneData.name}' has no NetworkObject. It will not sync in MP.");
            Destroy(cloneObj);
            return;
        }
        cloneNO.Spawn(true);

        // For each aura in the cloned list, create a NETWORKED AuraWeapon attached to the clone (server applies damage)
        foreach (var wid in weaponIdsToClone)
        {
            var wdata = weaponRegistry.GetWeaponData(wid);
            if (wdata == null) continue;
            if (wdata.archetype == WeaponArchetype.Aura)
            {
                if (wdata.weaponPrefab != null)
                {
                    TryRegisterNetworkPrefab(wdata.weaponPrefab);
                    GameObject auraObj = Instantiate(wdata.weaponPrefab, cloneObj.transform.position, Quaternion.identity);
                    auraObj.name = $"Aura_Clone_{wdata.name}";
                        var auraNO = auraObj.GetComponent<NetworkObject>();
                    if (auraNO == null)
                    {
                        Debug.LogWarning($"Aura prefab '{wdata.name}' has no NetworkObject. It will not sync in MP.");
                        Destroy(auraObj);
                    }
                    else
                    {
                        var aura = auraObj.GetComponent<AuraWeapon>();
                        if (aura != null)
                        {
                            // Stats owner = PLAYER; center parent = CLONE. Also pass direct WeaponData.
                            int auraWeaponId = weaponRegistry.GetWeaponId(wdata);
                            aura.PreSpawnConfigure(auraWeaponId, this.NetworkObjectId, wdata);
                        }
                        auraNO.Spawn(true);
                        // Preserve world position when parenting to clone
                        try { auraNO.TrySetParent(cloneNO, true); } catch {}
                    }
                }
            }
            else if (wdata.archetype == WeaponArchetype.Orbit)
            {
                // Spawn orbiting weapons for the clone (server-authoritative damage, clone-centered movement)
                int extraProjectiles = statsTracker != null ? (int)statsTracker.ProjectileCount.Value : playerStats.projectileCount;
                int finalAmount = Mathf.Max(1, wdata.amount + Mathf.Max(0, extraProjectiles));
                float angleStep = 360f / Mathf.Max(1, finalAmount);
                for (int i = 0; i < finalAmount; i++)
                {
                    TryRegisterNetworkPrefab(wdata.weaponPrefab);
                    GameObject orbitingObj = Instantiate(wdata.weaponPrefab, cloneObj.transform.position, Quaternion.identity);
                    var orbitNetObj = orbitingObj.GetComponent<NetworkObject>();
                    if (orbitNetObj == null)
                    {
                        Debug.LogWarning($"Orbit prefab '{wdata.name}' has no NetworkObject. Skipping for clone.");
                        Destroy(orbitingObj);
                        continue;
                    }
                    orbitNetObj.Spawn(true);
                    // Parent to the clone preserving world position
                    try { orbitNetObj.TrySetParent(cloneNO, true); } catch {}
                    // Server-side init uses the PLAYER for stats, but parent is the CLONE for center
                    var serverOrbiter = orbitingObj.GetComponent<OrbitingWeapon>();
                    if (serverOrbiter != null)
                    {
                        serverOrbiter.ServerInitialize(this.NetworkObject, wdata, i * angleStep);
                        // Persist config for late joiners (use clone as ownerId so clients center on clone)
                        int wId = weaponRegistry.GetWeaponId(wdata);
                        serverOrbiter.ServerSetNetworkConfigServerRpc(cloneNO.NetworkObjectId, wId, i * angleStep);
                    }
                    // Tell clients to initialize their copy using the clone as the owner/center, but use PLAYER for stats
                    InitializeOrbitingWeaponClientRpc(orbitNetObj.NetworkObjectId, cloneNO, this.NetworkObject, weaponRegistry.GetWeaponId(wdata), i * angleStep);
                }
            }
        }

    // Still notify clients about clone (for any client-side setup other than aura visuals if needed)
    InitializeShadowCloneClientRpc(cloneNO.NetworkObjectId, this.NetworkObjectId, weaponIdsToClone);
    }

    [ClientRpc]
    private void InitializeShadowCloneClientRpc(ulong cloneNetId, ulong ownerNetId, int[] weaponIdsToClone)
    {
        if (NetworkManager.Singleton == null) return;
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(cloneNetId, out var cloneNO)) return;
        NetworkObject ownerNO = null;
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(ownerNetId, out var found)) ownerNO = found;

        // Auras are network-spawned by the server; nothing to do here for auras.
    }
    #endregion

    #region Local Spawning
    private void SpawnProjectiles_Local(WeaponData data, Transform[] targets)
    {
        DamageResult damageResult = playerStats.CalculateDamage(data.damage);
        Transform firePoint = transform;
        float finalSpeed = data.speed * playerStats.projectileSpeedMultiplier;
        float finalDuration = data.duration * playerStats.durationMultiplier;
        float finalKnockback = data.knockback * playerStats.knockbackMultiplier;
        float finalSize = data.area * playerStats.projectileSizeMultiplier;
        int weaponId = weaponRegistry != null ? weaponRegistry.GetWeaponId(data) : -1;
        
        foreach (var target in targets)
        {
            Vector3 direction = (target != null)
                ? (target.position - firePoint.position).normalized
                : new Vector3(UnityEngine.Random.insideUnitCircle.normalized.x, 0, UnityEngine.Random.insideUnitCircle.normalized.y);
            direction.y = 0;

            Vector3 spawnPosWithOffset = firePoint.position + Vector3.up * 2f;
            // Keep root rotation unchanged; ProjectileWeapon will rotate only its visual child on Z
            GameObject projectileObj = Instantiate(data.weaponPrefab, spawnPosWithOffset, Quaternion.identity);
            var projectile = projectileObj.GetComponent<ProjectileWeapon>();
            if (projectile != null)
            {
                projectile.ConfigureSource(weaponId, data.weaponName);
                projectile.Initialize(target, direction, damageResult.damage, damageResult.isCritical, finalSpeed, finalDuration, finalKnockback, finalSize);
                // Attribute owner locally for attacker-aware effects in SP
                projectile.SetOwnerLocal(playerStats);
            }
        }
    }

    private void SpawnOrbitingWeapons_Local(WeaponData data)
    {
        int finalAmount = data.amount + playerStats.projectileCount;
        float angleStep = 360f / finalAmount;

        for (int i = 0; i < finalAmount; i++)
        {
            GameObject orbitingWeaponObj = Instantiate(data.weaponPrefab, transform.position, Quaternion.identity);
            OrbitingWeapon orbiter = orbitingWeaponObj.GetComponent<OrbitingWeapon>();
            if (orbiter != null)
            {
                orbiter.LocalInitialize(transform, i * angleStep, playerStats, data);
            }
        }
    }
    #endregion

    #region Helper Methods
    private void TryRegisterNetworkPrefab(GameObject prefab)
    {
        RuntimeNetworkPrefabRegistry.TryRegister(prefab);
    }

    private void AddStartingWeapon()
    {
        if (startingWeapon != null)
        {
            AddWeapon(startingWeapon);
        }
    }

    private void InstantiateWeaponController(int weaponId)
    {
        if (weaponRegistry == null)
        {
            Debug.LogError($"[PlayerWeaponManager] WeaponRegistry is not assigned on {gameObject.name}. Cannot instantiate weapon id {weaponId}.");
            return;
        }
        WeaponData weaponData = weaponRegistry.GetWeaponData(weaponId);
        if (weaponData == null || localWeaponControllers.Any(c => c.GetWeaponId() == weaponId)) return;

        GameObject weaponObject = new GameObject(weaponData.name + " Controller");
        weaponObject.transform.SetParent(weaponParent, false);

        WeaponController controller = weaponObject.AddComponent<WeaponController>();
        bool isOwner = !isP2P || IsOwner;
        
        controller.Initialize(weaponId, weaponData, this, playerStats, isOwner, weaponRegistry);
        
        localWeaponControllers.Add(controller);
    }

    private void InitializeExistingWeaponsForLateJoin()
    {
        foreach (int weaponId in networkWeaponIDs)
        {
            InstantiateWeaponController(weaponId);
        }
    }

    private void DestroyWeaponController(int index)
    {
        if (index >= 0 && index < localWeaponControllers.Count)
        {
            if (localWeaponControllers[index] != null) Destroy(localWeaponControllers[index].gameObject);
            localWeaponControllers.RemoveAt(index);
        }
    }

    private void ClearAllWeaponControllers()
    {
        foreach (var controller in localWeaponControllers)
        {
            if (controller != null) Destroy(controller.gameObject);
        }
        localWeaponControllers.Clear();
    }

    private void SubscribeShieldEvents()
    {
        if (shieldSubscribed) return;
        EnemyStats.OnEnemyDamaged += HandleEnemyDamagedForShield;
        shieldSubscribed = true;
    }

    private void UnsubscribeShieldEvents()
    {
        if (!shieldSubscribed) return;
        EnemyStats.OnEnemyDamaged -= HandleEnemyDamagedForShield;
        shieldSubscribed = false;
    }

    private void HandleEnemyDamagedForShield(EnemyStats damagedEnemy)
    {
        // Runs on server in MP (since EnemyStats damage events are server-side), and locally in SP
        if (damagedEnemy == null) return;

        if (damagedEnemy.CurrentMutation != MutationType.None)
        {
            MutationType stolenType = damagedEnemy.StealMutation();
            if (stolenType != MutationType.None)
            {
                // Apply the temporary buff to this player's stats
                playerStats?.AddTemporaryBuff(stolenType);

                // Duration from stats (sync with tracker in MP if available)
                float durationMult = statsTracker != null ? statsTracker.Duration.Value : playerStats != null ? playerStats.durationMultiplier : 1f;
                // We need a weapon data for duration; choose any shield we spawned/hold. Use first available.
                float baseDuration = 1.5f;
                foreach (var kvp in serverShieldObjects)
                {
                    var data = weaponRegistry.GetWeaponData(kvp.Key);
                    if (data != null) { baseDuration = data.duration; break; }
                }
                foreach (var kvp in localShieldObjects)
                {
                    var data = weaponRegistry.GetWeaponData(kvp.Key);
                    if (data != null) { baseDuration = data.duration; break; }
                }
                float finalDuration = Mathf.Max(0.1f, baseDuration * durationMult);

                // Visual: SP local
                foreach (var kvp in localShieldObjects)
                {
                    var shield = kvp.Value != null ? kvp.Value.GetComponentInChildren<ShieldWeapon>() : null;
                    if (shield != null)
                    {
                        shield.AbsorbBuff(stolenType, finalDuration);
                    }
                }

                // Visual: MP notify clients
                if (isP2P && IsServer)
                {
                    foreach (var kvp in serverShieldObjects)
                    {
                        if (kvp.Value != null)
                        {
                            ShieldAbsorbClientRpc(kvp.Value.NetworkObjectId, (int)stolenType, finalDuration);
                        }
                    }
                }
            }
        }
    }
    #endregion
}