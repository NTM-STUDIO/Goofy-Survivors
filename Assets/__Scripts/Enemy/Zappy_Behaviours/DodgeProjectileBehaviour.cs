/*using UnityEngine;

/// <summary>
/// Comportamento que detecta projéteis próximos e usa o dash para desviar.
/// Funciona em conjunto com DashBehaviour.
/// Configure os parâmetros diretamente no Inspector.
/// </summary>
public class DodgeProjectileBehaviour : EnemyBehaviour
{
    [Header("Detection Settings")]
    [Tooltip("Raio de detecção de projéteis")]
    public float detectionRadius = 5f;
    
    [Tooltip("Layer dos projéteis")]
    public LayerMask projectileLayer;
    
    [Header("Dodge Settings")]
    [Range(0f, 1f)]
    [Tooltip("Chance de desviar quando detectar projétil (0-1)")]
    public float dodgeChance = 0.8f;
    
    [Tooltip("Tempo mínimo entre desvios")]
    public float dodgeCooldown = 1f;
    
    [Header("Debug")]
    [Tooltip("Mostrar raio de detecção no editor")]
    public bool showDetectionRadius = true;
    
    private DashBehaviour dashBehaviour;
    private float lastDodgeTime;
    
    protected override void Awake()
    {
        base.Awake();
        dashBehaviour = GetComponent<DashBehaviour>();
        
        if (dashBehaviour == null)
        {
            Debug.LogWarning($"DodgeProjectileBehaviour em {gameObject.name} requer DashBehaviour!", this);
        }
    }
    
    public override void Execute()
    {
        if (Time.time - lastDodgeTime < dodgeCooldown) return;
        
        DetectAndDodgeProjectiles();
    }
    
    private void DetectAndDodgeProjectiles()
    {
        Collider[] projectiles = Physics.OverlapSphere(
            transform.position, 
            detectionRadius, 
            projectileLayer
        );
        
        if (projectiles.Length > 0 && Random.value < dodgeChance)
        {
            // Usar dash para evitar o projétil
            if (dashBehaviour != null && !dashBehaviour.IsDashing)
            {
                dashBehaviour.ForceDash();
                lastDodgeTime = Time.time;
            }
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        if (!showDetectionRadius) return;
        
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}
*/