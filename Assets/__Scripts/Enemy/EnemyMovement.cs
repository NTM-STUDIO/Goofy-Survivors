using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody))]
public class EnemyMovement : NetworkBehaviour
{
    private enum PursuitBehaviour { Direct, Predictive, PredictiveFlank }

    [Header("Behaviour")]
    [SerializeField] private PursuitBehaviour behaviour = PursuitBehaviour.Direct;
    [SerializeField] private bool randomizeOnAwake = true;
    [SerializeField, Range(0f, 1f)] private float predictiveChance = 0.6f;
    [SerializeField, Range(0f, 1f)] private float flankChance = 0.3f;

    [Header("Prediction")]
    [SerializeField] private float leadTime = 0.5f;

    [Header("Flank")]
    [SerializeField] private float flankOffset = 2f;
    [SerializeField] private float flankFalloffDistance = 2f;

    [Header("Attack")]
    [SerializeField] private float attackCooldown = 1.5f;
    [SerializeField] private float selfKnockbackForce = 50f;
    [SerializeField] private float selfKnockbackDuration = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool showGizmos = true;

    private const float horizontalNerfFactor = 0.56f; // same as player Movement.cs

    private Transform player;
    private Rigidbody playerRb;
    private Rigidbody rb;
    private EnemyStats stats;
    private float flankSign = 1f;
    private float nextAttackTime = 0f;
    private Vector3 targetDirection = Vector3.zero;

    private Vector3 debugIntercept;
    private Vector3 debugDestination;
    private bool hasDebugTarget;
    private EnemyPathfinding pathfindingComponent;

    public Vector3 TargetDirection
    {
        get => targetDirection;
        set => targetDirection = value;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        stats = GetComponent<EnemyStats>();

        GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null)
        {
            player = playerGO.transform;
            playerRb = playerGO.GetComponent<Rigidbody>();
        }
        else
        {
            Debug.LogError("Player not found!", gameObject);
        }

        if (randomizeOnAwake) RandomiseBehaviour();
        flankSign = Random.value < 0.5f ? -1f : 1f;

        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;

        pathfindingComponent = GetComponent<EnemyPathfinding>();
    }

    private void FixedUpdate()
    {
    // Only the server should simulate enemy movement. Clients receive position updates via NetworkTransform or custom sync.
    if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !IsServer) return;

        if (stats.IsKnockedBack || player == null)
        {
            rb.linearVelocity = Vector3.zero; // Ensure no movement during knockback
            return;
        }

        Vector3 direction;

        // Use pathfinding direction if available
        if (targetDirection != Vector3.zero)
        {
            direction = targetDirection;
            targetDirection = Vector3.zero;
        }
        else
        {
            Vector3 targetPosition = GetTargetPosition(transform.position);
            direction = targetPosition - transform.position;
        }

        // --- START OF CORRECTED MOVEMENT LOGIC ---

        // Ensure movement is only on the XZ plane
        direction.y = 0;

        // Safety check: If the direction is negligible, stop moving completely.
        if (direction.sqrMagnitude < 0.0001f)
        {
            rb.linearVelocity = Vector3.zero;
            hasDebugTarget = false;
            return; // Exit if there's no movement to be done
        }

        // First, normalize the vector to get a pure direction with a length of 1.
        Vector3 moveDirection = direction.normalized;

        // NOW, apply the horizontal nerf to the X component of the normalized vector.
        moveDirection.x *= horizontalNerfFactor;

        // Apply the final speed to the modified direction.
        rb.linearVelocity = moveDirection * stats.moveSpeed;

        // Update debug visualization
        debugDestination = transform.position + moveDirection * 5f; // Multiplied for better visibility
        hasDebugTarget = true;

        // --- END OF CORRECTED MOVEMENT LOGIC ---
    }

    private void OnTriggerStay(Collider other)
    {
    // Only server should handle attack hits to remain authoritative.
    if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !IsServer) return;

        if (other.CompareTag("Player") && Time.time >= nextAttackTime)
        {
            nextAttackTime = Time.time + attackCooldown;
            PlayerStats playerStats = other.GetComponentInParent<PlayerStats>();
            if (playerStats != null)
            {
                playerStats.ApplyDamage(stats.GetAttackDamage());
            }
            Vector3 knockbackDirection = (transform.position - player.position).normalized;
            stats.ApplyKnockback(selfKnockbackForce, selfKnockbackDuration, knockbackDirection);
        }
    }

    private Vector3 GetTargetPosition(Vector3 enemyPosition)
    {
        if (player == null) return enemyPosition;
        Vector3 playerPosition = player.position;
        Vector3 predicted = playerPosition;

        if (behaviour != PursuitBehaviour.Direct)
        {
            Vector3 velocity = playerRb ? playerRb.linearVelocity : Vector3.zero;
            velocity.y = 0;
            if (velocity.sqrMagnitude < 0.001f)
            {
                velocity = (playerPosition - enemyPosition).normalized * stats.moveSpeed;
            }
            predicted += velocity * Mathf.Max(0f, leadTime);
        }

        debugIntercept = predicted;
        debugIntercept.y = transform.position.y;

        if (behaviour != PursuitBehaviour.PredictiveFlank) return predicted;

        Vector3 toTarget = predicted - enemyPosition;
        toTarget.y = 0;
        if (toTarget.sqrMagnitude <= 0.0001f) return predicted;

        Vector3 perpendicular = new Vector3(-toTarget.z, 0, toTarget.x).normalized;
        float distance = toTarget.magnitude;
        float falloff = flankFalloffDistance <= 0f ? 1f : Mathf.Clamp01(distance / flankFalloffDistance);
        return predicted + perpendicular * flankOffset * flankSign * falloff;
    }

    private void RandomiseBehaviour()
    {
        float roll = Random.value;
        float flankThreshold = Mathf.Clamp01(flankChance);
        float predictiveThreshold = Mathf.Clamp01(flankThreshold + predictiveChance);

        if (roll < flankThreshold) behaviour = PursuitBehaviour.PredictiveFlank;
        else if (roll < predictiveThreshold) behaviour = PursuitBehaviour.Predictive;
        else behaviour = PursuitBehaviour.Direct;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (!showGizmos || !Application.isPlaying || !hasDebugTarget) return;

        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(debugIntercept, 0.12f);
        Gizmos.color = Color.magenta;
        Gizmos.DrawSphere(debugDestination, 0.12f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, debugDestination);
    }
#endif
}