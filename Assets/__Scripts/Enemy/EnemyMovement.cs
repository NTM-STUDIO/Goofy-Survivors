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

    [Header("Isometric / Screen-space Correction")]
    [Tooltip("If true, uses camera-aware screen-space correction to equalize perceived speed.")]
    [SerializeField] private bool useScreenSpaceCorrection = true;

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
        if (stats.IsKnockedBack)
        {
            return;
        }

        // --- PERFORMANCE OPTIMIZATION ---
        if (PlayerTargetManager.Instance != null)
        {
            player = PlayerTargetManager.Instance.ClosestPlayer;
            if (player != null)
            {
                playerRb = player.GetComponent<Rigidbody>();
            }
        }

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

        Vector3 velocity;

        if (useScreenSpaceCorrection && Camera.main != null)
        {
            velocity = ComputeWorldVelocityForEqualScreenSpeed(direction, Camera.main, stats.moveSpeed);
        }
        else
        {
            // Fallback: simple pre-normalization X nerf (if you still want it off)
            Vector3 dir = new Vector3(direction.x, 0f, direction.z);
            dir.x *= 0.70710678f; // optional: classic iso factor
            velocity = dir.normalized * stats.moveSpeed;
        }

        rb.linearVelocity = velocity;

        debugDestination = transform.position + direction;
        hasDebugTarget = true;
    }

    /// <summary>
    /// Compute a world-space velocity (units/sec) that makes the motion appear to have
    /// equal screen-space speed in the direction of 'worldDir'.
    /// This estimates the mapping of small world steps in X/Z to screen space and
    /// then solves a 2x2 system for the needed world delta per second.
    /// </summary>
    private Vector3 ComputeWorldVelocityForEqualScreenSpeed(Vector3 worldDir, Camera cam, float worldSpeed)
    {
        // Small epsilon (in world units) for finite differencing
        const float eps = 0.05f;

        Vector3 pos = transform.position;
        Vector3 screenPos = cam.WorldToScreenPoint(pos);
        // sample how a +eps step in world X and world Z maps to screen (pixels)
        Vector3 s_dx = cam.WorldToScreenPoint(pos + new Vector3(eps, 0f, 0f)) - screenPos;
        Vector3 s_dz = cam.WorldToScreenPoint(pos + new Vector3(0f, 0f, eps)) - screenPos;
        s_dx.z = 0f;
        s_dz.z = 0f;

        // if projection is degenerate for some reason, fallback to simple normalized world direction
        if (s_dx.sqrMagnitude < 1e-8f || s_dz.sqrMagnitude < 1e-8f)
        {
            return worldDir.normalized * worldSpeed;
        }

        // Build 2x2 matrix A where columns are screen-per-world-unit for X and Z
        // A = [ s_dx/eps  s_dz/eps ] (each column is a 2-vector)
        float a11 = s_dx.x / eps;
        float a21 = s_dx.y / eps;
        float a12 = s_dz.x / eps;
        float a22 = s_dz.y / eps;

        // desired screen-direction (pixels) normalized
        Vector3 screenTarget = cam.WorldToScreenPoint(transform.position + worldDir) - screenPos;
        screenTarget.z = 0f;
        if (screenTarget.sqrMagnitude < 1e-6f)
        {
            // extremely close; fallback
            return worldDir.normalized * worldSpeed;
        }

        Vector3 screenDirNorm = screenTarget.normalized;

        // Choose a sensible target *screen speed* (pixels/sec).
        // We'll choose the baseline as the average pixel speed produced by moving 1 world unit along X and Z,
        // scaled by the desired worldSpeed (units/sec). This gives a screen speed "baseline" consistent with worldSpeed.
        float pixelsPerWorldX = s_dx.magnitude / eps;
        float pixelsPerWorldZ = s_dz.magnitude / eps;
        float baselinePixelsPerWorld = (pixelsPerWorldX + pixelsPerWorldZ) * 0.5f;

        // desired screen speed in pixels/sec
        float desiredScreenSpeed = baselinePixelsPerWorld * worldSpeed;

        // desired screen velocity (pixels/sec) vector
        Vector2 desiredScreenVel = screenDirNorm * desiredScreenSpeed;

        // We need desiredScreenDelta over one fixed step:
        float dt = Time.fixedDeltaTime;
        Vector2 desiredScreenDelta = desiredScreenVel * dt; // pixels per fixed step

        // Solve A * worldDelta = desiredScreenDelta
        // Where worldDelta = [dxUnits; dzUnits] (world units moved this fixed step)
        // A = 2x2 matrix built above
        // Compute inverse of A (2x2)
        float det = a11 * a22 - a12 * a21;

        if (Mathf.Abs(det) < 1e-8f)
        {
            // Degenerate mapping — fallback to normalized world direction
            return worldDir.normalized * worldSpeed;
        }

        // inverse A
        float inv11 = a22 / det;
        float inv12 = -a12 / det;
        float inv21 = -a21 / det;
        float inv22 = a11 / det;

        // worldDeltaUnits = A^{-1} * desiredScreenDelta
        float worldDeltaX = inv11 * desiredScreenDelta.x + inv12 * desiredScreenDelta.y;
        float worldDeltaZ = inv21 * desiredScreenDelta.x + inv22 * desiredScreenDelta.y;

        // Now compute world velocity (units/sec) from delta per fixed step
        float worldVelX = worldDeltaX / dt;
        float worldVelZ = worldDeltaZ / dt;

        // Final world velocity vector
        Vector3 result = new Vector3(worldVelX, 0f, worldVelZ);

        // If result is NaN or infinite, fallback
        if (float.IsNaN(result.x) || float.IsNaN(result.z) || float.IsInfinity(result.x) || float.IsInfinity(result.z))
            return worldDir.normalized * worldSpeed;

        return result;
    }

    private void OnTriggerEnter(Collider other)
    {
        // Server is authoritative for attacks.
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !IsServer) return;

        if (other.CompareTag("Player") && Time.time >= nextAttackTime)
        {
            var targetPs = other.GetComponentInParent<PlayerStats>();
            if (targetPs == null || targetPs.IsDowned) return;

            nextAttackTime = Time.time + attackCooldown;

            var netObj = other.GetComponentInParent<NetworkObject>();
            if (netObj != null && GameManager.Instance != null)
            {
                float dmg = stats.GetAttackDamage();
                GameManager.Instance.ServerApplyPlayerDamage(netObj.OwnerClientId, dmg, transform.position, null);
            }

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
