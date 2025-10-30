using UnityEngine;
using Unity.Netcode;

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
        // In P2P mode the server should own enemy AI and shooting.
        if (GameManager.Instance != null && GameManager.Instance.isP2P)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
            {
                Debug.Log($"[{gameObject.name}] EnemyProjectileCaster: Disabled on client (server runs AI).", this);
                this.enabled = false;
                return;
            }
        }

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;
        else
        {
            Debug.LogError("EnemyCasterAI: Player object not found! Disabling AI.", this);
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
            rb.linearVelocity = Vector3.zero;
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

        Vector3 direction = (player.position - firePoint.position).normalized;
        GameObject projectile = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);
        Transform visualsChild = projectile.transform.Find("Visuals");

        if (visualsChild != null)
        {
            Vector2 projectileScreenPos = Camera.main.WorldToScreenPoint(firePoint.position);
            Vector2 playerScreenPos = Camera.main.WorldToScreenPoint(player.position);
            Vector2 screenDirection = (playerScreenPos - projectileScreenPos).normalized;
            float aimingAngleZ = Mathf.Atan2(screenDirection.y, screenDirection.x) * Mathf.Rad2Deg + 90f;

            Quaternion baseIsoRotation = Quaternion.Euler(30, 45, 0);
            Quaternion aimingRotation = Quaternion.Euler(0, 0, aimingAngleZ);
            visualsChild.rotation = baseIsoRotation * aimingRotation;
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