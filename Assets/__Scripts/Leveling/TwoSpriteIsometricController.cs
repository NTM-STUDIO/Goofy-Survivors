using UnityEngine;

public class TwoSpriteIsometricController : Unity.Netcode.NetworkBehaviour
{
    [Header("Base Isometric Sprites")]
    public Sprite frontFacingSprite; // Used for looking "down" or "front"
    public Sprite backFacingSprite;  // Used for looking "up" or "back"

    [Header("Visuals References")]
    public Transform visualsTransform; // Assign the 'Visuals' child GameObject's Transform here
    public SpriteRenderer spriteRenderer; // Assign the SpriteRenderer from the 'Visuals' child here

    [Header("Settings")]
    public bool isPlayer = false; // Check if this is a player for different input handling
    public float rotationSpeed = 10f; // How fast the sprite rotates around the Y-axis
    public float movementThreshold = 0.1f; // How much movement is needed to determine direction

    // This is no longer used by the enemy's new logic but may be useful for other things.
    [Header("Enemy AI Settings")]
    [Tooltip("How far past the center the player must be to trigger a turn. Prevents rapid flipping.")]
    public float enemyQuadrantThreshold = 1f;

    // Private state variables
    private Vector2 lastLookDirection = Vector2.down;
    private Transform playerTransform;
    private float currentTargetYAngle = 0f;

    // Network Variable for syncing look direction in multiplayer
    // Only the Owner (local player) can write to this variable. Everyone can read.
    private Unity.Netcode.NetworkVariable<Vector2> netLookDirection = new Unity.Netcode.NetworkVariable<Vector2>(
        Vector2.down, 
        Unity.Netcode.NetworkVariableReadPermission.Everyone, 
        Unity.Netcode.NetworkVariableWritePermission.Owner);

    void Awake()
    {
        if (visualsTransform == null || spriteRenderer == null)
        {
            Debug.LogError("Visuals references not assigned on " + gameObject.name);
            enabled = false;
            return;
        }

        // Make sure the visuals are not rotated at the start.
        visualsTransform.localRotation = Quaternion.identity;

        if (!isPlayer)
        {
            GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
            if (playerObject != null)
            {
                playerTransform = playerObject.transform;
            }
            else
            {
                // Warn but don't disable yet, player might spawn later
                // Debug.LogError("Player with tag 'Player' not found! The enemy needs this reference.");
            }
        }

        if (frontFacingSprite != null)
        {
            spriteRenderer.sprite = frontFacingSprite;
        }
    }

    public override void OnNetworkSpawn()
    {
        if (isPlayer && !IsOwner)
        {
            // Initialize with current network state
            UpdatePlayerVisuals(netLookDirection.Value);
            
            // Subscribe to changes
            netLookDirection.OnValueChanged += OnLookDirectionChanged;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (isPlayer)
        {
            netLookDirection.OnValueChanged -= OnLookDirectionChanged;
        }
    }

    private void OnLookDirectionChanged(Vector2 previous, Vector2 current)
    {
        UpdatePlayerVisuals(current);
    }

    void FixedUpdate()
    {
        if (isPlayer)
        {
            // --- PLAYER MULTIPLAYER LOGIC ---
            if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening && IsSpawned)
            {
                if (IsOwner)
                {
                    // LOCAL PLAYER: Read Inputs, Apply Visuals, Update Network
                    float moveX = Input.GetAxisRaw("Horizontal");
                    float moveY = Input.GetAxisRaw("Vertical");
                    Vector2 moveInput = new Vector2(moveX, moveY);
                    
                    if (moveInput.sqrMagnitude > 0.01f)
                    {
                        lastLookDirection = moveInput.normalized;
                        // Sync to network only if changed significantly to save bandwidth (optional optimization)
                        if (Vector2.Distance(netLookDirection.Value, lastLookDirection) > 0.05f)
                        {
                            netLookDirection.Value = lastLookDirection;
                        }
                    }
                    UpdatePlayerVisuals(lastLookDirection);
                }
                else
                {
                    // REMOTE PLAYER: Visuals are updated via OnValueChanged or reading NetVar
                    // No input reading here! This fixes the "Blend" issue.
                }
            }
            // --- PLAYER SINGLEPLAYER / OFFLINE LOGIC ---
            else
            {
                float moveX = Input.GetAxisRaw("Horizontal");
                float moveY = Input.GetAxisRaw("Vertical");
                Vector2 moveInput = new Vector2(moveX, moveY);
                if (moveInput.sqrMagnitude > 0.01f) lastLookDirection = moveInput.normalized;
                UpdatePlayerVisuals(lastLookDirection);
            }
        }
        else
        {
            // --- ENEMY MULTIPLAYER GUARD ---
            // In multiplayer, enemy facing should be driven by NetworkEnemyVisuals (server authoritative).
            // This component otherwise runs on every client and will fight the networked flip.
            if (Unity.Netcode.NetworkManager.Singleton != null && Unity.Netcode.NetworkManager.Singleton.IsListening && IsSpawned)
            {
                // If this enemy has NetworkEnemyVisuals, do not run per-client facing logic.
                if (GetComponent<NetworkEnemyVisuals>() != null)
                {
                    return;
                }
                // Otherwise, only let the server run enemy visuals logic.
                if (!IsServer)
                {
                    return;
                }
            }

            // --- ENEMY LOGIC (No Networking needed usually, server driven) ---
            if (playerTransform == null)
            {
                 GameObject playerObject = GameObject.FindGameObjectWithTag("Player");
                 if (playerObject != null) playerTransform = playerObject.transform;
            }

            if (playerTransform != null)
            {
                Vector3 directionToPlayer = playerTransform.position - transform.position;
                UpdateEnemyVisuals(directionToPlayer);
            }
        }
    }

    // --- PLAYER FUNCTION (UNCHANGED) ---
    void UpdatePlayerVisuals(Vector2 direction)
    {
        // 1. Determine Sprite based on vertical movement
        if (direction.y > 0.1f) // Moving Up
        {
            spriteRenderer.sprite = backFacingSprite;
        }
        else if (direction.y < -0.1f) // Moving Down
        {
            spriteRenderer.sprite = frontFacingSprite;
        }

        // 2. Determine Rotation based on horizontal movement
        if (direction.x < -0.1f) // Moving Left
        {
            currentTargetYAngle = 0f;
        }
        else if (direction.x > 0.1f) // Moving Right
        {
            currentTargetYAngle = 180f;
        }

        // 3. Apply the Rotation Smoothly
        Quaternion currentVisualsRotation = visualsTransform.localRotation;
        Quaternion targetVisualsRotation = Quaternion.Euler(currentVisualsRotation.eulerAngles.x, currentTargetYAngle, currentVisualsRotation.eulerAngles.z);
        visualsTransform.localRotation = Quaternion.Slerp(currentVisualsRotation, targetVisualsRotation, rotationSpeed * Time.deltaTime);
    }

    // --- ENEMY FUNCTION (REWRITTEN TO USE SPRITE FLIPPING) ---
    void UpdateEnemyVisuals(Vector2 offset)
    {
        // 1. Determine Vertical Facing (Up/Down Sprite)
        if (offset.y > 0) // Player is above the enemy
        {
            spriteRenderer.sprite = backFacingSprite;
        }
        else // Player is below or level with the enemy
        {
            spriteRenderer.sprite = frontFacingSprite;
        }

        // 2. Determine Horizontal Facing (Flip Sprite on X-axis)
        // If another component (NetworkEnemyVisuals) is responsible for flipX, don't fight it.
        if (GetComponent<NetworkEnemyVisuals>() != null)
        {
            return;
        }

        // This assumes your base sprites are drawn facing left.
        // If your sprites are drawn facing right, change this to offset.x < 0.
        if (offset.x > 0) // Player is to the right of the enemy
        {
            spriteRenderer.flipX = true; // Flip sprite to face right
        }
        else // Player is to the left of the enemy
        {
            spriteRenderer.flipX = false; // Use the default sprite direction (facing left)
        }
    }
}