using UnityEngine;

public class TwoSpriteIsometricController : MonoBehaviour
{
    [Header("Base Isometric Sprites")]
    public Sprite frontFacingSprite; // Used for moving/facing generally "down" or "front"
    public Sprite backFacingSprite;  // Used for moving/facing generally "up" or "back"

    [Header("Visuals References")]
    public Transform visualsTransform; // Assign the 'Visuals' child GameObject's Transform here
    public SpriteRenderer spriteRenderer; // Assign the SpriteRenderer on the 'Visuals' child here

    [Header("Settings")]
    public bool isPlayer = false; // Check if this is a player for different input handling
    public float rotationSpeed = 10f; // How fast the sprite rotates around the Y-axis
    public float movementThreshold = 0.1f; // How much movement is needed to determine direction

    private Vector2 currentMovementInput = Vector2.zero; // Stores the raw movement input
    private Vector2 lastLookDirection = Vector2.down;    // Stores the last significant direction for sprite choice and rotation

    void Awake()
    {
        // Add checks to ensure references are set
        if (visualsTransform == null)
        {
            Debug.LogError("Visuals Transform not assigned on " + gameObject.name + ". Please assign the 'Visuals' child.");
            enabled = false;
            return;
        }
        if (spriteRenderer == null)
        {
            Debug.LogError("SpriteRenderer not assigned on " + gameObject.name + ". Please assign the SpriteRenderer from the 'Visuals' child.");
            enabled = false;
            return;
        }

        // Set initial sprite
        if (frontFacingSprite != null)
        {
            spriteRenderer.sprite = frontFacingSprite;
        }
        // Ensure initial Z-rotation is 0, but Y-rotation will be controlled on the visualsTransform
        visualsTransform.localEulerAngles = new Vector3(visualsTransform.localEulerAngles.x, 0, visualsTransform.localEulerAngles.z);
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
            // Update lastLookDirection when moving for both sprite choice and idle rotation
            lastLookDirection = activeDirection;
        }
        else if (lastLookDirection != Vector2.zero) // If idle, use the last direction
        {
            activeDirection = lastLookDirection;
        }
        else // Fallback if no movement and no last direction
        {
            activeDirection = Vector2.down; // Default to facing front
            lastLookDirection = Vector2.down; // Also set last direction
        }

        // --- Determine Sprite (Front or Back) ---
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

        // --- Determine Y-Axis Rotation (Left or Right) ---
        // Your base sprites should be drawn facing one direction (e.g., Left or Right).
        // Let's assume your sprites are drawn facing RIGHT, so 0 Y-rotation means "right".
        // Then 180 Y-rotation means "left".

        float targetYAngle = visualsTransform.localEulerAngles.y; // Keep current Y-angle if no strong horizontal
        
        if (activeDirection.x < -0.1f) // Moving/Facing Left
        {
            targetYAngle = 0f; // Rotate 180 degrees around Y to face left
        }
        else if (activeDirection.x > 0.1f) // Moving/Facing Right
        {
            targetYAngle = 180f; // Reset Y-rotation to 0 to face right
        }

        // Smoothly interpolate the Y-rotation of the visualsTransform
        Quaternion currentVisualsRotation = visualsTransform.localRotation;
        Quaternion targetVisualsRotation = Quaternion.Euler(currentVisualsRotation.eulerAngles.x, targetYAngle, currentVisualsRotation.eulerAngles.z);
        visualsTransform.localRotation = Quaternion.Slerp(currentVisualsRotation, targetVisualsRotation, rotationSpeed * Time.deltaTime);
    }

    // Public method for AI to set movement direction for enemies
    public void SetEnemyMovementInput(Vector2 moveInput)
    {
        currentMovementInput = moveInput;
        if (moveInput.magnitude > movementThreshold)
        {
            lastLookDirection = moveInput.normalized;
        }
    }
}