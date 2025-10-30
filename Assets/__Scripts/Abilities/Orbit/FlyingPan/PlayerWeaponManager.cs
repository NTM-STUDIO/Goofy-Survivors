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
    private bool isP2P = false;

    #region Initialization and Lifecycle
    void Awake()
    {
        playerStats = GetComponent<PlayerStats>();
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
                Vector2 randomDir = Random.insideUnitCircle.normalized;
                Vector3 direction = new Vector3(randomDir.x, 0, randomDir.y);
                SpawnSingleProjectile_Host(data, weaponId, firePoint.position, direction, damageResult);
            }
        }
    }

    private void SpawnSingleProjectile_Host(WeaponData data, int weaponId, Vector3 spawnPosition, Vector3 direction, DamageResult damageResult)
    {
        GameObject projectileObj = Instantiate(data.weaponPrefab, spawnPosition, Quaternion.LookRotation(direction));
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
            GameObject orbitingObj = Instantiate(data.weaponPrefab, transform.position, Quaternion.identity);
            orbitingObj.GetComponent<NetworkObject>().Spawn(true);
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
                : new Vector3(Random.insideUnitCircle.normalized.x, 0, Random.insideUnitCircle.normalized.y);
            direction.y = 0;

            GameObject projectileObj = Instantiate(data.weaponPrefab, firePoint.position, Quaternion.LookRotation(direction));
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
    private void AddStartingWeapon()
    {
        if (startingWeapon != null)
        {
            AddWeapon(startingWeapon);
        }
    }

    private void InstantiateWeaponController(int weaponId)
    {
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
    #endregion
}