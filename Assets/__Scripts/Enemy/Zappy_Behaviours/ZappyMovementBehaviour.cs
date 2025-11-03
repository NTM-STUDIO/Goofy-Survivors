using UnityEngine;
using System.Collections;

public class ZappyMovementBehaviour : EnemyBehaviour
{
    [Header("Target Settings")]
    [SerializeField] private string playerTag = "Player";
    [SerializeField] private float aggroRange = 30f;
    
    [Header("Attack & Retreat")]
    [Tooltip("Distance to get close before attacking")]
    [SerializeField] private float attackRange = 2f;
    
    [Tooltip("Time between attacks (cooldown)")]
    [SerializeField] private float attackCooldown = 3f;
    
    [Tooltip("Minimum retreat distance from player")]
    [SerializeField] private float retreatMinDistance = 10f;
    
    [Tooltip("Maximum retreat distance from player")]
    [SerializeField] private float retreatMaxDistance = 15f;
    
    [Tooltip("How long to wait at retreat position before attacking again")]
    [SerializeField] private float retreatWaitTime = 2f;
    
    [Tooltip("Speed multiplier during retreat (how fast to run away)")]
    [SerializeField] private float retreatSpeedMultiplier = 3f;
    
    [Header("Lightning Movement - Stop/Start Pattern")]
    [Tooltip("Base speed multiplier for fast movement phases")]
    [SerializeField] private float speedMultiplier = 1.5f;
    
    [Tooltip("Duration of movement bursts (seconds)")]
    [SerializeField] private float moveDuration = 0.3f;
    
    [Tooltip("Duration of stop/pause (seconds)")]
    [SerializeField] private float stopDuration = 0.2f;
    
    [Tooltip("Add randomness to movement timing")]
    [SerializeField] private float timeRandomness = 0.1f;
    
    [Header("Erratic Movement")]
    [Tooltip("How much the enemy zigzags (0 = straight, 1 = very erratic)")]
    [Range(0f, 1f)]
    [SerializeField] private float erraticness = 0.4f;
    
    [Tooltip("How often to change direction during movement (seconds)")]
    [SerializeField] private float directionChangeInterval = 0.15f;
    
    [Tooltip("Maximum angle deviation from direct path (degrees)")]
    [SerializeField] private float maxAngleDeviation = 45f;
    
    [Header("Speed Burst")]
    [Tooltip("Chance to enter speed burst mode after stop (0-1)")]
    [Range(0f, 1f)]
    [SerializeField] private float speedBurstChance = 0.3f;
    
    [Tooltip("Speed multiplier during burst")]
    [SerializeField] private float burstSpeedMultiplier = 2.5f;
    
    [Tooltip("Duration of speed burst (seconds)")]
    [SerializeField] private float burstDuration = 0.5f;
    
    [Header("Visual Effects")]
    [Tooltip("Enable afterimage trail during fast movement")]
    [SerializeField] private bool useAfterimages = true;
    
    [Tooltip("Color tint during speed burst")]
    [SerializeField] private Color burstColor = new Color(1f, 1f, 0.3f, 1f);
    
    [Header("Debug")]
    [SerializeField] private bool showDebugGizmos = true;
    
    // Internal state
    private enum MovementState { Moving, Stopping, SpeedBurst, Attacking, Retreating, RetreatWaiting }
    private MovementState currentState = MovementState.Stopping;
    
    private Transform targetPlayer;
    private Rigidbody rb;
    private AfterimageEffect afterimageEffect;
    private SpriteRenderer spriteRenderer;
    
    private float stateTimer;
    private float nextDirectionChangeTime;
    private Vector3 currentMoveDirection;
    private Vector3 erraticOffset;
    private bool isInitialized;
    
    private Color originalColor;
    private bool inSpeedBurst;
    
    // Attack & Retreat
    private float lastAttackTime = -999f;
    private Vector3 retreatPosition;
    private bool hasRetreatPosition;
    
    protected override void Awake()
    {
        base.Awake();
        
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = false;
            rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
        }
        else
        {
            Debug.LogError($"[{name}] ZappyMovementBehaviour requires Rigidbody!", this);
            enabled = false;
            return;
        }
        
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }
        if (spriteRenderer != null)
        {
            originalColor = spriteRenderer.color;
        }
        
        if (useAfterimages)
        {
            afterimageEffect = GetComponent<AfterimageEffect>();
            if (afterimageEffect == null)
            {
                afterimageEffect = gameObject.AddComponent<AfterimageEffect>();
            }
        }
        
        StartCoroutine(WaitForGameStart());
    }
    
    private IEnumerator WaitForGameStart()
    {
        while (GameManager.Instance == null || GameManager.Instance.CurrentState != GameManager.GameState.Playing)
        {
            yield return new WaitForSeconds(0.5f);
        }
        
        isInitialized = true;
        StartMovementCycle();
    }
    
    private void LateUpdate()
    {
        // Auto-manage afterimages based on velocity
        if (useAfterimages && afterimageEffect != null && isInitialized)
        {
            // Enable afterimages when moving, disable when stationary
            if (rb.linearVelocity.sqrMagnitude > 0.1f)
            {
                if (!afterimageEffect.enabled || afterimageEffect == null)
                {
                    afterimageEffect.StartEffect();
                }
            }
            else
            {
                // Only stop afterimages during long waits, not brief stops
                if (currentState == MovementState.RetreatWaiting)
                {
                    afterimageEffect.StopEffect();
                }
            }
        }
    }
    
    public override void Execute()
    {
        if (!isInitialized || !enabled)
        {
            rb.linearVelocity = Vector3.zero;
            return;
        }
        
        // Find or update player reference
        if (targetPlayer == null)
        {
            FindClosestPlayer();
            if (targetPlayer == null)
            {
                rb.linearVelocity = Vector3.zero;
                return;
            }
        }
        
        // Check if player is in aggro range
        float distanceToPlayer = Vector3.Distance(transform.position, targetPlayer.position);
        if (distanceToPlayer > aggroRange)
        {
            rb.linearVelocity = Vector3.zero;
            return;
        }
        
        // Priority 1: Check if we should attack
        if (currentState != MovementState.Attacking && 
            currentState != MovementState.Retreating && 
            currentState != MovementState.RetreatWaiting &&
            distanceToPlayer <= attackRange && 
            Time.time >= lastAttackTime + attackCooldown)
        {
            StartCoroutine(PerformAttackAndRetreat());
            return;
        }
        
        // Update movement based on state
        switch (currentState)
        {
            case MovementState.Moving:
                UpdateMovingState();
                break;
            case MovementState.Stopping:
                UpdateStoppingState();
                break;
            case MovementState.SpeedBurst:
                UpdateSpeedBurstState();
                break;
            case MovementState.Attacking:
                // Handled by coroutine
                rb.linearVelocity = Vector3.zero;
                break;
            case MovementState.Retreating:
                UpdateRetreatingState();
                break;
            case MovementState.RetreatWaiting:
                UpdateRetreatWaitingState();
                break;
        }
    }
    
    private void FindClosestPlayer()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag(playerTag);
        if (players.Length == 0) return;
        
        float closestDistance = float.MaxValue;
        Transform closest = null;
        
        foreach (GameObject player in players)
        {
            float distance = Vector3.Distance(transform.position, player.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = player.transform;
            }
        }
        
        targetPlayer = closest;
    }
    
    private void StartMovementCycle()
    {
        // Randomly start with either moving or stopping
        if (Random.value > 0.5f)
        {
            StartMoving();
        }
        else
        {
            StartStopping();
        }
    }
    
    private void StartMoving()
    {
        currentState = MovementState.Moving;
        stateTimer = moveDuration + Random.Range(-timeRandomness, timeRandomness);
        nextDirectionChangeTime = Time.time;
        
        if (useAfterimages && afterimageEffect != null)
        {
            afterimageEffect.StartEffect();
        }
        
        UpdateMoveDirection();
    }
    
    private void StartStopping()
    {
        currentState = MovementState.Stopping;
        stateTimer = stopDuration + Random.Range(-timeRandomness, timeRandomness);
        rb.linearVelocity = Vector3.zero;
        
        // Keep afterimages on even during brief stops for continuous trail effect
        // They will be managed by velocity in LateUpdate
    }
    
    private void StartSpeedBurst()
    {
        currentState = MovementState.SpeedBurst;
        stateTimer = burstDuration;
        inSpeedBurst = true;
        
        if (useAfterimages && afterimageEffect != null)
        {
            afterimageEffect.StartEffect();
        }
        
        if (spriteRenderer != null)
        {
            spriteRenderer.color = burstColor;
        }
        
        UpdateMoveDirection();
    }
    
    private void UpdateMovingState()
    {
        stateTimer -= Time.deltaTime;
        
        // Change direction periodically for erratic movement
        if (Time.time >= nextDirectionChangeTime)
        {
            UpdateMoveDirection();
            nextDirectionChangeTime = Time.time + directionChangeInterval;
        }
        
        // Apply movement
        if (stats != null)
        {
            Vector3 finalDirection = (currentMoveDirection + erraticOffset).normalized;
            finalDirection.y = 0;
            rb.linearVelocity = finalDirection * (stats.moveSpeed * speedMultiplier);
        }
        
        // Check if movement phase is over
        if (stateTimer <= 0)
        {
            // Chance for speed burst or normal stop
            if (Random.value < speedBurstChance)
            {
                StartSpeedBurst();
            }
            else
            {
                StartStopping();
            }
        }
    }
    
    private void UpdateStoppingState()
    {
        stateTimer -= Time.deltaTime;
        rb.linearVelocity = Vector3.zero;
        
        if (stateTimer <= 0)
        {
            StartMoving();
        }
    }
    
    private void UpdateSpeedBurstState()
    {
        stateTimer -= Time.deltaTime;
        
        // Move straight towards player during burst
        if (targetPlayer != null && stats != null)
        {
            Vector3 directionToPlayer = (targetPlayer.position - transform.position).normalized;
            directionToPlayer.y = 0;
            rb.linearVelocity = directionToPlayer * (stats.moveSpeed * burstSpeedMultiplier);
        }
        
        // End speed burst
        if (stateTimer <= 0)
        {
            inSpeedBurst = false;
            
            if (spriteRenderer != null)
            {
                spriteRenderer.color = originalColor;
            }
            
            if (useAfterimages && afterimageEffect != null)
            {
                afterimageEffect.StopEffect();
            }
            
            StartStopping();
        }
    }
    
    private void UpdateMoveDirection()
    {
        if (targetPlayer == null) return;
        
        // Base direction towards player
        Vector3 toPlayer = (targetPlayer.position - transform.position).normalized;
        toPlayer.y = 0;
        currentMoveDirection = toPlayer;
        
        // Add erratic offset for lightning-like movement
        if (erraticness > 0)
        {
            // Generate perpendicular vector for zigzag
            Vector3 perpendicular = new Vector3(-toPlayer.z, 0, toPlayer.x);
            
            // Random angle deviation
            float randomAngle = Random.Range(-maxAngleDeviation, maxAngleDeviation) * erraticness;
            
            // Combine with perpendicular movement
            erraticOffset = perpendicular * Mathf.Sin(randomAngle * Mathf.Deg2Rad) * erraticness;
        }
        else
        {
            erraticOffset = Vector3.zero;
        }
    }
    
    /// <summary>
    /// Executes the attack preparation.
    /// Zappy stops and enters attack state. Actual retreat is triggered when damage is dealt in OnTriggerStay.
    /// </summary>
    private IEnumerator PerformAttackAndRetreat()
    {
        // Enter attacking state and stop movement
        currentState = MovementState.Attacking;
        rb.linearVelocity = Vector3.zero;
        
        // Disable afterimages during attack preparation
        if (useAfterimages && afterimageEffect != null)
        {
            afterimageEffect.StopEffect();
        }
        
        // Brief pause before striking - creates tension
        yield return new WaitForSeconds(0.2f);
        
        // Ready to attack - damage and retreat are handled by OnTriggerStay when contact is made
        Debug.Log($"[Zappy] Ready to attack at {Time.time}");
        
        // Wait for OnTriggerStay to handle damage and retreat, or timeout after 2 seconds
        yield return new WaitForSeconds(2f);
        
        // If still in attacking state after timeout (no contact made), resume movement
        if (currentState == MovementState.Attacking)
        {
            Debug.Log($"[Zappy] Attack timeout - resuming movement");
            StartMoving();
        }
    }
    
    /// <summary>
    /// Updates the retreating state, moving Zappy away from the player at high speed.
    /// Moves directly to retreat position without recalculating to avoid oscillation.
    /// </summary>
    private void UpdateRetreatingState()
    {
        // Validate retreat conditions
        if (!hasRetreatPosition || targetPlayer == null)
        {
            currentState = MovementState.Stopping;
            StartStopping();
            return;
        }
        
        // Check if retreat destination has been reached FIRST (before applying movement)
        float distanceToRetreat = Vector3.Distance(transform.position, retreatPosition);
        if (distanceToRetreat < 3f) // Larger threshold to avoid oscillation at destination
        {
            // Stop immediately
            rb.linearVelocity = Vector3.zero;
            
            // Transition to waiting state
            hasRetreatPosition = false;
            currentState = MovementState.RetreatWaiting;
            stateTimer = retreatWaitTime;
            
            // Disable afterimages once retreat is complete
            if (useAfterimages && afterimageEffect != null)
            {
                afterimageEffect.StopEffect();
            }
            
            Debug.Log($"[Zappy] Reached retreat position - waiting for {retreatWaitTime}s");
            return;
        }
        
        // Calculate movement direction towards retreat position
        Vector3 directionToRetreat = (retreatPosition - transform.position).normalized;
        directionToRetreat.y = 0; // Maintain horizontal movement only
        
        // Apply high-speed retreat movement
        if (stats != null)
        {
            rb.linearVelocity = directionToRetreat * (stats.moveSpeed * retreatSpeedMultiplier);
        }
    }
    
    /// <summary>
    /// Updates the retreat waiting state where Zappy remains stationary at the retreat position.
    /// After the wait timer expires, resumes normal movement behavior.
    /// </summary>
    private void UpdateRetreatWaitingState()
    {
        // Force stop movement during wait
        rb.linearVelocity = Vector3.zero;
        
        // Countdown wait timer
        stateTimer -= Time.deltaTime;
        
        // Wait period complete - resume normal movement cycle
        if (stateTimer <= 0)
        {
            Debug.Log($"[Zappy] Retreat wait complete - resuming normal movement");
            StartMoving();
        }
    }
    
    /// <summary>
    /// Calculates a retreat position away from the player.
    /// Position is randomized within min/max distance range and includes lateral offset for unpredictability.
    /// Ensures the retreat point remains visible within the camera's view range.
    /// </summary>
    private void CalculateRetreatPosition()
    {
        // Validate player reference
        if (targetPlayer == null)
        {
            retreatPosition = transform.position;
            return;
        }
        
        // Calculate direction vector away from player
        Vector3 awayFromPlayer = (transform.position - targetPlayer.position).normalized;
        awayFromPlayer.y = 0; // Maintain horizontal movement only
        
        // Randomize retreat distance within configured range
        float retreatDistance = Random.Range(retreatMinDistance, retreatMaxDistance);
        retreatPosition = targetPlayer.position + awayFromPlayer * retreatDistance;
        
        // Add lateral randomness to prevent predictable retreat patterns
        Vector3 randomOffset = new Vector3(
            Random.Range(-3f, 3f),
            0,
            Random.Range(-3f, 3f)
        );
        retreatPosition += randomOffset;
        
        // Preserve vertical position (Y-axis) to maintain ground level
        retreatPosition.y = transform.position.y;
    }
    
    /// <summary>
    /// Force an immediate speed burst (for dodge behavior)
    /// </summary>
    public void ForceSpeedBurst()
    {
        if (currentState != MovementState.SpeedBurst)
        {
            StartSpeedBurst();
        }
    }
    
    public bool IsInSpeedBurst => inSpeedBurst;
    public bool IsInAttackSequence => currentState == MovementState.Attacking || 
                                       currentState == MovementState.Retreating || 
                                       currentState == MovementState.RetreatWaiting;
    
    /// <summary>
    /// Handles collision with player to deal damage and immediately trigger retreat.
    /// </summary>
    private void OnTriggerStay(Collider other)
    {
        // Only damage during the attack state, not when just moving around
        if (currentState != MovementState.Attacking) return;
        
        if (other.CompareTag(playerTag))
        {
            PlayerStats playerStats = other.GetComponentInParent<PlayerStats>();
            if (playerStats != null && stats != null)
            {
                // Deal damage to player
                playerStats.ApplyDamage(stats.GetAttackDamage());
                Debug.Log($"[Zappy] Dealt {stats.GetAttackDamage()} damage to player - Retreating!");
                
                // Immediately start retreat after dealing damage
                StopAllCoroutines(); // Stop the attack coroutine
                StartCoroutine(InitiateRetreat());
            }
        }
    }
    
    /// <summary>
    /// Initiates immediate retreat after successful hit on player.
    /// Called directly from OnTriggerStay when damage is dealt.
    /// </summary>
    private IEnumerator InitiateRetreat()
    {
        // Update last attack time
        lastAttackTime = Time.time;
        
        // Calculate new retreat position based on current player location
        CalculateRetreatPosition();
        
        // Enter retreat state with high-speed movement
        currentState = MovementState.Retreating;
        hasRetreatPosition = true;
        
        // Enable afterimages for visual trail during fast retreat
        if (useAfterimages && afterimageEffect != null)
        {
            afterimageEffect.StartEffect();
        }
        
        yield return null;
    }
    
    /// <summary>
    /// Draws debug gizmos in the Scene view to visualize behavior parameters and state.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos) return;
        
        if (Application.isPlaying)
        {
            // Aggro range - Yellow sphere indicating detection radius
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, aggroRange);
            
            // Attack range - Red sphere showing melee attack distance
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
            
            // Retreat distance ranges - Orange spheres around player
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.3f); // Semi-transparent orange
            if (targetPlayer != null)
            {
                Gizmos.DrawWireSphere(targetPlayer.position, retreatMinDistance);
                Gizmos.DrawWireSphere(targetPlayer.position, retreatMaxDistance);
            }
            
            // Retreat position and path - Blue sphere and cyan line
            if (hasRetreatPosition || currentState == MovementState.RetreatWaiting)
            {
                // Target retreat position
                Gizmos.color = Color.blue;
                Gizmos.DrawWireSphere(retreatPosition, 1f);
                
                // Current position indicator
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(transform.position, 0.5f);
                
                // Path line from current position to retreat target
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position, retreatPosition);
                
                #if UNITY_EDITOR
                // Distance label in Scene view
                float distance = Vector3.Distance(transform.position, retreatPosition);
                UnityEditor.Handles.Label(
                    (transform.position + retreatPosition) / 2f,
                    $"Retreat: {distance:F1}m"
                );
                #endif
            }
            
            // Movement direction visualization
            if (currentState == MovementState.Moving)
            {
                // Base direction towards player
                Gizmos.color = Color.red;
                Gizmos.DrawRay(transform.position, currentMoveDirection * 3f);
                
                // Erratic movement offset
                Gizmos.color = Color.magenta;
                Gizmos.DrawRay(transform.position, (currentMoveDirection + erraticOffset).normalized * 3f);
            }
            
            // Line to player - Green connection line
            if (targetPlayer != null)
            {
                Gizmos.color = Color.green;
                Gizmos.DrawLine(transform.position, targetPlayer.position);
            }
        }
        else
        {
            // Editor-only gizmos when not playing
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, aggroRange);
            
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
        }
    }
}
