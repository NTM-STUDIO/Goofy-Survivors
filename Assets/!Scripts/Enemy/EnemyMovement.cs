using UnityEngine;

public class EnemyMovement : MonoBehaviour
{
    private enum PursuitBehaviour
    {
        Direct,
        Predictive,
        PredictiveFlank
    }

    [Header("Behaviour")]
    [SerializeField] private PursuitBehaviour behaviour = PursuitBehaviour.Direct;
    [SerializeField] private bool randomizeOnAwake = true;
    [SerializeField, Range(0f, 1f)] private float predictiveChance = 0.6f;
    [SerializeField, Range(0f, 1f)] private float flankChance = 0.3f;

    [Header("Movement")]
    [Tooltip("How quickly the enemy accelerates to its top speed. Higher values are more responsive.")]
    [SerializeField] private float acceleration = 50f;

    [Header("Prediction")]
    [SerializeField] private float leadTime = 0.5f;

    [Header("Flank")]
    [SerializeField] private float flankOffset = 2f;
    [SerializeField] private float flankFalloffDistance = 2f;

    [Header("Collision")]
    [Tooltip("How hard another object must hit this enemy to trigger its knockback state.")]
    [SerializeField] private float minCollisionForceForStun = 5f;

    [Header("Debug")]
    [SerializeField] private bool showGizmos = true;

    private Transform player;
    private Rigidbody2D playerRb;
    private Rigidbody2D rb;
    private EnemyStats stats;
    private float flankSign = 1f;

    private Vector2 debugIntercept;
    private Vector2 debugDestination;
    private bool hasDebugTarget;

    // Grab references and optionally randomise this enemy's behaviour.
    private void Awake()
    {
        player = GameObject.FindGameObjectWithTag("Player")?.transform;
        playerRb = player ? player.GetComponent<Rigidbody2D>() : null;
        rb = GetComponent<Rigidbody2D>();
        stats = GetComponent<EnemyStats>();

        if (randomizeOnAwake)
        {
            RandomiseBehaviour();
        }

        flankSign = Random.value < 0.5f ? -1f : 1f;
    }

    // Move the enemy toward the chosen target point each physics tick.
    private void FixedUpdate()
    {
        if (stats == null || rb == null || player == null || stats.IsKnockedBack)
        {
            hasDebugTarget = false;
            return;
        }

        Vector2 enemyPosition = rb.position;
        Vector2 targetPosition = GetTargetPosition(enemyPosition);

        Vector2 direction = targetPosition - enemyPosition;
        if (direction.sqrMagnitude <= 0.0001f)
        {
            rb.linearVelocity = Vector2.zero; // This is fine for stopping completely.
            hasDebugTarget = false;
            return;
        }

        // --- REVISED MOVEMENT LOGIC ---
        // This new method uses forces to avoid negating knockback.

        // 1. Determine the velocity we WANT to have.
        Vector2 targetVelocity = direction.normalized * stats.moveSpeed;

        // 2. Find the difference between our current velocity and the one we want.
        Vector2 velocityDifference = targetVelocity - rb.linearVelocity;

        // 3. Calculate the force needed to overcome the difference, using our acceleration.
        Vector2 moveForce = velocityDifference * acceleration;

        // 4. Apply the force. This will work WITH the physics engine, not against it.
        rb.AddForce(moveForce, ForceMode2D.Force);

        // --- END OF REVISED LOGIC ---

        debugDestination = targetPosition;
        hasDebugTarget = true;
    }

    /// <summary>
    /// This is the new function that handles chain-reaction knockbacks.
    /// It triggers when another physics object collides with this one.
    /// </summary>
    private void OnCollisionEnter2D(Collision2D collision)
    {
        // If we are already knocked back, ignore new collisions.
        if (stats.IsKnockedBack)
        {
            return;
        }

        // Check if the object that hit us was another enemy with the "Enemy" tag.
        if (collision.gameObject.CompareTag("Enemy"))
        {
            // A collision's "relativeVelocity" tells us how fast the two objects were moving towards each other.
            // A high magnitude means a hard impact.
            if (collision.relativeVelocity.magnitude > minCollisionForceForStun)
            {
                // The other enemy hit us hard enough to cause a stun.
                
                // Calculate a direction away from the point of impact.
                Vector2 knockbackDir = (transform.position - collision.transform.position).normalized;

                // Trigger our own knockback/stun state.
                // We use the impact force (relative velocity) as the strength of the knockback.
                // You can adjust the stun duration (0.25f) as needed.
                stats.ApplyKnockback(collision.relativeVelocity.magnitude, 0.25f, knockbackDir);
            }
        }
    }

    // Decide which position to travel toward based on the configured behaviour.
    private Vector2 GetTargetPosition(Vector2 enemyPosition)
    {
        if (player == null)
        {
            return enemyPosition;
        }

        Vector2 playerPosition = player.position;
        Vector2 predicted = playerPosition;

        if (behaviour != PursuitBehaviour.Direct)
        {
            Vector2 velocity = playerRb ? playerRb.linearVelocity : Vector2.zero;

            if (velocity.sqrMagnitude < 0.001f)
            {
                velocity = (playerPosition - enemyPosition).normalized * stats.moveSpeed;
            }

            predicted += velocity * Mathf.Max(0f, leadTime);
        }

        debugIntercept = predicted;

        if (behaviour != PursuitBehaviour.PredictiveFlank)
        {
            return predicted;
        }

        Vector2 toTarget = predicted - enemyPosition;
        if (toTarget.sqrMagnitude <= 0.0001f)
        {
            return predicted;
        }

        Vector2 perpendicular = new Vector2(-toTarget.y, toTarget.x).normalized;
        float distance = toTarget.magnitude;
        float falloff = flankFalloffDistance <= 0f ? 1f : Mathf.Clamp01(distance / flankFalloffDistance);

        return predicted + perpendicular * flankOffset * flankSign * falloff;
    }

    // Roll a behaviour using the configured probabilities.
    private void RandomiseBehaviour()
    {
        float roll = Random.value;
        float flankThreshold = Mathf.Clamp01(flankChance);
        float predictiveThreshold = Mathf.Clamp01(flankThreshold + predictiveChance);

        if (roll < flankThreshold)
        {
            behaviour = PursuitBehaviour.PredictiveFlank;
        }
        else if (roll < predictiveThreshold)
        {
            behaviour = PursuitBehaviour.Predictive;
        }
        else
        {
            behaviour = PursuitBehaviour.Direct;
        }
    }

#if UNITY_EDITOR
    // Show the predicted position and target path while in play mode.
    private void OnDrawGizmos()
    {
        if (!showGizmos || !Application.isPlaying || !hasDebugTarget)
        {
            return;
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawSphere(debugIntercept, 0.12f);

        Gizmos.color = Color.magenta;
        Gizmos.DrawSphere(debugDestination, 0.12f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, debugDestination);
    }
#endif
}