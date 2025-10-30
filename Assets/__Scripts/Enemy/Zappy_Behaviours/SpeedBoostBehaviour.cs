using UnityEngine;
using System.Collections;

/// <summary>
/// Comportamento que aumenta temporariamente a velocidade do inimigo.
/// Pode ser usado sozinho ou em combinação com outros comportamentos.
/// Configure os parâmetros diretamente no Inspector.
/// </summary>
public class SpeedBoostBehaviour : EnemyBehaviour
{
    [Header("Speed Boost Settings")]
    [Tooltip("Multiplicador de velocidade")]
    public float speedMultiplier = 2f;
    
    [Tooltip("Duração do boost em segundos")]
    public float boostDuration = 2f;
    
    [Tooltip("Tempo entre boosts")]
    public float boostCooldown = 5f;
    
    [Header("Activation")]
    [Tooltip("Ativar automaticamente quando a vida estiver baixa")]
    public bool activateOnLowHealth = true;
    
    [Range(0f, 1f)]
    [Tooltip("Percentual de vida para ativar (0-1)")]
    public float lowHealthThreshold = 0.3f;
    
    [Tooltip("Ativar periodicamente mesmo com vida cheia")]
    public bool periodicActivation = true;
    
    [Header("Visual Feedback")]
    [Tooltip("Mudar cor durante o boost")]
    public bool changeColorOnBoost = true;
    
    [Tooltip("Cor durante o boost")]
    public Color boostColor = Color.cyan;
    
    [Header("Z-Path Movement")]
    [Tooltip("Enable Z-path movement during speed boost")]
    public bool enableZPathMovement = true;

    [Tooltip("Distance for each segment of the Z-path")]
    public float zPathSegmentDistance = 2f;

    [Tooltip("Number of segments in the Z-path")]
    public int zPathSegments = 3;
    
    [Header("Afterimage Effect")]
    [Tooltip("Enable afterimage effect during speed boost")]
    public bool enableAfterimageEffect = true;

    private float lastBoostTime = -999f;
    private float boostEndTime;
    private bool isBoosting;
    private bool hasActivatedLowHealthBoost;
    private SpriteRenderer spriteRenderer;
    private Color originalColor;
    private Vector3[] zPathPoints;
    private int currentZPathIndex;
    private AfterimageEffect afterimageEffect;
    
    protected override void Awake()
    {
        base.Awake();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }

        afterimageEffect = GetComponent<AfterimageEffect>();
        if (afterimageEffect == null)
        {
            afterimageEffect = gameObject.AddComponent<AfterimageEffect>();
        }
    }
    
    public override void Execute()
    {
        // Verificar ativação por vida baixa
        if (activateOnLowHealth && !hasActivatedLowHealthBoost && stats != null)
        {
            float healthPercentage = stats.CurrentHealth / stats.MaxHealth;
            if (healthPercentage <= lowHealthThreshold)
            {
                ActivateSpeedBoost();
                hasActivatedLowHealthBoost = true;
                return;
            }
        }
        
        // Ativação periódica
        if (periodicActivation && !isBoosting && Time.time - lastBoostTime >= boostCooldown)
        {
            ActivateSpeedBoost();
        }
        
        // Desativar boost quando terminar
        if (isBoosting && Time.time >= boostEndTime)
        {
            DeactivateSpeedBoost();
        }

        if (isBoosting && enableZPathMovement)
        {
            MoveAlongZPath();
        }
    }
    
    private void ActivateSpeedBoost()
    {
        if (stats == null) return;
        
        isBoosting = true;
        lastBoostTime = Time.time;
        boostEndTime = Time.time + boostDuration;
        
        stats.moveSpeed *= speedMultiplier;
        
        if (changeColorOnBoost && spriteRenderer != null)
        {
            spriteRenderer.color = boostColor;
        }

        if (enableZPathMovement)
        {
            CalculateZPath();
        }

        if (enableAfterimageEffect && afterimageEffect != null)
        {
            afterimageEffect.StartEffect();
        }
    }
    
    private void DeactivateSpeedBoost()
    {
        if (stats == null) return;
        
        isBoosting = false;
        stats.moveSpeed /= speedMultiplier;
        
        if (changeColorOnBoost && spriteRenderer != null)
        {
            spriteRenderer.color = originalColor;
        }

        if (enableAfterimageEffect && afterimageEffect != null)
        {
            afterimageEffect.StopEffect();
        }
    }

    private void CalculateZPath()
    {
        zPathPoints = new Vector3[zPathSegments];
        Vector3 startPosition = transform.position;
        bool isRight = true;

        for (int i = 0; i < zPathSegments; i++)
        {
            float xOffset = isRight ? zPathSegmentDistance : -zPathSegmentDistance;
            zPathPoints[i] = startPosition + new Vector3(xOffset, 0, i * zPathSegmentDistance);
            isRight = !isRight;
        }
        currentZPathIndex = 0;
    }

    private void MoveAlongZPath()
    {
        if (zPathPoints == null || zPathPoints.Length == 0) return;

        Vector3 targetPosition = zPathPoints[currentZPathIndex];
        transform.position = Vector3.MoveTowards(transform.position, targetPosition, stats.moveSpeed * Time.deltaTime);

        if (Vector3.Distance(transform.position, targetPosition) < 0.1f)
        {
            currentZPathIndex++;
            if (currentZPathIndex >= zPathPoints.Length)
            {
                currentZPathIndex = 0; // Loop back to the start
            }
        }
    }
    
    /// <summary>
    /// Força a ativação do boost de velocidade.
    /// </summary>
    public void ForceActivate()
    {
        if (!isBoosting)
        {
            ActivateSpeedBoost();
        }
    }
    
    public bool IsBoosting => isBoosting;
    
    private void OnDestroy()
    {
        // Garantir que a velocidade seja restaurada ao destruir
        if (isBoosting && stats != null)
        {
            stats.moveSpeed /= speedMultiplier;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!enabled) return;

        // Desenhar o alcance de ativação por vida baixa
        if (activateOnLowHealth)
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position, 1f); // Apenas um indicador visual
        }

        // Desenhar o tempo de cooldown como um círculo
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, boostCooldown);
    }
}
