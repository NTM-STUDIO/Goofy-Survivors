using UnityEngine;
using Unity.Netcode;

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

    [Header("Direction Vectors (Tweak in Inspector!)")]
    [Tooltip("The world direction for the UP-RIGHT sprite (RED LINE)")]
    public Vector3 directionVector_Red = new Vector3(1, 0, 0);
    [Tooltip("The world direction for the UP-LEFT sprite (BLUE LINE)")]
    public Vector3 directionVector_Blue = new Vector3(0, 0, 1);
    [Tooltip("The world direction for the DOWN-RIGHT sprite (GREEN LINE)")]
    public Vector3 directionVector_Green = new Vector3(-1, 0, 0);
    [Tooltip("The world direction for the DOWN-LEFT sprite (YELLOW LINE)")]
    public Vector3 directionVector_Yellow = new Vector3(0, 0, -1);

    [Header("Debugging")]
    [Tooltip("Show colored lines in the Scene view to visualize the direction vectors.")]
    public bool showGizmos = true;

    [Header("Manual/Inspector Overrides")]
    [Tooltip("Mirror image by swapping Left/Right sprites (does not use SpriteRenderer.flipX).")]
    public bool manualFlipImage = false;
    [Tooltip("When enabled, the controller will ignore player direction and use the Manual Quadrant below.")]
    public bool manualOverrideDirection = false;
    public enum Quadrant { UpRight, UpLeft, DownRight, DownLeft }
    [Tooltip("Selected when Manual Override Direction is enabled.")]
    public Quadrant manualQuadrant = Quadrant.DownRight;
    [Tooltip("Apply overrides every frame (recommended). Disable to only apply on change/validation.")]
    public bool applyOverridesEveryFrame = true;

    // --- Private References ---
    private SpriteRenderer spriteRenderer;
    private Transform playerTransform; // This will be updated from the PlayerTargetManager
    private Sprite currentSprite;
    private float firePointXMagnitude;
    private Vector3 _directionToPlayer; // For Gizmos

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (firePoint != null)
        {
            firePointXMagnitude = Mathf.Abs(firePoint.localPosition.x);
        }

        // Normalize vectors on startup for accurate dot products
        directionVector_Red.Normalize();
        directionVector_Blue.Normalize();
        directionVector_Green.Normalize();
        directionVector_Yellow.Normalize();
    }

    void LateUpdate()
    {
        Sprite newSprite = null;
        float newSign = 1f; // Default right-facing for firepoint

        if (manualOverrideDirection)
        {
            // Use inspector-selected quadrant
            switch (manualQuadrant)
            {
                case Quadrant.UpRight:    newSprite = UpRightSprite;    newSign = 1f;  break;
                case Quadrant.UpLeft:     newSprite = UpLeftSprite;     newSign = -1f; break;
                case Quadrant.DownRight:  newSprite = DownRightSprite;  newSign = 1f;  break;
                case Quadrant.DownLeft:   newSprite = DownLeftSprite;   newSign = -1f; break;
            }
        }
        else
        {
            // --- PERFORMANCE OPTIMIZATION ---
            // Get the target from the central manager instead of searching every frame.
            if (PlayerTargetManager.Instance != null)
            {
                playerTransform = PlayerTargetManager.Instance.ClosestPlayer;
            }
            
            if (playerTransform == null) return; // If manager finds no player, do nothing.

            Vector3 directionToPlayer = playerTransform.position - transform.position;
            directionToPlayer.y = 0;
            
            if (directionToPlayer.sqrMagnitude < 0.01f) return; // Don't update if too close

            directionToPlayer.Normalize();
            _directionToPlayer = directionToPlayer; // Store for gizmo

            // Calculate the dot product for each of the four directions.
            float dotRed = Vector3.Dot(directionToPlayer, directionVector_Red);
            float dotBlue = Vector3.Dot(directionToPlayer, directionVector_Blue);
            float dotGreen = Vector3.Dot(directionToPlayer, directionVector_Green);
            float dotYellow = Vector3.Dot(directionToPlayer, directionVector_Yellow);

            float maxDot = Mathf.Max(dotRed, dotBlue, dotGreen, dotYellow);

            if (maxDot == dotRed)      { newSprite = UpRightSprite;   newSign = 1f; }
            else if (maxDot == dotBlue) { newSprite = UpLeftSprite;    newSign = -1f; }
            else if (maxDot == dotGreen){ newSprite = DownRightSprite; newSign = 1f; }
            else                       { newSprite = DownLeftSprite;  newSign = -1f; }
        }

        // Apply inspector-driven mirror
        if (manualFlipImage)
        {
            if (newSprite == UpRightSprite)      { newSprite = UpLeftSprite;     newSign = -newSign; }
            else if (newSprite == UpLeftSprite)  { newSprite = UpRightSprite;    newSign = -newSign; }
            else if (newSprite == DownRightSprite){ newSprite = DownLeftSprite;  newSign = -newSign; }
            else if (newSprite == DownLeftSprite){ newSprite = DownRightSprite;  newSign = -newSign; }
        }

        // Flip firepoint if it exists
        if (firePoint != null)
        {
            Vector3 newFirePointPos = firePoint.localPosition;
            newFirePointPos.x = firePointXMagnitude * newSign;
            firePoint.localPosition = newFirePointPos;
        }

        // Update the sprite only if it has changed
        if (newSprite != null && newSprite != currentSprite)
        {
            spriteRenderer.sprite = newSprite;
            currentSprite = newSprite;
        }
    }

    private void OnValidate()
    {
        if (!Application.isPlaying || applyOverridesEveryFrame)
        {
            LateUpdate();
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmos) return;

        Vector3 origin = transform.position;
        float lineLength = 2.5f;

        if (Application.isPlaying)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawLine(origin, origin + _directionToPlayer * lineLength);
        }

        Gizmos.color = Color.red;
        Gizmos.DrawLine(origin, origin + directionVector_Red.normalized * lineLength);
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(origin, origin + directionVector_Blue.normalized * lineLength);
        Gizmos.color = Color.green;
        Gizmos.DrawLine(origin, origin + directionVector_Green.normalized * lineLength);
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(origin, origin + directionVector_Yellow.normalized * lineLength);
    }
}