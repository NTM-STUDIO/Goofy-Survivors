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

    [Header("Isometric Nerf (optional)")]
    [Tooltip("If enabled, applies an extra nerf to the world X component to compensate for isometric projection horizontal speed.")]
    [SerializeField] private bool tryNerfHorizontal = false;
    [Tooltip("Multiplier applied to the world X component of movement when nerf is enabled (0-1).")]
    [Range(0f, 1f)]
    [SerializeField] private float horizontalNerfMultiplier = 0.56f;

    // --- Private Variables ---
    private Transform player;
    private Rigidbody playerRb;
    private Rigidbody rb;
    private EnemyStats stats;
    private float flankSign = 1f;
    private float nextAttackTime = 0f;

    // Direction that can be set externally by pathfinding
    public Vector3 TargetDirection { get; set; } = Vector3.zero;

    // Debug variables
    private Vector3 debugIntercept;
    private Vector3 debugDestination;
    private bool hasDebugTarget;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        stats = GetComponent<EnemyStats>();

        if (randomizeOnAwake) RandomiseBehaviour();
        flankSign = Random.value < 0.5f ? -1f : 1f;

        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
    }

    private void FixedUpdate()
    {
        // Only the server should simulate enemy movement.
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !IsServer) return;

        // --- KNOCKBACK FIX ---
        // If knocked back, stop all AI movement and let physics take over.
        if (stats.IsKnockedBack)
        {
            return;
        }

        // --- PERFORMANCE OPTIMIZATION ---
        // Get the player target from the central, high-performance manager.
        if (PlayerTargetManager.Instance != null)
        {
            player = PlayerTargetManager.Instance.ClosestPlayer;
            if (player != null)
            {
                // Cache the player's rigidbody if it exists.
                playerRb = player.GetComponent<Rigidbody>();
            }
        }

        // If the manager can't find a valid player, THEN stop moving.
        if (player == null)
        {
            rb.linearVelocity = Vector3.zero;
            hasDebugTarget = false;
            return;
        }

        Vector3 direction;
        if (TargetDirection != Vector3.zero)
        {
            direction = TargetDirection;
            TargetDirection = Vector3.zero; // Consume the direction from pathfinding.
        }
        else
        {
            Vector3 enemyPosition = transform.position;
            Vector3 targetPosition = GetTargetPosition(enemyPosition);
            direction = targetPosition - enemyPosition;
        }

        // XZ plane only
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            rb.linearVelocity = Vector3.zero;
            hasDebugTarget = false;
            return;
        }

        // Apply diagonal movement compensation
        float diagonalCompensation = (Mathf.Abs(direction.x) > 0.1f && Mathf.Abs(direction.z) > 0.1f) ? 0.70710678f : 1f;
        Vector3 velocity = direction.normalized * stats.moveSpeed * diagonalCompensation;

        // Optional isometric horizontal speed nerf
        if (tryNerfHorizontal)
        {
            velocity.x *= Mathf.Clamp01(horizontalNerfMultiplier);
        }

        rb.linearVelocity = velocity;

        // For gizmo drawing
        debugDestination = transform.position + direction;
        hasDebugTarget = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Server is authoritative for attacks.
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !IsServer) return;

        // The cooldown check is still important to prevent multi-hits if the enemy is pushed back in.
        if (other.CompareTag("Player") && Time.time >= nextAttackTime)
        {
            var targetPs = other.GetComponentInParent<PlayerStats>();
            if (targetPs == null || targetPs.IsDowned) return;

            // Set the cooldown immediately.
            nextAttackTime = Time.time + attackCooldown;

            // Apply damage to the player.
            var netObj = other.GetComponentInParent<NetworkObject>();
            if (netObj != null && GameManager.Instance != null)
            {
                float dmg = stats.GetAttackDamage();
                GameManager.Instance.ServerApplyPlayerDamage(netObj.OwnerClientId, dmg, transform.position, null);
            }

            // Apply knockback to THIS enemy.
            Vector3 knockbackDirection = (transform.position - other.transform.position).normalized;
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
            // A simple fallback if the player is standing still
            if (velocity.sqrMagnitude < 0.001f)
            {
                velocity = (playerPosition - enemyPosition).normalized * stats.moveSpeed;
                velocity.y = 0f;
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