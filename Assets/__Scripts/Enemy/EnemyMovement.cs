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

    [Header("Prediction")]
    [SerializeField] private float leadTime = 0.5f;

    [Header("Flank")]
    [SerializeField] private float flankOffset = 2f;
    [SerializeField] private float flankFalloffDistance = 2f;

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
            rb.linearVelocity = Vector2.zero;
            hasDebugTarget = false;
            return;
        }

        rb.linearVelocity = direction.normalized * stats.moveSpeed;

        debugDestination = targetPosition;
        hasDebugTarget = true;
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