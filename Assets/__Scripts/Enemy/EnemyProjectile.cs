using UnityEngine;

[RequireComponent(typeof(Rigidbody))] // Using the 3D Rigidbody
[RequireComponent(typeof(EnemyStats))]
public class EnemyCasterAI_2Point5D : MonoBehaviour
{
    // --- STATE MACHINE ---
    private enum AIState { Idle, Chasing, Attacking }
    private AIState currentState;

    [Header("Component References")]
    public GameObject projectilePrefab;
    public Transform firePoint;

    [Header("AI Parameters")]
    public float moveSpeed = 4f;
    public float shootRange = 10f;
    public float sightRange = 20f;
    public float fireRate = 2f;

    // --- Private Variables ---
    private Rigidbody rb;
    private EnemyStats myStats;
    private Transform player;
    private float fireTimer;
    private Vector3 moveDirection; // 3D vector for movement direction

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        myStats = GetComponent<EnemyStats>();
    }

    void Start()
    {
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
        }
        else
        {
            // If the player isn't found, disable the AI.
            this.enabled = false;
            return;
        }

        SetState(AIState.Idle);
        fireTimer = fireRate;
    }

    void Update()
    {
        if (player == null) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        // --- STATE LOGIC (The Brain) ---
        switch (currentState)
        {
            case AIState.Idle:
                UpdateIdleState(distanceToPlayer);
                break;
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
        // --- PHYSICS (The Legs) ---
        if (currentState == AIState.Chasing)
        {
            rb.linearVelocity = moveDirection * moveSpeed;
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

    // --- STATE BEHAVIORS ---

    private void UpdateIdleState(float distanceToPlayer)
    {
        if (distanceToPlayer <= sightRange)
        {
            SetState(AIState.Chasing);
        }
    }

    private void UpdateChasingState(float distanceToPlayer)
    {
        if (distanceToPlayer <= shootRange)
        {
            SetState(AIState.Attacking);
        }
        else if (distanceToPlayer > sightRange)
        {
            SetState(AIState.Idle);
        }
        else
        {
            Vector3 direction = (player.position - transform.position).normalized;
            direction.y = 0; // Keep the enemy's movement on a flat plane.
            moveDirection = direction;
        }
    }

    private void UpdateAttackingState(float distanceToPlayer)
    {
        if (distanceToPlayer > shootRange)
        {
            SetState(AIState.Chasing);
            return;
        }

        // Manage attack cooldown
        fireTimer += Time.deltaTime;
        if (fireTimer >= fireRate)
        {
            fireTimer = 0f;
            Shoot();
        }
    }

    // --- MODIFIED METHOD ---
    private void Shoot()
    {
        // 1. Calculate the projectile's movement direction in 3D world space.
        Vector3 direction = (player.position - firePoint.position).normalized;

        // --- FINAL WORLD-SPACE LOGIC ---
        // This calculates the angle on the flat XZ ground plane.
        float angle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;

        // We negate the angle to make the rotation direction correct for Unity's system.
        angle = -angle;

        // *** THE FIX IS HERE: We add 180 degrees to flip the result. ***
        angle += 180f;

        Quaternion projectileRotation = Quaternion.Euler(0, 0, angle);
        // --- END OF NEW LOGIC ---

        // Instantiate the parent object with a neutral rotation
        GameObject projectile = Instantiate(projectilePrefab, firePoint.position, Quaternion.identity);

        // Set a 3-second lifetime for the projectile
        Destroy(projectile, 3f);

        // Find the "Visuals" child and apply the calculated rotation ONLY to it
        Transform visualsChild = projectile.transform.Find("Visuals");
        if (visualsChild != null)
        {
            visualsChild.rotation = projectileRotation;
        }
        else
        {
            projectile.transform.rotation = projectileRotation;
        }

        // Assign stats to the projectile's damage script
        var projectileDamageScript = projectile.GetComponentInChildren<EnemyProjectileDamage3D>();
        if (projectileDamageScript != null)
        {
            projectileDamageScript.CasterStats = myStats;
        }

        // Apply velocity to the projectile's rigidbody
        Rigidbody projRb = projectile.GetComponent<Rigidbody>();
        if (projRb != null)
        {
            float projectileSpeed = 15f;
            projRb.linearVelocity = direction * projectileSpeed;
        }
    }

    // --- DEBUG: VISUAL GIZMOS ---
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, sightRange); // Yellow for sight range

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, shootRange); // Red for attack range
    }
}