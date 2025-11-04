using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class PlayerSpriteFlipperIsometric : MonoBehaviour
{
    [Header("Assign Sprites")]
    [Tooltip("The sprite to use when the player is moving LEFT on the screen.")]
    public Sprite LeftSprite;

    [Tooltip("The sprite to use when the player is moving RIGHT on the screen.")]
    public Sprite RightSprite;

    // --- Private References ---
    private SpriteRenderer spriteRenderer;
    private Rigidbody rb;
    private Transform cameraTransform;

    // A small threshold to prevent the sprite from flipping on tiny movements
    private const float velocityThreshold = 0.1f;

    void Awake()
    {
        // Get the components we need
        spriteRenderer = GetComponent<SpriteRenderer>();
        rb = GetComponentInParent<Rigidbody>();
        
        // Find the main camera in the scene
        if (Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }
        else
        {
            Debug.LogError("PlayerSpriteFlipper: Main camera not found in the scene. Please ensure you have a camera tagged as 'MainCamera'.");
        }
    }

    void LateUpdate()
    {
        if (cameraTransform == null)
        {
            return;
        }

        // 1. Get the character's velocity in world space.
    Vector3 worldVelocity = rb.linearVelocity;

        // 2. We only care about movement in the horizontal plane (X and Z).
        Vector3 horizontalVelocity = new Vector3(worldVelocity.x, 0, worldVelocity.z);

        // 3. If the character is not moving significantly, do nothing.
        if (horizontalVelocity.magnitude < velocityThreshold)
        {
            return;
        }

        // 4. Get the camera's right vector, ignoring any vertical tilt.
        Vector3 cameraRight = new Vector3(cameraTransform.right.x, 0, cameraTransform.right.z).normalized;

        // 5. Calculate the dot product between the player's horizontal velocity and the camera's right vector.
        // A positive dot product means the player is moving more towards the camera's right.
        // A negative dot product means the player is moving more towards the camera's left.
        float dotProduct = Vector3.Dot(horizontalVelocity, cameraRight);

        // 6. Check the direction of movement relative to the camera.
        if (dotProduct > 0)
        {
            // Moving right relative to the camera.
            if (spriteRenderer.sprite != RightSprite)
            {
                spriteRenderer.sprite = RightSprite;
            }
        }
        else
        {
            // Moving left relative to the camera.
            if (spriteRenderer.sprite != LeftSprite)
            {
                spriteRenderer.sprite = LeftSprite;
            }
        }
    }
}