using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(EnemyStats))]
public class EnemyProjectileCaster : MonoBehaviour
{
    // --- MODIFIED: Removed the Idle state ---
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

        // --- MODIFIED: Removed the Idle case ---
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
    
    // --- MODIFIED: The Idle state and all its logic has been removed ---

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
        GameObject projectile = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
        Transform visualsChild = projectile.transform.Find("Visuals");

        if (visualsChild != null)
        {
            // Point visuals in the actual shot direction (using a pseudo target along the direction)
            Vector3 aimPoint = firePoint.position + direction * 5f;
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector2 projectileScreenPos = cam.WorldToScreenPoint(firePoint.position);
                Vector2 aimScreenPos = cam.WorldToScreenPoint(aimPoint);
                Vector2 screenDirection = (aimScreenPos - projectileScreenPos).normalized;
                float aimingAngleZ = Mathf.Atan2(screenDirection.y, screenDirection.x) * Mathf.Rad2Deg + visualsZAngleOffset;

                Quaternion baseIsoRotation = Quaternion.Euler(30, 45, 0);
                Quaternion aimingRotation = Quaternion.Euler(0, 0, aimingAngleZ);
                visualsChild.rotation = baseIsoRotation * aimingRotation;
            }
        }
        else
        {
            Debug.LogError("Projectile prefab is missing a required child object named 'Visuals'!", projectile);
        }

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

    // --- MODIFIED: Removed GetCurrentSightRange as it's no longer used ---
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