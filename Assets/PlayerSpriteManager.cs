using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class PlayerSpriteFlipper : MonoBehaviour
{
    [Header("Assign Sprites")]
    [Tooltip("The sprite to use when the player is moving LEFT on the screen.")]
    public Sprite LeftSprite;

    [Tooltip("The sprite to use when the player is moving RIGHT on the screen.")]
    public Sprite RightSprite;

    // --- Private References ---
    private SpriteRenderer spriteRenderer;
    private Rigidbody rb;

    // A small threshold to prevent the sprite from flipping on tiny movements
    private const float velocityThreshold = 0.1f;

    void Awake()
    {
        // Get the components we need
        spriteRenderer = GetComponent<SpriteRenderer>();
        rb = GetComponentInParent<Rigidbody>();
    }

    void LateUpdate()
    {
        // 1. Get the horizontal velocity from the Rigidbody.
        // We only care about the X-axis for left/right movement.
        float horizontalVelocity = rb.linearVelocity.x;

        // 2. Check if the player is moving to the right.
        if (horizontalVelocity > velocityThreshold)
        {
            // If we're not already showing the RightSprite, update it.
            if (spriteRenderer.sprite != RightSprite)
            {
                spriteRenderer.sprite = RightSprite;
            }
        }
        // 3. Check if the player is moving to the left.
        else if (horizontalVelocity < -velocityThreshold)
        {
            // If we're not already showing the LeftSprite, update it.
            if (spriteRenderer.sprite != LeftSprite)
            {
                spriteRenderer.sprite = LeftSprite;
            }
        }
        // 4. If the player is not moving horizontally, we do nothing.
        // The sprite will remain facing its last direction, which is the desired behavior.
    }
}