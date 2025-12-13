using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(EnemyStats))]
public class EnemyProjectileCaster : NetworkBehaviour
{
    private enum AIState { Chasing, Attacking }
    private AIState currentState;

    [Header("Component References")]
    public GameObject projectilePrefab;
    public Transform firePoint;

    [Header("AI Parameters")]
    [Tooltip("The maximum distance at which the enemy will start shooting.")]
    public float shootRange = 15f;
    [Tooltip("The perfect distance the enemy wants to be from the player.")]
    public float idealRange = 12f;
    [Tooltip("The minimum distance. If the player gets closer, the enemy backs up.")]
    public float retreatDistance = 8f;

    [Header("Projectile Spread")]
    [Tooltip("Half-angle of the aim cone in degrees. 0 = perfect aim, 15 = +/-15° spread.")]
    [Range(0f, 45f)] public float spreadAngleDegrees = 10f;

    [Header("Visuals")]
    [Tooltip("Z angle offset to apply to the projectile's Visuals child so sprites face the shot direction. If visuals look backwards, try -90 or 180.")]
    [SerializeField] private float visualsZAngleOffset = 90f;

    // --- Private Variables ---
    private Rigidbody rb;
    private EnemyStats myStats;
    private Transform player;
    private float fireTimer;
    private Vector3 moveDirection;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        myStats = GetComponent<EnemyStats>();
    }

    void Start()
    {
        // Find an initial target; in P2P we'll dynamically retarget to the nearest player
        player = FindNearestPlayer();
        if (player == null)
        {
            Debug.LogError("EnemyCasterAI: No player found! Disabling AI.", this);
            this.enabled = false;
            return;
        }

        // --- MODIFIED: Start in the Chasing state immediately ---
        SetState(AIState.Chasing);
        fireTimer = GetCurrentFireRate();
    }

    void Update()
    {
        if (Time.timeScale == 0f) return;
        if (myStats.IsKnockedBack) return;
        if (player == null) return;

    // Retarget to the nearest player each frame in P2P; SP keeps original target
    var latestPlayer = FindNearestPlayer();
    if (latestPlayer != null) player = latestPlayer;
    float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        switch (currentState)
        {
            case AIState.Chasing:
                UpdateChasingState(distanceToPlayer);
                break;
            case AIState.Attacking:
                UpdateAttackingState(distanceToPlayer);
                break;
        }
    }

    void FixedUpdate()
    {
        if (myStats.IsKnockedBack || Time.timeScale == 0f)
        {
            // While knocked back, let physics handle motion and do not override velocity.
            return;
        }

        // Apply movement if chasing or repositioning while attacking
        if (currentState == AIState.Chasing || currentState == AIState.Attacking)
        {
            rb.linearVelocity = moveDirection * myStats.moveSpeed;
        }
        else
        {
            rb.linearVelocity = Vector3.zero;
        }
    }

    private void SetState(AIState newState)
    {
        if (currentState == newState) return;
        currentState = newState;
    }
    

    private void UpdateChasingState(float distanceToPlayer)
    {
        // If we get into shooting range, switch to attacking
        if (distanceToPlayer <= shootRange)
        {
            SetState(AIState.Attacking);
        }
        else // Otherwise, always move towards the player
        {
            moveDirection = (player.position - transform.position).normalized;
            moveDirection.y = 0;
        }
    }

    private void UpdateAttackingState(float distanceToPlayer)
    {
        // Player is completely out of range -> Go back to chasing.
        if (distanceToPlayer > shootRange)
        {
            SetState(AIState.Chasing);
            return;
        }
        
        // Player is too close -> Back up.
        if (distanceToPlayer < retreatDistance)
        {
            moveDirection = (transform.position - player.position).normalized;
        }
        // Player is too far (but still in range) -> Move closer.
        else if (distanceToPlayer > idealRange)
        {
            moveDirection = (player.position - transform.position).normalized;
        }
        // Player is in the perfect spot -> Stop moving.
        else
        {
            moveDirection = Vector3.zero;
        }
        
        moveDirection.y = 0;

        // Always try to shoot while in the attacking state
        fireTimer += Time.deltaTime;
        if (fireTimer >= GetCurrentFireRate())
        {
            fireTimer = 0f;
            Shoot();
        }
    }
    
    // Your correct Shoot() method
    private void Shoot()
    {
        if (projectilePrefab == null || firePoint == null || player == null) return;

        // Base direction towards player (XZ plane)
        Vector3 direction = (player.position - firePoint.position).normalized;
        direction.y = 0f;
        // Apply a random yaw within the cone for inaccuracy
        if (spreadAngleDegrees > 0f)
        {
            float yaw = Random.Range(-spreadAngleDegrees, spreadAngleDegrees);
            direction = Quaternion.AngleAxis(yaw, Vector3.up) * direction;
            direction.Normalize();
        }
        var nm = NetworkManager.Singleton;
        bool isNetworked = nm != null && nm.IsListening;

        // Server-authoritative spawn in multiplayer
        if (isNetworked)
        {
            if (!IsServer)
            {
                // Only the server should spawn projectiles in MP
                return;
            }

            // Register and ensure the projectile is a NetworkObject
            RuntimeNetworkPrefabRegistry.TryRegister(projectilePrefab);
            GameObject projectile = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
            var projNO = projectile.GetComponent<NetworkObject>();
            if (projNO == null)
            {
                projNO = projectile.AddComponent<NetworkObject>();
            }
            projNO.Spawn(true);

            // Compute visuals rotation locally (clients will compute too in RPC for consistency)
            ApplyProjectileVisualsRotation(projectile.transform, firePoint.position, direction);

            // Set server physics so server projectile moves and collides authoritatively
            Rigidbody projRb = projectile.GetComponent<Rigidbody>();
            if (projRb != null)
            {
                projRb.linearVelocity = direction * GetCurrentProjectileSpeed();
            }

            // Set damage caster on server projectile
            var projectileDamageScript = projectile.GetComponentInChildren<EnemyProjectileDamage3D>();
            if (projectileDamageScript != null)
            {
                projectileDamageScript.CasterStats = myStats;
            }

            // Initialize client copies to the same velocity and visuals; provide caster id for optional lookups
            ulong casterId = 0UL;
            var casterNO = GetComponent<NetworkObject>();
            if (casterNO != null) casterId = casterNO.NetworkObjectId;
            InitializeEnemyProjectileClientRpc(projNO.NetworkObjectId, direction, GetCurrentProjectileSpeed(), firePoint.position, casterId);

            // Proper network despawn after lifetime
            StartCoroutine(DespawnProjectileLater(projNO.NetworkObjectId, 5f));
        }
        else
        {
            // Single-player: classic local instantiate and behavior
            GameObject projectile = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
            ApplyProjectileVisualsRotation(projectile.transform, firePoint.position, direction);

            Rigidbody projRb = projectile.GetComponent<Rigidbody>();
            if (projRb != null)
            {
                projRb.linearVelocity = direction * GetCurrentProjectileSpeed();
            }
            var projectileDamageScript = projectile.GetComponentInChildren<EnemyProjectileDamage3D>();
            if (projectileDamageScript != null)
            {
                projectileDamageScript.CasterStats = myStats;
            }
            Destroy(projectile, 5f);
        }
    }

    private void ApplyProjectileVisualsRotation(Transform projectileTransform, Vector3 origin, Vector3 direction)
    {
        Transform visualsChild = projectileTransform.Find("Visuals");
        if (visualsChild == null) return;

        Vector3 aimPoint = origin + direction * 5f;
        Camera cam = Camera.main;
        if (cam != null)
        {
            Vector2 projectileScreenPos = cam.WorldToScreenPoint(origin);
            Vector2 aimScreenPos = cam.WorldToScreenPoint(aimPoint);
            Vector2 screenDirection = (aimScreenPos - projectileScreenPos).normalized;
            float aimingAngleZ = Mathf.Atan2(screenDirection.y, screenDirection.x) * Mathf.Rad2Deg + visualsZAngleOffset;

            Quaternion baseIsoRotation = Quaternion.Euler(30, 45, 0);
            Quaternion aimingRotation = Quaternion.Euler(0, 0, aimingAngleZ);
            visualsChild.rotation = baseIsoRotation * aimingRotation;
        }
    }

    [ClientRpc]
    private void InitializeEnemyProjectileClientRpc(ulong projectileNetId, Vector3 direction, float speed, Vector3 origin, ulong casterNetId)
    {
        if (NetworkManager.Singleton == null) return;
        var spawnMgr = NetworkManager.Singleton.SpawnManager;
        if (spawnMgr == null) return;
        if (!spawnMgr.SpawnedObjects.TryGetValue(projectileNetId, out var projNO))
        {
            // Fallback: if the networked projectile prefab is not registered on this client,
            // create a local visual-only projectile so the client still sees the shot.
            if (projectilePrefab != null)
            {
                GameObject localProj = Instantiate(projectilePrefab, origin, Quaternion.identity);
                ApplyProjectileVisualsRotation(localProj.transform, origin, direction);
                var lrb = localProj.GetComponent<Rigidbody>();
                if (lrb != null) lrb.linearVelocity = direction * speed;
                var dmg = localProj.GetComponentInChildren<EnemyProjectileDamage3D>();
                if (dmg != null && casterNetId != 0UL && spawnMgr.SpawnedObjects.TryGetValue(casterNetId, out var casterNO2))
                {
                    var casterStats2 = casterNO2.GetComponent<EnemyStats>();
                    if (casterStats2 != null) dmg.CasterStats = casterStats2;
                }
                Destroy(localProj, 5f);
            }
            return;
        }

        // Apply visuals rotation consistently
        ApplyProjectileVisualsRotation(projNO.transform, origin, direction);

        // Apply client-side velocity for visual sync
        var rb = projNO.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = direction * speed;
        }

        // Optionally set CasterStats reference on client (not needed for damage authority)
        if (casterNetId != 0UL && spawnMgr.SpawnedObjects.TryGetValue(casterNetId, out var casterNO))
        {
            var casterStats = casterNO.GetComponent<EnemyStats>();
            var dmg = projNO.GetComponentInChildren<EnemyProjectileDamage3D>();
            if (casterStats != null && dmg != null)
            {
                dmg.CasterStats = casterStats;
            }
        }
    }

    private System.Collections.IEnumerator DespawnProjectileLater(ulong projectileNetId, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (!IsServer || NetworkManager.Singleton == null) yield break;
        var spawnMgr = NetworkManager.Singleton.SpawnManager;
        if (spawnMgr == null) yield break;
        if (spawnMgr.SpawnedObjects.TryGetValue(projectileNetId, out var projNO))
        {
            if (projNO != null && projNO.IsSpawned)
            {
                projNO.Despawn(true);
            }
            else if (projNO != null)
            {
                Destroy(projNO.gameObject);
            }
        }
    }

    private float GetCurrentFireRate() => GameManager.Instance ? GameManager.Instance.currentFireRate : 2f;
    private float GetCurrentProjectileSpeed() => GameManager.Instance ? GameManager.Instance.currentProjectileSpeed : 15f;

    private Transform FindNearestPlayer()
    {
        // Prefer networked list in P2P; fallback to tag search in SP.
        if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening)
        {
            float best = float.MaxValue;
            Transform bestT = null;
            foreach (var client in Unity.Netcode.NetworkManager.Singleton.ConnectedClientsList)
            {
                if (client.PlayerObject == null) continue;
                var ps = client.PlayerObject.GetComponent<PlayerStats>();
                if (ps != null && ps.IsDowned) continue; // skip downed players
                Transform t = client.PlayerObject.transform;
                float d = Vector3.Distance(transform.position, t.position);
                if (d < best)
                {
                    best = d;
                    bestT = t;
                }
            }
            return bestT;
        }
        else
        {
            // In SP, pick the player only if not downed
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                var ps = playerObj.GetComponent<PlayerStats>();
                if (ps != null && ps.IsDowned) return null;
                return playerObj.transform;
            }
            return null;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, shootRange);

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, idealRange);
        
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, retreatDistance);
    }
}