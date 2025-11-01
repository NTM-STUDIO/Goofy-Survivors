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
    // Server-only: track created aura/shield proxies per weapon id
    private readonly Dictionary<int, GameObject> serverAuraProxies = new Dictionary<int, GameObject>();
    private readonly Dictionary<int, NetworkObject> serverShieldObjects = new Dictionary<int, NetworkObject>();

    // Local single-player references
    private readonly Dictionary<int, GameObject> localShieldObjects = new Dictionary<int, GameObject>();

    private bool shieldSubscribed = false;

    #region Initialization and Lifecycle
    void Awake()
    {
        playerStats = GetComponent<PlayerStats>();
        statsTracker = GetComponent<NetworkedPlayerStatsTracker>();
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

        if (startingWeapon != null)
        {
            int weaponId = weaponRegistry.GetWeaponId(startingWeapon);
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
            }
        }
    }

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
            var targetRefs = targets.Where(t => t != null && t.TryGetComponent(out NetworkObject _))
                                    .Select(t => (NetworkObjectReference)t.GetComponent<NetworkObject>())
                                    .ToArray();
            RequestAttackServerRpc(weaponId, targetRefs);
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
    private void RequestAttackServerRpc(int weaponId, NetworkObjectReference[] targetRefs)
    {
        WeaponData data = weaponRegistry.GetWeaponData(weaponId);
        if (data == null) return;

        switch (data.archetype)
        {
            case WeaponArchetype.Projectile: SpawnProjectiles_Host(data, targetRefs); break;
            case WeaponArchetype.Orbit: SpawnOrbitingWeapons_Host(data, weaponId); break;
        }
    }

    [ServerRpc]
    private void RequestCreateAuraServerRpc(int weaponId)
    {
        // Only the server should create aura proxies
        if (!IsServer) return;

        // Avoid duplicate proxies for the same weapon id
        if (serverAuraProxies.ContainsKey(weaponId) && serverAuraProxies[weaponId] != null)
            return;

        WeaponData data = weaponRegistry.GetWeaponData(weaponId);
        if (data == null) return;

        // Create a server-only aura proxy attached to this player
        GameObject proxy = new GameObject($"ServerAura_{data.name}");
        proxy.transform.SetParent(this.transform, false);
        proxy.transform.localPosition = Vector3.zero;
        var serverAura = proxy.AddComponent<ServerAura>();
        serverAura.Initialize(this.transform, playerStats, GetComponent<NetworkedPlayerStatsTracker>(), data);
        serverAuraProxies[weaponId] = proxy;

        // Inform all clients to attach local aura visuals under this owner
        InitializeAuraVisualClientRpc(this.NetworkObjectId, weaponId);
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

    [ClientRpc]
    private void InitializeAuraVisualClientRpc(ulong ownerNetId, int weaponId)
    {
        if (NetworkManager.Singleton == null) return;
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(ownerNetId, out var ownerNO)) return;

        var data = weaponRegistry.GetWeaponData(weaponId);
        if (data == null || data.archetype != WeaponArchetype.Aura || data.weaponPrefab == null) return;

        // Avoid duplicates: look for an existing child named "Aura_{data.name}"
        string childName = $"Aura_{data.name}";
        foreach (Transform child in ownerNO.transform)
        {
            if (child != null && child.gameObject.name == childName)
            {
                return;
            }
        }

        GameObject auraObj = Instantiate(data.weaponPrefab, ownerNO.transform.position, Quaternion.identity, ownerNO.transform);
        auraObj.name = childName;
        var aura = auraObj.GetComponent<AuraWeapon>();
        if (aura != null)
        {
            // If the aura owner is the local player, use PlayerStats for accurate tick timing
            var isLocalOwner = ownerNO.IsOwner;
            if (isLocalOwner)
            {
                aura.Initialize(playerStats, data);
            }
            else
            {
                var t = ownerNO.GetComponent<NetworkedPlayerStatsTracker>();
                if (t != null) aura.Initialize(t, data);
                else aura.Initialize(playerStats, data); // fallback
            }
        }
    }
    #endregion

    #region Host-Side Spawning
    private void SpawnProjectiles_Host(WeaponData data, NetworkObjectReference[] targetRefs)
    {
        DamageResult damageResult = playerStats.CalculateDamage(data.damage);
        int weaponId = weaponRegistry.GetWeaponId(data);
        Transform firePoint = transform;

        if (targetRefs.Length > 0)
        {
            foreach (var targetRef in targetRefs)
            {
                if (targetRef.TryGet(out NetworkObject targetObject))
                {
                    Vector3 direction = (targetObject.transform.position - firePoint.position).normalized;
                    direction.y = 0;
                    SpawnSingleProjectile_Host(data, weaponId, firePoint.position, direction, damageResult);
                }
            }
        }
        else
        {
            int finalAmount = data.amount + playerStats.projectileCount;
            for (int i = 0; i < finalAmount; i++)
            {
                Vector2 randomDir = UnityEngine.Random.insideUnitCircle.normalized;
                Vector3 direction = new Vector3(randomDir.x, 0, randomDir.y);
                SpawnSingleProjectile_Host(data, weaponId, firePoint.position, direction, damageResult);
            }
        }
    }

    private void SpawnSingleProjectile_Host(WeaponData data, int weaponId, Vector3 spawnPosition, Vector3 direction, DamageResult damageResult)
    {
        // Use identity rotation; projectile motion is controlled by Rigidbody velocity.
        Vector3 spawnPosWithOffset = spawnPosition + Vector3.up * 3f;
        TryRegisterNetworkPrefab(data.weaponPrefab);
        GameObject projectileObj = Instantiate(data.weaponPrefab, spawnPosWithOffset, Quaternion.identity);
        projectileObj.GetComponent<NetworkObject>().Spawn(true);
        InitializeProjectileClientRpc(projectileObj.GetComponent<NetworkObject>().NetworkObjectId, direction, damageResult.damage, damageResult.isCritical, weaponId);
    }

    [ClientRpc]
    private void InitializeProjectileClientRpc(ulong networkObjectId, Vector3 direction, float damage, bool isCritical, int weaponId)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject netObj))
        {
            var projectile = netObj.GetComponent<ProjectileWeapon>();
            if (projectile != null)
            {
                WeaponData data = weaponRegistry.GetWeaponData(weaponId);
                if (data == null) return;
                
                float finalSpeed = data.speed * playerStats.projectileSpeedMultiplier;
                float finalDuration = data.duration * playerStats.durationMultiplier;
                float finalKnockback = data.knockback * playerStats.knockbackMultiplier;
                float finalSize = data.area * playerStats.projectileSizeMultiplier;
                projectile.Initialize(null, direction, damage, isCritical, finalSpeed, finalDuration, finalKnockback, finalSize);
            }
        }
    }

    private void SpawnOrbitingWeapons_Host(WeaponData data, int weaponId)
    {
        int finalAmount = data.amount + playerStats.projectileCount;
        float angleStep = 360f / finalAmount;

        for (int i = 0; i < finalAmount; i++)
        {
            TryRegisterNetworkPrefab(data.weaponPrefab);
            GameObject orbitingObj = Instantiate(data.weaponPrefab, transform.position, Quaternion.identity);
            var orbitNetObj = orbitingObj.GetComponent<NetworkObject>();
            orbitNetObj.Spawn(true);
            // Parent to the player on server so hierarchy follows owner immediately
            try { orbitNetObj.TrySetParent(this.NetworkObject, true); } catch {}
            // Server-side init to ensure authoritative logic has correct owner stats immediately
            var serverOrbiter = orbitingObj.GetComponent<OrbitingWeapon>();
            if (serverOrbiter != null)
            {
                serverOrbiter.ServerInitialize(this.NetworkObject, data, i * angleStep);
                // Persist config for late joiners
                serverOrbiter.ServerSetNetworkConfigServerRpc(this.NetworkObjectId, weaponId, i * angleStep);
            }
            InitializeOrbitingWeaponClientRpc(orbitingObj.GetComponent<NetworkObject>().NetworkObjectId, this.NetworkObject, weaponId, i * angleStep);
        }
    }

    [ClientRpc]
    private void InitializeOrbitingWeaponClientRpc(ulong networkObjectId, NetworkObjectReference ownerRef, int weaponId, float startAngle)
    {
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject netObj))
        {
            var orbiter = netObj.GetComponent<OrbitingWeapon>();
            if (orbiter != null)
            {
                orbiter.NetworkInitialize(ownerRef, weaponRegistry.GetWeaponData(weaponId), startAngle);
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
        GameObject cloneObj = Instantiate(cloneData.weaponPrefab, transform.position, transform.rotation);
        var cloneNO = cloneObj.GetComponent<NetworkObject>();
        if (cloneNO == null)
        {
            Debug.LogWarning($"ShadowClone prefab '{cloneData.name}' has no NetworkObject. It will not sync in MP.");
            Destroy(cloneObj);
            return;
        }
    cloneNO.Spawn(true);
    // Parent to this player so it follows and clients can find stats via parent
    try { cloneNO.TrySetParent(this.NetworkObject, true); } catch {}

        // For each aura in the cloned list, create a server-authoritative aura proxy attached to the clone
        foreach (var wid in weaponIdsToClone)
        {
            var wdata = weaponRegistry.GetWeaponData(wid);
            if (wdata == null) continue;
            if (wdata.archetype == WeaponArchetype.Aura)
            {
                GameObject proxy = new GameObject($"ServerAura_Clone_{wdata.name}");
                proxy.transform.SetParent(cloneObj.transform, false);
                proxy.transform.localPosition = Vector3.zero;
                var serverAura = proxy.AddComponent<ServerAura>();
                serverAura.Initialize(cloneObj.transform, playerStats, GetComponent<NetworkedPlayerStatsTracker>(), wdata);
            }
        }

        // Tell clients to attach aura visuals (local only, damage will be ignored on clients)
        InitializeShadowCloneClientRpc(cloneNO.NetworkObjectId, weaponIdsToClone);
    }

    [ClientRpc]
    private void InitializeShadowCloneClientRpc(ulong cloneNetId, int[] weaponIdsToClone)
    {
        if (NetworkManager.Singleton == null) return;
        if (!NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(cloneNetId, out var cloneNO)) return;

        // Attach local-only aura visuals for each aura in the list
        foreach (var wid in weaponIdsToClone)
        {
            var wdata = weaponRegistry.GetWeaponData(wid);
            if (wdata == null) continue;
            if (wdata.archetype == WeaponArchetype.Aura && wdata.weaponPrefab != null)
            {
                GameObject auraObj = Instantiate(wdata.weaponPrefab, cloneNO.transform.position, Quaternion.identity, cloneNO.transform);
                var aura = auraObj.GetComponent<AuraWeapon>();
                if (aura != null)
                {
                    // Prefer the owner's tracker via parent
                    var parentTracker = cloneNO.transform.parent != null ? cloneNO.transform.parent.GetComponent<NetworkedPlayerStatsTracker>() : null;
                    if (parentTracker != null) aura.Initialize(parentTracker, wdata);
                    else aura.Initialize(playerStats, wdata); // fallback for SP
                }
            }
        }
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
        
        foreach (var target in targets)
        {
            Vector3 direction = (target != null)
                ? (target.position - firePoint.position).normalized
                : new Vector3(UnityEngine.Random.insideUnitCircle.normalized.x, 0, UnityEngine.Random.insideUnitCircle.normalized.y);
            direction.y = 0;

            Vector3 spawnPosWithOffset = firePoint.position + Vector3.up * 3f;
            GameObject projectileObj = Instantiate(data.weaponPrefab, spawnPosWithOffset, Quaternion.LookRotation(direction));
            var projectile = projectileObj.GetComponent<ProjectileWeapon>();
            if (projectile != null)
            {
                projectile.Initialize(target, direction, damageResult.damage, damageResult.isCritical, finalSpeed, finalDuration, finalKnockback, finalSize);
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