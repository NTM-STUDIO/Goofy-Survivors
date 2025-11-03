using UnityEngine;

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
    
    [Header("Dodge Type")]
    [Tooltip("Use speed burst to dodge projectiles")]
    public bool useSpeedBurstDodge = true;
    
    [Header("Debug")]
    [Tooltip("Mostrar raio de detecção no editor")]
    public bool showDetectionRadius = true;
    
    private ZappyMovementBehaviour zappyMovement;
    private float lastDodgeTime;
    
    protected override void Awake()
    {
        base.Awake();
        zappyMovement = GetComponent<ZappyMovementBehaviour>();
        
        if (zappyMovement == null)
        {
            Debug.LogWarning($"DodgeProjectileBehaviour em {gameObject.name} requer ZappyMovementBehaviour!", this);
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
            if (zappyMovement == null) return;
            
            // Use speed burst to dodge projectiles with fast movement
            if (useSpeedBurstDodge && !zappyMovement.IsInSpeedBurst)
            {
                zappyMovement.ForceSpeedBurst();
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