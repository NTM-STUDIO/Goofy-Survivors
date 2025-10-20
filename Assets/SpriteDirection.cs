using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class RobustIsometricController : MonoBehaviour
{
    [Header("Assign Sprites (Screen Direction)")]
    public Sprite UpRightSprite;
    public Sprite UpLeftSprite;
    public Sprite DownRightSprite;
    public Sprite DownLeftSprite;

    [Header("Optional Child Object Control")]
    [Tooltip("(Optional) Assign a child object like a fire point. Its local X-position will be flipped based on direction.")]
    public Transform firePoint;

    // --- Private References ---
    private SpriteRenderer spriteRenderer;
    private Transform playerTransform;

    // Caching for optimization
    private Sprite currentSprite;
    private float firePointXMagnitude; // We only store the POSITIVE magnitude of X.

    // --- The four isometric directions in world space ---
    private static readonly Vector3 isoDirectionUpRight = new Vector3(1, 0, 1).normalized;
    private static readonly Vector3 isoDirectionUpLeft = new Vector3(-1, 0, 1).normalized;
    private static readonly Vector3 isoDirectionDownRight = new Vector3(1, 0, -1).normalized;
    private static readonly Vector3 isoDirectionDownLeft = new Vector3(-1, 0, -1).normalized;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        // Store the absolute value (magnitude) of the initial X position ONCE.
        if (firePoint != null)
        {
            firePointXMagnitude = Mathf.Abs(firePoint.localPosition.x);
        }

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
        Vector3 directionToPlayer = (playerTransform.position - transform.position).normalized;

        float dotUpRight = Vector3.Dot(directionToPlayer, isoDirectionUpRight);
        float dotUpLeft = Vector3.Dot(directionToPlayer, isoDirectionUpLeft);
        float dotDownRight = Vector3.Dot(directionToPlayer, isoDirectionDownRight);
        float dotDownLeft = Vector3.Dot(directionToPlayer, isoDirectionDownLeft);

        float maxDot = Mathf.Max(dotUpRight, dotUpLeft, dotDownRight, dotDownLeft);

        Sprite newSprite = null;
        
        // *** INVERTED LOGIC ***
        // We now decide the SIGN here. Default to positive for Right-facing sprites.
        float newSign = 1f; 

        if (maxDot == dotUpRight)
        {
            newSprite = UpRightSprite;
            newSign = 1f; // Positive for Right
        }
        else if (maxDot == dotUpLeft)
        {
            newSprite = UpLeftSprite;
            newSign = -1f; // Negative for Left
        }
        else if (maxDot == dotDownRight)
        {
            newSprite = DownRightSprite;
            newSign = 1f; // Positive for Right
        }
        else // maxDot == dotDownLeft
        {
            newSprite = DownLeftSprite;
            newSign = -1f; // Negative for Left
        }

        // Apply the new position if the firePoint exists
        if (firePoint != null)
        {
            // Get the current position, but immediately overwrite X
            Vector3 newFirePointPos = firePoint.localPosition;
            newFirePointPos.x = firePointXMagnitude * newSign;
            firePoint.localPosition = newFirePointPos;
        }

        // Only update the sprite if it has changed.
        if (newSprite != null && newSprite != currentSprite)
        {
            spriteRenderer.sprite = newSprite;
            currentSprite = newSprite;
        }
    }
}