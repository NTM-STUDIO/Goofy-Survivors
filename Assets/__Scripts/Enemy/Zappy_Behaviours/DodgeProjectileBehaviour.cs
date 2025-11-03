using UnityEngine;

public class DodgeProjectileBehaviour : EnemyBehaviour
{
    [Header("Detection Settings")]
    [Tooltip("Projectile detection radius")]
    public float detectionRadius = 7f;
    
    [Tooltip("Projectile layer mask")]
    public LayerMask projectileLayer;
    
    [Header("Dodge Settings")]
    [Range(0f, 1f)]
    [Tooltip("Chance to dodge when detecting projectile (0-1)")]
    public float dodgeChance = 0.8f;
    
    [Tooltip("Minimum time between dodges")]
    public float dodgeCooldown = 0.5f;
    
    [Header("Dodge Type")]
    [Tooltip("Use speed burst to dodge projectiles")]
    public bool useSpeedBurstDodge = true;
    
    [Header("Debug")]
    [Tooltip("Show detection radius in editor")]
    public bool showDetectionRadius = true;
    
    [Tooltip("Log when detecting projectiles")]
    public bool debugLog = false;
    
    private ZappyMovementBehaviour zappyMovement;
    private float lastDodgeTime;
    
    protected override void Awake()
    {
        base.Awake();
        zappyMovement = GetComponent<ZappyMovementBehaviour>();
        
        if (zappyMovement == null)
        {
            Debug.LogWarning($"DodgeProjectileBehaviour on {gameObject.name} requires ZappyMovementBehaviour!", this);
        }
        
        // Check if projectile layer is configured
        if (projectileLayer.value == 0)
        {
            Debug.LogError($"ProjectileLayer not configured on {gameObject.name}!", this);
        }
    }
    
    public override void Execute()
    {
        if (Time.time - lastDodgeTime < dodgeCooldown) return;
        
        DetectAndDodgeProjectiles();
    }
    
    private void DetectAndDodgeProjectiles()
    {
        // Detect all projectiles within detection radius
        Collider[] projectiles = Physics.OverlapSphere(
            transform.position, 
            detectionRadius, 
            projectileLayer
        );
        
        if (debugLog && projectiles.Length > 0)
        {
            Debug.Log($"Zappy detected {projectiles.Length} projectile(s) at {Vector3.Distance(transform.position, projectiles[0].transform.position)}m");
        }
        
        if (projectiles.Length > 0)
        {
            // Check if any projectile is approaching
            foreach (var proj in projectiles)
            {
                Rigidbody rb = proj.GetComponent<Rigidbody>();
                if (rb != null)
                {
                    // Check if projectile is heading towards us
                    Vector3 toEnemy = (transform.position - proj.transform.position).normalized;
                    float dot = Vector3.Dot(rb.linearVelocity.normalized, toEnemy);
                    
                    // If projectile is heading towards us (dot > 0.5 means roughly in our direction)
                    if (dot > 0.5f && Random.value < dodgeChance)
                    {
                        if (zappyMovement != null && useSpeedBurstDodge && !zappyMovement.IsInSpeedBurst)
                        {
                            if (debugLog)
                            {
                                Debug.Log($"Zappy dodging projectile!");
                            }
                            zappyMovement.ForceSpeedBurst();
                            lastDodgeTime = Time.time;
                            return;
                        }
                    }
                }
            }
        }
    }
    
    private void OnDrawGizmosSelected()
    {
        if (!showDetectionRadius) return;
        
        // Draw detection radius
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        
        // Draw lines to detected projectiles
        Collider[] projectiles = Physics.OverlapSphere(transform.position, detectionRadius, projectileLayer);
        Gizmos.color = Color.red;
        foreach (var proj in projectiles)
        {
            Gizmos.DrawLine(transform.position, proj.transform.position);
        }
    }
}