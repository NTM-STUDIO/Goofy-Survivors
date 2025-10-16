using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class EnemyMovement : MonoBehaviour
{
    // --- Enums and Serialized Fields remain the same ---
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
    [Header("Debug")]
    [SerializeField] private bool showGizmos = true;
    // --- Private Variables ---
    private Transform player;
    private Rigidbody playerRb;
    private Rigidbody rb;
    private EnemyStats stats;
    private float flankSign = 1f;
    private Vector3 debugIntercept;
    private Vector3 debugDestination;
    private bool hasDebugTarget;
    private bool isCollidingWithPlayer = false;

    private void Awake()
    {
        // (Awake logic remains the same)
        Debug.Log("--- EnemyMovement Awake() ---", gameObject);
        GameObject playerGO = GameObject.FindGameObjectWithTag("Player");
        if (playerGO != null) {
            player = playerGO.transform;
            playerRb = player.GetComponent<Rigidbody>();
            Debug.Log("Player found successfully.", gameObject);
            if (playerRb == null) {
                Debug.LogWarning("Player was found, but it does not have a Rigidbody component. Predictive movement will not work correctly.", gameObject);
            }
        } else {
            Debug.LogError("CRITICAL: Player transform not found! Make sure the player object is tagged 'Player'.", gameObject);
        }
        rb = GetComponent<Rigidbody>();
        if (rb != null) {
            Debug.Log("Enemy Rigidbody found.", gameObject);
        } else {
            Debug.LogError("CRITICAL: Enemy Rigidbody component is missing.", gameObject);
        }
        stats = GetComponent<EnemyStats>();
        if (stats != null) {
            Debug.Log($"EnemyStats found. Move Speed is: {stats.moveSpeed}", gameObject);
        } else {
            Debug.LogError("CRITICAL: EnemyStats component is missing.", gameObject);
        }
        if (randomizeOnAwake) {
            RandomiseBehaviour();
            Debug.Log($"Behaviour randomized to: {behaviour}", gameObject);
        }
        flankSign = Random.value < 0.5f ? -1f : 1f;
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
    }

    private void FixedUpdate()
    {
        if (stats == null || rb == null || player == null || stats.IsKnockedBack || isCollidingWithPlayer) {
            return;
        }

        Vector3 enemyPosition = transform.position;
        Vector3 targetPosition = GetTargetPosition(enemyPosition);
        Vector3 direction = targetPosition - enemyPosition;
        direction.y = 0;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            rb.linearVelocity = Vector3.zero; // CORRECTED
            hasDebugTarget = false;
            return;
        }

        Vector3 newVelocity = direction.normalized * stats.moveSpeed;
        rb.linearVelocity = newVelocity; // CORRECTED

        debugDestination = targetPosition;
        hasDebugTarget = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player")) {
            isCollidingWithPlayer = true;
            rb.linearVelocity = Vector3.zero; // CORRECTED
            Debug.Log("Entered Player trigger, stopping movement.", gameObject);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player")) {
            isCollidingWithPlayer = false;
            Debug.Log("Exited Player trigger, resuming movement.", gameObject);
        }
    }

    private Vector3 GetTargetPosition(Vector3 enemyPosition)
    {
        if (player == null) return enemyPosition;
        
        Vector3 playerPosition = player.position;
        Vector3 predicted = playerPosition;
        if (behaviour != PursuitBehaviour.Direct)
        {
            Vector3 velocity = playerRb ? playerRb.linearVelocity : Vector3.zero; // CORRECTED
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