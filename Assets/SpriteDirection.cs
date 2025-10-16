using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class RobustIsometricController : MonoBehaviour
{
    [Header("Assign Sprites (Screen Direction)")]
    [Tooltip("Sprite for when the target is generally UP and RIGHT on the screen.")]
    public Sprite UpRightSprite;

    [Tooltip("Sprite for when the target is generally UP and LEFT on the screen.")]
    public Sprite UpLeftSprite;

    [Tooltip("Sprite for when the target is generally DOWN and RIGHT on the screen.")]
    public Sprite DownRightSprite;

    [Tooltip("Sprite for when the target is generally DOWN and LEFT on the screen.")]
    public Sprite DownLeftSprite;

    // --- Private References ---
    private SpriteRenderer spriteRenderer;
    private Transform playerTransform;

    // Caching for optimization
    private Sprite currentSprite;

    // --- The four isometric directions in world space ---
    // These vectors represent the four diagonal axes of our isometric grid.
    private static readonly Vector3 isoDirectionUpRight = new Vector3(1, 0, 1).normalized;
    private static readonly Vector3 isoDirectionUpLeft = new Vector3(-1, 0, 1).normalized;
    private static readonly Vector3 isoDirectionDownRight = new Vector3(1, 0, -1).normalized;
    private static readonly Vector3 isoDirectionDownLeft = new Vector3(-1, 0, -1).normalized;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }
        else
        {
            Debug.LogError("RobustIsometricController: Player not found! Make sure your player is tagged 'Player'.", this);
            this.enabled = false;
        }
    }

    void LateUpdate()
    {
        // 1. Calculate the normalized direction from the enemy to the player.
        Vector3 directionToPlayer = (playerTransform.position - transform.position).normalized;

        // 2. Use the Dot Product to see which isometric axis the direction is closest to.
        // The dot product gives a value from -1 to 1 indicating how similar two directions are.
        // The highest positive value wins.
        float dotUpRight = Vector3.Dot(directionToPlayer, isoDirectionUpRight);
        float dotUpLeft = Vector3.Dot(directionToPlayer, isoDirectionUpLeft);
        float dotDownRight = Vector3.Dot(directionToPlayer, isoDirectionDownRight);
        float dotDownLeft = Vector3.Dot(directionToPlayer, isoDirectionDownLeft);

        // Find the maximum dot product
        float maxDot = Mathf.Max(dotUpRight, dotUpLeft, dotDownRight, dotDownLeft);

        Sprite newSprite = null;

        // 3. Select the sprite that corresponds to the direction with the highest similarity.
        if (maxDot == dotUpRight)
        {
            newSprite = UpRightSprite;
        }
        else if (maxDot == dotUpLeft)
        {
            newSprite = UpLeftSprite;
        }
        else if (maxDot == dotDownRight)
        {
            newSprite = DownRightSprite;
        }
        else // maxDot == dotDownLeft
        {
            newSprite = DownLeftSprite;
        }

        // 4. Only update the sprite if it has changed.
        if (newSprite != null && newSprite != currentSprite)
        {
            spriteRenderer.sprite = newSprite;
            currentSprite = newSprite;
        }
    }
}