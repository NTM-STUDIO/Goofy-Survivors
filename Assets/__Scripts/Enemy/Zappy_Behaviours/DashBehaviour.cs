using UnityEngine;
using System.Collections;

/// <summary>
/// Comportamento de Dash Rápido com efeito de trail (estilo Flash).
/// O inimigo faz dashes em alta velocidade deixando um rastro luminoso.
/// Configure todos os parâmetros diretamente no Inspector.
/// </summary>
public class DashBehaviour : EnemyBehaviour
{
    [Header("Dash Settings")]
    [Tooltip("Velocidade durante o dash")]
    public float dashSpeed = 30f;
    
    [Tooltip("Duração do dash em segundos")]
    public float dashDuration = 0.3f;
    
    [Tooltip("Tempo entre dashes")]
    public float dashCooldown = 3f;
    
    [Tooltip("Distância máxima do dash")]
    public float dashRange = 10f;
    
    [Header("Trail Settings")]
    [Tooltip("Trail Renderer (auto-criado se vazio)")]
    public TrailRenderer trailRenderer;
    
    [Tooltip("Cor do rastro")]
    public Color trailColor = Color.yellow;
    
    [Tooltip("Tempo que o rastro permanece visível")]
    public float trailTime = 0.5f;
    
    [Tooltip("Largura inicial do rastro")]
    public float trailStartWidth = 0.5f;
    
    [Tooltip("Largura final do rastro")]
    public float trailEndWidth = 0.1f;
    
    [Header("Afterimage Settings")]
    [Tooltip("Ativar clones fantasmas durante o dash")]
    public bool useAfterimages = true;
    
    [Header("Target Settings")]
    [Tooltip("Fazer dash em direção ao jogador")]
    public bool dashTowardsPlayer = true;
    
    [Tooltip("Se não houver jogador, fazer dash aleatório")]
    public bool randomDashIfNoPlayer = true;
    
    private float lastDashTime;
    private bool isDashing;
    private Vector3 dashDirection;
    private AfterimageEffect afterimageEffect;
    private Rigidbody rb;
    
    protected override void Awake()
    {
        base.Awake();
        rb = GetComponent<Rigidbody>();
        SetupTrail();
        
        if (useAfterimages)
        {
            afterimageEffect = gameObject.AddComponent<AfterimageEffect>();
        }
    }
    
    private void SetupTrail()
    {
        if (trailRenderer == null)
        {
            trailRenderer = gameObject.AddComponent<TrailRenderer>();
        }
        
        // Criar material para o trail
        trailRenderer.time = trailTime;
        trailRenderer.startWidth = trailStartWidth;
        trailRenderer.endWidth = trailEndWidth;
        trailRenderer.material = new Material(Shader.Find("Sprites/Default"));
        trailRenderer.startColor = trailColor;
        trailRenderer.endColor = new Color(trailColor.r, trailColor.g, trailColor.b, 0);
        trailRenderer.emitting = false;
        trailRenderer.sortingOrder = -1; // Renderizar atrás do sprite
    }
    
    public override void Execute()
    {
        if (!isDashing && Time.time - lastDashTime >= dashCooldown)
        {
            StartCoroutine(PerformDash(dashDirection));
        }
    }

    public IEnumerator PerformDash(Vector3 direction)
    {
        isDashing = true;
        lastDashTime = Time.time;

        // Definir direção do dash
        if (dashTowardsPlayer)
        {
            Transform player = GameObject.FindGameObjectWithTag("Player")?.transform;
            if (player != null)
            {
                dashDirection = (player.position - transform.position).normalized;
            }
            else if (randomDashIfNoPlayer)
            {
                dashDirection = Random.insideUnitCircle.normalized;
                dashDirection = new Vector3(dashDirection.x, 0, dashDirection.y);
            }
            else
            {
                isDashing = false;
                yield break;
            }
        }
        else
        {
            dashDirection = Random.insideUnitCircle.normalized;
            dashDirection = new Vector3(dashDirection.x, 0, dashDirection.y);
        }

        // Garantir movimento apenas no plano XZ
        dashDirection.y = 0; // Remove qualquer movimento no eixo Y

        // Ativar efeitos visuais
        trailRenderer.emitting = true;
        if (useAfterimages && afterimageEffect != null)
        {
            afterimageEffect.StartEffect();
        }

        // Armazenar velocidade original
        float originalSpeed = stats.moveSpeed;
        stats.moveSpeed = 0; // Parar movimento normal durante o dash

        // Executar o dash diretamente para a direção definida
        float elapsedTime = 0;
        Vector3 startPosition = transform.position;

        while (elapsedTime < dashDuration)
        {
            if (rb != null)
            {
                rb.linearVelocity = dashDirection * dashSpeed;
            }
            else
            {
                transform.position += dashDirection * dashSpeed * Time.deltaTime;
            }

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // Parar o movimento
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
        }

        // Restaurar velocidade original
        stats.moveSpeed = originalSpeed;

        // Desativar efeitos visuais
        yield return new WaitForSeconds(0.1f);
        trailRenderer.emitting = false;
        if (useAfterimages && afterimageEffect != null)
        {
            afterimageEffect.StopEffect();
        }
        
        isDashing = false;
    }
    
    /// <summary>
    /// Força o inimigo a fazer um dash imediatamente (ignora cooldown).
    /// </summary>
    public void ForceDash()
    {
        if (!isDashing)
        {
            lastDashTime = 0;
        }
    }
    
    public bool IsDashing => isDashing;

    private void OnDrawGizmosSelected()
    {
        if (!enabled) return;

        // Desenhar o alcance máximo do dash
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, dashRange);
    }
}