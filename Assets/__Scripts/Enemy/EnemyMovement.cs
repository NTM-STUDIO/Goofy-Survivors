using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class EnemyMovement : MonoBehaviour
{
    // ... (Your existing serialized fields remain the same)
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

    // --- NEW ATTACK AND KNOCKBACK FIELDS ---
    [Header("Attack")]
    [Tooltip("How often the enemy can deal damage while touching the player (seconds).")]
    [SerializeField] private float attackCooldown = 1.5f;
    [Tooltip("The force of the knockback applied to this enemy after it attacks.")]
    [SerializeField] private float selfKnockbackForce = 50f;
    [Tooltip("The duration of the self-knockback stun.")]
    [SerializeField] private float selfKnockbackDuration = 0.5f; // Shortened for better feel

    [Header("Debug")]
    [SerializeField] private bool showGizmos = true;

    // --- Private Variables ---
    private Transform player;
    private Rigidbody playerRb;
    private Rigidbody rb;
    private EnemyStats stats;
    private float flankSign = 1f;
    private float nextAttackTime = 0f; // Cooldown timer for attacks
    
    // (Debug variables remain the same)
    private Vector3 debugIntercept;
    private Vector3 debugDestination;
    private bool hasDebugTarget;

    private void Awake()
    {
        // (Awake logic is unchanged and still correct)
        rb = GetComponent<Rigidbody>();
        stats = GetComponent<EnemyStats>();
        GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null) {
            player = playerGO.transform;
            playerRb = player.GetComponent<Rigidbody>();
        } else {
            Debug.LogError("CRITICAL: Player transform not found!", gameObject);
        }
        if (randomizeOnAwake) RandomiseBehaviour();
        flankSign = Random.value < 0.5f ? -1f : 1f;
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
    }

    private void FixedUpdate()
    {
        // Check for knockback FIRST. This is crucial.
        if (stats.IsKnockedBack)
        {
            return;
        }

        if (stats == null || rb == null || player == null) return;
        
        Vector3 enemyPosition = transform.position;
        Vector3 targetPosition = GetTargetPosition(enemyPosition);
        Vector3 direction = targetPosition - enemyPosition;
        direction.y = 0;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            // FIX: The property is named 'velocity', not 'linearVelocity'.
            rb.linearVelocity = Vector3.zero;
            return;
        }

        // FIX: The property is named 'velocity', not 'linearVelocity'.
        rb.linearVelocity = direction.normalized * stats.moveSpeed;
        debugDestination = targetPosition;
        hasDebugTarget = true;
    }

    // --- FIX: Renamed to OnTriggerStay for continuous contact and added cooldown reset ---
    private void OnTriggerStay(Collider other)
    {
        // Check if we collided with the player and if our attack is off cooldown
        if (other.CompareTag("Player") && Time.time >= nextAttackTime)
        {
            // FIX: Set the cooldown for the NEXT attack
            nextAttackTime = Time.time + attackCooldown;

            // 1. Deal Damage to the Player
            PlayerStats playerStats = other.GetComponentInParent<PlayerStats>();
            if (playerStats != null)
            {
                playerStats.ApplyDamage(stats.GetAttackDamage());
            }

            // 2. Apply Knockback to this Enemy (itself)
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
            // FIX: The property is named 'velocity', not 'linearVelocity'.
            Vector3 velocity = playerRb ? playerRb.linearVelocity : Vector3.zero;
            velocity.y = 0;
            if (velocity.sqrMagnitude < 0.001f)
            {
                velocity = (playerPosition - enemyPosition).normalized * stats.moveSpeed;
                velocity.y = 0;
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
        // (This function remains unchanged)
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
        // (This function remains unchanged)
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