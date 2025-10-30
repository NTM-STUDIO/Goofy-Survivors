using UnityEngine;
using System.Collections;

/// <summary>
/// Comportamento que faz o inimigo perseguir o jogador, atacar e recuar com um dash.
/// Requer os componentes EnemyMovement e DashBehaviour no mesmo GameObject.
/// </summary>
[RequireComponent(typeof(DashBehaviour))]
public class AttackAndRetreatBehaviour : EnemyBehaviour
{
    public enum EnemyState
    {
        Idle,
        Chasing,
        Attacking,
        Retreating,
        Cooldown
    }

    [Header("State (Read Only)")]
    [SerializeField] private EnemyState currentState = EnemyState.Idle;

    [Header("Attack & Detection")]
    [Tooltip("A que distância o inimigo começa a perseguir o jogador.")]
    public float detectionRange = 20f;
    [Tooltip("A que distância o inimigo pode atacar.")]
    public float attackRange = 3f;
    [Tooltip("Tempo em segundos entre ataques.")]
    public float attackCooldown = 2.5f;
    [Tooltip("Duração do cooldown após recuar, antes de voltar a perseguir.")]
    public float retreatCooldown = 1.5f;

    private Transform player;
    private DashBehaviour dashBehaviour;
    private float lastAttackTime;

    protected override void Awake()
    {
        base.Awake();
        
        // Encontrar o jogador pela tag "Player"
        var playerObject = GameObject.FindGameObjectWithTag("Player");
        if (playerObject != null)
        {
            player = playerObject.transform;
        }
        else
        {
            Debug.LogError("[AttackAndRetreat] Jogador não encontrado! Certifique-se que o jogador tem a tag 'Player'.", this);
            //enabled = false;
            return;
        }

        // Obter a referência para o DashBehaviour
        dashBehaviour = GetComponent<DashBehaviour>();
        if (dashBehaviour == null)
        {
            Debug.LogError("[AttackAndRetreat] DashBehaviour não encontrado! Adicione o componente DashBehaviour a este GameObject.", this);
            //enabled = false;
        }

        // Permite que o primeiro ataque seja imediato
        lastAttackTime = -attackCooldown;
    }

    private void OnEnable()
    {
        // Ensure the script starts in the Idle state when enabled
        currentState = EnemyState.Idle;
    }

    public override void Execute()
    {
        if (!player || !enabled) return;

        // A lógica de execução é gerida por um Coroutine para lidar com estados e esperas.
        // A chamada é feita aqui para se integrar com o EnemyBehaviourController.
    }

    void Update()
    {
        if (!player || !enabled) return;

        HandleStateMachine();
    }

    private void HandleStateMachine()
    {
        float distanceToPlayer = Vector2.Distance(transform.position, player.position);

        switch (currentState)
        {
            case EnemyState.Idle:
                HandleIdleState(distanceToPlayer);
                break;

            case EnemyState.Chasing:
                HandleChasingState(distanceToPlayer);
                break;

            case EnemyState.Attacking:
                HandleAttackingState();
                break;

            case EnemyState.Retreating:
                // O estado de recuo é gerido pelo Coroutine, aqui não fazemos nada.
                break;
            
            case EnemyState.Cooldown:
                // O estado de cooldown é gerido pelo Coroutine, aqui não fazemos nada.
                break;
        }
    }

    private void HandleIdleState(float distanceToPlayer)
    {
        if (distanceToPlayer <= detectionRange && Time.time >= lastAttackTime + attackCooldown)
        {
            currentState = EnemyState.Chasing;
        }
    }

    private void HandleChasingState(float distanceToPlayer)
    {
        if (distanceToPlayer <= attackRange)
        {
            movement.TargetDirection = Vector3.zero;
            currentState = EnemyState.Attacking;
        }
        else if (distanceToPlayer > detectionRange)
        {
            movement.TargetDirection = Vector3.zero;
            currentState = EnemyState.Idle;
        }
        else
        {
            // Perseguir o jogador apenas no plano XZ
            Vector3 direction = (player.position - transform.position);
            direction.y = 0; // Remove o movimento no eixo Z
            movement.TargetDirection = direction.normalized;
        }
    }

    private void HandleAttackingState()
    {
        if (Time.time < lastAttackTime + attackCooldown)
        {
            // Se ainda está em cooldown, volta a perseguir
            currentState = EnemyState.Chasing;
            return;
        }
        
        // Garante que o inimigo está virado para o jogador ao atacar
        stats.FlipTowards(player.position);

        // Executa o ataque e o recuo
        StartCoroutine(AttackAndRetreatSequence());
    }

    private Vector3 GetRandomEdgePosition()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null) return transform.position;

        // Define the edges of the screen in world coordinates
        Vector3 bottomLeft = mainCamera.ViewportToWorldPoint(new Vector3(0, 0, mainCamera.nearClipPlane));
        Vector3 topLeft = mainCamera.ViewportToWorldPoint(new Vector3(0, 1, mainCamera.nearClipPlane));
        Vector3 bottomRight = mainCamera.ViewportToWorldPoint(new Vector3(1, 0, mainCamera.nearClipPlane));
        Vector3 topRight = mainCamera.ViewportToWorldPoint(new Vector3(1, 1, mainCamera.nearClipPlane));

        // Randomly select one of the edges
        int edge = Random.Range(0, 4);
        switch (edge)
        {
            case 0: // Bottom edge
                return Vector3.Lerp(bottomLeft, bottomRight, Random.value);
            case 1: // Top edge
                return Vector3.Lerp(topLeft, topRight, Random.value);
            case 2: // Left edge
                return Vector3.Lerp(bottomLeft, topLeft, Random.value);
            case 3: // Right edge
                return Vector3.Lerp(bottomRight, topRight, Random.value);
            default:
                return transform.position;
        }
    }

    private IEnumerator AttackAndRetreatSequence()
    {
        // --- FASE DE ATAQUE ---
        currentState = EnemyState.Attacking;
        movement.TargetDirection = Vector3.zero;

        // Placeholder para a lógica de ataque (ex: animação, causar dano)
        Debug.Log($"<color=red>ATAQUE!</color> Inimigo {gameObject.name} atacou o jogador.");
        // Aqui poderias chamar uma função para causar dano real ao jogador.
        // Ex: player.GetComponent<PlayerHealth>().TakeDamage(stats.damage);

        lastAttackTime = Time.time;

        // --- FASE DE RECUO EM LINHA RETA ---
        Vector3 retreatDirection = (transform.position - player.position).normalized;
        dashBehaviour.PerformDash(retreatDirection);

        // Espera o dash terminar
        yield return new WaitForSeconds(dashBehaviour.dashDuration);

        // --- FASE DE COOLDOWN ---
        currentState = EnemyState.Cooldown;

        // Espera o cooldown de recuo antes de voltar a perseguir
        yield return new WaitForSeconds(retreatCooldown);

        // Volta ao estado Idle para reavaliar a situação
        currentState = EnemyState.Idle;
    }

    private void OnDrawGizmosSelected()
    {
        if (!enabled) return;

        // Desenhar o alcance de detecção
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Desenhar o alcance de ataque
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, attackRange);
    }
}
