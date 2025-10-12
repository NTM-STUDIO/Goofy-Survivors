using UnityEngine;

public class TwoSpriteIsometricController : MonoBehaviour
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
                Debug.LogError("Player with tag 'Player' not found! The enemy needs this reference.");
                enabled = false;
                return;
            }
        }

        if (frontFacingSprite != null)
        {
            spriteRenderer.sprite = frontFacingSprite;
        }
    }

    void FixedUpdate()
    {
        if (isPlayer)
        {
            // --- PLAYER LOGIC (UNCHANGED) ---
            float moveX = Input.GetAxisRaw("Horizontal");
            float moveY = Input.GetAxisRaw("Vertical");
            Vector2 moveInput = new Vector2(moveX, moveY);
            lastLookDirection = moveInput.normalized;
            UpdatePlayerVisuals(lastLookDirection);
        }
        else
        {
            // --- ENEMY LOGIC ---
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