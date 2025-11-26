using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic; // Necessário para Listas

[RequireComponent(typeof(Rigidbody))]
public class EnemyMovement : NetworkBehaviour
{
    private enum PursuitBehaviour { Direct, Predictive, PredictiveFlank }

    [Header("Behaviour")]
    [SerializeField] private PursuitBehaviour behaviour = PursuitBehaviour.Direct;
    [SerializeField] private bool randomizeOnAwake = true;
    [SerializeField, Range(0f, 1f)] private float predictiveChance = 0.6f;
    [SerializeField, Range(0f, 1f)] private float flankChance = 0.3f;

    [Header("Targeting")]
    [Tooltip("De quanto em quanto tempo o inimigo procura um novo alvo.")]
    [SerializeField] private float targetUpdateInterval = 1f;

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
    private Transform currentTarget; // O jogador alvo atual
    private Rigidbody targetRb;      // O Rigidbody do alvo (para predição)

    private Rigidbody rb;
    private EnemyStats stats;
    private float flankSign = 1f;
    private float nextAttackTime = 0f;

    // Variáveis para o sistema de busca individual
    private float searchTimer;

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

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        // Inicializa o timer com um valor aleatório para evitar lag spikes (todos calcularem no mesmo frame)
        searchTimer = Random.Range(0f, targetUpdateInterval);
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

        // --- TARGET UPDATE LOGIC (NOVO) ---
        // Verifica se está na hora de procurar o jogador mais próximo
        searchTimer -= Time.fixedDeltaTime;
        if (searchTimer <= 0f)
        {
            FindLocalClosestPlayer();
            searchTimer = targetUpdateInterval;
        }

        // Se não tiver alvo, fica parado
        if (currentTarget == null)
        {
            rb.linearVelocity = Vector3.zero;
            hasDebugTarget = false;
            return;
        }

        // --- MOVEMENT LOGIC ---
        Vector3 direction;
        if (TargetDirection != Vector3.zero)
        {
            direction = TargetDirection;
            TargetDirection = Vector3.zero; // Consume the direction from pathfinding.
        }
        else
        {
            Vector3 enemyPosition = transform.position;
            // Passamos currentTarget para o método de mira
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
            Vector3 dir = new Vector3(direction.x, 0f, direction.z);
            dir.x *= 0.70710678f;
            velocity = dir.normalized * stats.moveSpeed;
        }

        rb.linearVelocity = velocity;

        debugDestination = transform.position + direction;
        hasDebugTarget = true;
    }

    /// <summary>
    /// Procura na lista do PlayerManager qual jogador está mais perto DESTE inimigo especificamente.
    /// </summary>
    private void FindLocalClosestPlayer()
    {
        // Lista temporária para busca
        List<Transform> potentialTargets = new List<Transform>();

        // 1. Tenta pegar do Manager
        if (PlayerManager.Instance != null && PlayerManager.Instance.ActivePlayers.Count > 0)
        {
            potentialTargets = PlayerManager.Instance.ActivePlayers;
        }
        // 2. Fallback: Procura por Tag
        else
        {
            var players = GameObject.FindGameObjectsWithTag("Player");
            foreach (var p in players) potentialTargets.Add(p.transform);
        }

        Transform bestTarget = null;
        float minSqrDist = float.MaxValue;
        Vector3 myPos = transform.position;

        foreach (Transform t in potentialTargets)
        {
            if (t == null) continue;

            // --- CORREÇÃO AQUI: VERIFICA SE ESTÁ CAÍDO ---
            var stats = t.GetComponent<PlayerStats>();
            
            // Se tiver stats e estiver caído, IGNORA este alvo
            if (stats != null && stats.IsDowned) continue;
            // ---------------------------------------------

            float sqrDist = (t.position - myPos).sqrMagnitude;
            if (sqrDist < minSqrDist)
            {
                minSqrDist = sqrDist;
                bestTarget = t;
            }
        }

        // Atualiza o alvo (se for null, o inimigo para)
        currentTarget = bestTarget;
        
        if (currentTarget != null)
        {
            targetRb = currentTarget.GetComponent<Rigidbody>();
        }
        else
        {
            targetRb = null;
            // Opcional: Podes mandar o inimigo passear aleatoriamente se não houver alvos
        }
    }

    private Vector3 ComputeWorldVelocityForEqualScreenSpeed(Vector3 worldDir, Camera cam, float worldSpeed)
    {
        // ... (O teu código matemático mantém-se igual, removi para poupar espaço aqui na resposta, mas deves mantê-lo) ...
        // Se quiseres que eu o reescreva aqui diz, mas a lógica matemática não precisa de mudar.

        // CÓDIGO ORIGINAL MANTIDO PARA ESTA FUNÇÃO
        const float eps = 0.05f;
        Vector3 pos = transform.position;
        Vector3 screenPos = cam.WorldToScreenPoint(pos);
        Vector3 s_dx = cam.WorldToScreenPoint(pos + new Vector3(eps, 0f, 0f)) - screenPos;
        Vector3 s_dz = cam.WorldToScreenPoint(pos + new Vector3(0f, 0f, eps)) - screenPos;
        s_dx.z = 0f; s_dz.z = 0f;

        if (s_dx.sqrMagnitude < 1e-8f || s_dz.sqrMagnitude < 1e-8f) return worldDir.normalized * worldSpeed;

        float a11 = s_dx.x / eps; float a21 = s_dx.y / eps;
        float a12 = s_dz.x / eps; float a22 = s_dz.y / eps;

        Vector3 screenTarget = cam.WorldToScreenPoint(transform.position + worldDir) - screenPos;
        screenTarget.z = 0f;
        if (screenTarget.sqrMagnitude < 1e-6f) return worldDir.normalized * worldSpeed;

        Vector3 screenDirNorm = screenTarget.normalized;
        float pixelsPerWorldX = s_dx.magnitude / eps;
        float pixelsPerWorldZ = s_dz.magnitude / eps;
        float baselinePixelsPerWorld = (pixelsPerWorldX + pixelsPerWorldZ) * 0.5f;
        float desiredScreenSpeed = baselinePixelsPerWorld * worldSpeed;

        Vector2 desiredScreenVel = screenDirNorm * desiredScreenSpeed;
        float dt = Time.fixedDeltaTime;
        Vector2 desiredScreenDelta = desiredScreenVel * dt;

        float det = a11 * a22 - a12 * a21;
        if (Mathf.Abs(det) < 1e-8f) return worldDir.normalized * worldSpeed;

        float inv11 = a22 / det; float inv12 = -a12 / det;
        float inv21 = -a21 / det; float inv22 = a11 / det;

        float worldDeltaX = inv11 * desiredScreenDelta.x + inv12 * desiredScreenDelta.y;
        float worldDeltaZ = inv21 * desiredScreenDelta.x + inv22 * desiredScreenDelta.y;

        float worldVelX = worldDeltaX / dt;
        float worldVelZ = worldDeltaZ / dt;

        Vector3 result = new Vector3(worldVelX, 0f, worldVelZ);
        if (float.IsNaN(result.x) || float.IsNaN(result.z) || float.IsInfinity(result.x) || float.IsInfinity(result.z))
            return worldDir.normalized * worldSpeed;

        return result;
    }

   private void OnTriggerEnter(Collider other)
    {
        // Se for Multiplayer e eu não for o Servidor, ignoro (apenas o servidor calcula dano)
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !IsServer) return;

        if (other.CompareTag("Player") && Time.time >= nextAttackTime)
        {
            var targetPs = other.GetComponentInParent<PlayerStats>();
            if (targetPs == null || targetPs.IsDowned) return;

            nextAttackTime = Time.time + attackCooldown;

            // Tenta obter o ID. Se for SP, o netObj pode ser null, enviamos 0.
            ulong targetId = 0;
            var netObj = other.GetComponentInParent<NetworkObject>();
            if (netObj != null) targetId = netObj.OwnerClientId;

            if (GameManager.Instance != null)
            {
                float dmg = stats.GetAttackDamage();
                // Chama o GameManager (que agora tem a lógica SP/MP separada)
                GameManager.Instance.ServerApplyPlayerDamage(targetId, dmg, transform.position, null);
            }

            // Knockback funciona localmente no physics
            Vector3 knockbackDirection = (transform.position - other.transform.position).normalized;
            stats.ApplyKnockback(selfKnockbackForce, selfKnockbackDuration, knockbackDirection);
        }
    }
    private Vector3 GetTargetPosition(Vector3 enemyPosition)
    {
        // Usa currentTarget em vez de player
        if (currentTarget == null) return enemyPosition;
        Vector3 playerPosition = currentTarget.position;
        Vector3 predicted = playerPosition;

        if (behaviour != PursuitBehaviour.Direct)
        {
            // Usa targetRb em vez de playerRb
            Vector3 velocity = targetRb ? targetRb.linearVelocity : Vector3.zero;
            velocity.y = 0;

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