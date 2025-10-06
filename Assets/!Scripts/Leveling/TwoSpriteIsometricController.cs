using UnityEngine;

public class TwoSpriteIsometricController : MonoBehaviour
{
    [Header("Base Isometric Sprites")]
    public Sprite frontFacingSprite; // Used for moving/facing generally "down" or "front"
    public Sprite backFacingSprite;  // Used for moving/facing generally "up" or "back"

    [Header("Settings")]
    public bool isPlayer = false; // Check if this is a player for different input handling
    public float rotationSpeed = 10f; // How fast the sprite rotates when switching sides
    public float movementThreshold = 0.1f; // How much movement is needed to determine direction

    private SpriteRenderer spriteRenderer;
    private Vector2 currentMovementInput = Vector2.zero; // Stores the raw movement input
    private Vector2 lastLookDirection = Vector2.down;    // Stores the last significant direction for rotation

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            Debug.LogError("SpriteRenderer not found on " + gameObject.name + ". Please add one.");
            enabled = false;
            return;
        }

        // Set initial sprite
        if (frontFacingSprite != null)
        {
            spriteRenderer.sprite = frontFacingSprite;
        }
    }

    void Update()
    {
        if (isPlayer)
        {
            HandlePlayerInput();
        }
        else
        {
            // For enemies, AI should set currentMovementInput
            // Example: SetEnemyMovementInput((target.position - transform.position).normalized);
        }

        UpdateIsometricVisuals();
    }

    void HandlePlayerInput()
    {
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveY = Input.GetAxisRaw("Vertical");
        currentMovementInput = new Vector2(moveX, moveY);
    }

    void UpdateIsometricVisuals()
    {
        Vector2 activeDirection = Vector2.zero;

        // Determine if moving, and get the active direction
        if (currentMovementInput.magnitude > movementThreshold)
        {
            activeDirection = currentMovementInput.normalized;
        }
        else if (lastLookDirection != Vector2.zero) // If idle, use the last direction
        {
            activeDirection = lastLookDirection;
        }
        else // Fallback if no movement and no last direction
        {
            activeDirection = Vector2.down; // Default to facing front
        }

        // --- Determine Sprite (Front or Back) ---
        // If the primary vertical component is positive (moving up/back), use back sprite.
        // Otherwise (moving down/front or primarily horizontal), use front sprite.
        if (activeDirection.y > 0) // Facing generally up/back
        {
            if (spriteRenderer.sprite != backFacingSprite)
            {
                spriteRenderer.sprite = backFacingSprite;
            }
        }
        else // Facing generally down/front, or primarily horizontal
        {
            if (spriteRenderer.sprite != frontFacingSprite)
            {
                spriteRenderer.sprite = frontFacingSprite;
            }
        }

        // --- Determine Rotation (Left or Right) ---
        // Only update lastLookDirection if there's significant input or we're currently moving
        if (currentMovementInput.magnitude > movementThreshold)
        {
            lastLookDirection = currentMovementInput.normalized;
        }
        // If the X component is significant, rotate. Otherwise, keep upright.
        // We're essentially rotating the chosen front/back sprite
        float targetAngle = 0;
        if (activeDirection.x < -0.1f) // Facing Left (small threshold for precision)
        {
            targetAngle = 0; // Or -90, depending on your sprite's default orientation and desired visual
        }
        else if (activeDirection.x > 0.1f) // Facing Right
        {
            targetAngle = 180; // Or 90
        }
        // else targetAngle remains 0 (facing forward/backward, no side rotation)

        Quaternion targetRotation = Quaternion.Euler(0, 0, targetAngle);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);

        // Keep lastLookDirection updated even if no strong horizontal movement,
        // so when stopping, it still knows which way it was primarily facing for the sprite choice.
        if (currentMovementInput.magnitude > movementThreshold)
        {
            lastLookDirection = currentMovementInput.normalized;
        }
        else if (Mathf.Approximately(currentMovementInput.magnitude, 0f) && lastLookDirection == Vector2.zero)
        {
            // If completely stopped and no prior direction, default to down
            lastLookDirection = Vector2.down;
        }
    }

    // Public method for AI to set movement direction for enemies
    public void SetEnemyMovementInput(Vector2 moveInput)
    {
        currentMovementInput = moveInput;
        // Also update lastLookDirection if there's significant movement
        if (moveInput.magnitude > movementThreshold)
        {
            lastLookDirection = moveInput.normalized;
        }
    }
}