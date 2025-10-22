using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class EnemyMovement : MonoBehaviour
{
    // ... (Todos os seus campos e enums permanecem exatamente iguais)
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
    [Tooltip("How often the enemy can deal damage while touching the player (seconds).")]
    [SerializeField] private float attackCooldown = 1.5f;
    [Tooltip("The force of the knockback applied to this enemy after it attacks.")]
    [SerializeField] private float selfKnockbackForce = 50f;
    [Tooltip("The duration of the self-knockback stun.")]
    [SerializeField] private float selfKnockbackDuration = 0.5f;

    [Header("Debug")]
    [SerializeField] private bool showGizmos = true;

    // --- Private Variables ---
    private Transform player;
    private Rigidbody playerRb;
    private Rigidbody rb;
    private EnemyStats stats;
    private float flankSign = 1f;
    private float nextAttackTime = 0f;
    
    // Debug variables
    private Vector3 debugIntercept;
    private Vector3 debugDestination;
    private bool hasDebugTarget;

    private void Awake()
    {
        // (NENHUMA ALTERAÇÃO AQUI)
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

    // --- MÉTODO MODIFICADO ---
    private void FixedUpdate()
    {
        if (stats.IsKnockedBack)
        {
            return;
        }

        if (stats == null || rb == null || player == null) return;
        
        Vector3 enemyPosition = transform.position;
        Vector3 targetPosition = GetTargetPosition(enemyPosition);
        
        // A direção pura do movimento, equivalente ao 'moveInput' do jogador
        Vector3 direction = targetPosition - enemyPosition;
        direction.y = 0;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            rb.linearVelocity = Vector3.zero;
            return;
        }

        // --- INÍCIO DA LÓGICA PORTADA DO JOGADOR ---

        // PASSO 1: CALCULAR A COMPENSAÇÃO DIAGONAL
        // Usamos a direção do inimigo da mesma forma que o input do jogador é usado.
        float diagonalCompensation = 1f;
        // Se o movimento tem um componente horizontal E vertical significativos, aplica o nerf.
        if (Mathf.Abs(direction.x) > 0.1f && Mathf.Abs(direction.z) > 0.1f)
        {
            // O valor é 1 / sqrt(2), o mesmo que no seu script de jogador.
            diagonalCompensation = 0.70710678f; 
        }

        // PASSO 2: APLICAR A VELOCIDADE FINAL COM A COMPENSAÇÃO
        // A velocidade base é multiplicada pela compensação, exatamente como no seu código.
        rb.linearVelocity = direction.normalized * stats.moveSpeed * diagonalCompensation;
        
        // --- FIM DA LÓGICA PORTADA ---

        debugDestination = targetPosition;
        hasDebugTarget = true;
    }

    // (O resto do seu script, incluindo OnTriggerStay, GetTargetPosition, etc., permanece inalterado)

    private void OnTriggerStay(Collider other)
    {
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