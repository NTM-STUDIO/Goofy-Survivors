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
    // *** NOTE: The variable names no longer match the direction, but they match the color. ***
    // This is intentional to make the logic below work with your color mapping.
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
    private Transform playerTransform;
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

        // Don't assume a single tagged Player; target will be resolved dynamically in LateUpdate
        playerTransform = null;

        // Normalize vectors on startup
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
            // Use inspector-selected quadrant; don't resolve player direction
            switch (manualQuadrant)
            {
                case Quadrant.UpRight:    newSprite = UpRightSprite;    newSign = 1f;  break;
                case Quadrant.UpLeft:     newSprite = UpLeftSprite;     newSign = -1f; break;
                case Quadrant.DownRight:  newSprite = DownRightSprite;  newSign = 1f;  break;
                case Quadrant.DownLeft:   newSprite = DownLeftSprite;   newSign = -1f; break;
            }

            // Store a synthetic gizmo direction that roughly matches the choice (for editor visuals)
            switch (manualQuadrant)
            {
                case Quadrant.UpRight:   _directionToPlayer = directionVector_Red;    break;
                case Quadrant.UpLeft:    _directionToPlayer = directionVector_Blue;   break;
                case Quadrant.DownRight: _directionToPlayer = directionVector_Green;  break;
                case Quadrant.DownLeft:  _directionToPlayer = directionVector_Yellow; break;
            }
        }
        else
        {
            // Resolve or refresh target each frame to avoid looking at downed players
            if (playerTransform == null || IsTargetDowned(playerTransform))
            {
                playerTransform = FindClosestActivePlayer();
            }

            if (playerTransform == null) return;

            Vector3 directionToPlayer = playerTransform.position - transform.position;
            directionToPlayer.y = 0;
            directionToPlayer.Normalize();
            
            _directionToPlayer = directionToPlayer; // Store for gizmo

            if (directionToPlayer.sqrMagnitude < 0.01f) return;

            // Calculate the dot product for each of the four directions.
            float dotRed = Vector3.Dot(directionToPlayer, directionVector_Red);
            float dotBlue = Vector3.Dot(directionToPlayer, directionVector_Blue);
            float dotGreen = Vector3.Dot(directionToPlayer, directionVector_Green);
            float dotYellow = Vector3.Dot(directionToPlayer, directionVector_Yellow);

            float maxDot = Mathf.Max(dotRed, dotBlue, dotGreen, dotYellow);

            // *** LOGIC UPDATED TO MATCH YOUR COLOR MAPPING ***
            if (maxDot == dotRed) // Red Line
            {
                newSprite = UpRightSprite;
                newSign = 1f; // Right-facing
            }
            else if (maxDot == dotBlue) // Blue Line
            {
                newSprite = UpLeftSprite;
                newSign = -1f; // Left-facing
            }
            else if (maxDot == dotGreen) // Green Line
            {
                newSprite = DownRightSprite;
                newSign = 1f; // Right-facing
            }
            else // maxDot == dotYellow (Yellow Line)
            {
                newSprite = DownLeftSprite;
                newSign = -1f; // Left-facing
            }
        }

        // Apply inspector-driven mirror (swap left/right sprites without using flipX)
        if (manualFlipImage)
        {
            if (newSprite == UpRightSprite)      { newSprite = UpLeftSprite;     newSign = -newSign; }
            else if (newSprite == UpLeftSprite)  { newSprite = UpRightSprite;    newSign = -newSign; }
            else if (newSprite == DownRightSprite){ newSprite = DownLeftSprite;  newSign = -newSign; }
            else if (newSprite == DownLeftSprite){ newSprite = DownRightSprite;  newSign = -newSign; }
        }

        if (firePoint != null)
        {
            Vector3 newFirePointPos = firePoint.localPosition;
            newFirePointPos.x = firePointXMagnitude * newSign;
            firePoint.localPosition = newFirePointPos;
        }

        if (newSprite != null && newSprite != currentSprite)
        {
            spriteRenderer.sprite = newSprite;
            currentSprite = newSprite;
        }
    }

    private void OnValidate()
    {
        // In editor, apply overrides immediately for fast iteration
        if (!Application.isPlaying || applyOverridesEveryFrame)
        {
            // Force a one-shot update
            LateUpdate();
        }
    }

    private bool IsTargetDowned(Transform t)
    {
        if (t == null) return true;
        var ps = t.GetComponent<PlayerStats>();
        return (ps == null) || ps.IsDowned;
    }

    private Transform FindClosestActivePlayer()
    {
        Transform closest = null;
        float minDist = float.MaxValue;

        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening)
        {
            foreach (var client in nm.ConnectedClientsList)
            {
                if (client?.PlayerObject == null) continue;
                var ps = client.PlayerObject.GetComponent<PlayerStats>();
                if (ps == null || ps.IsDowned) continue;
                float d = Vector3.Distance(transform.position, ps.transform.position);
                if (d < minDist)
                {
                    minDist = d;
                    closest = ps.transform;
                }
            }
        }
        else
        {
            // Single-player/editor: scan PlayerStats in scene
            var all = Object.FindObjectsByType<PlayerStats>(FindObjectsSortMode.None);
            foreach (var ps in all)
            {
                if (ps == null || ps.IsDowned) continue;
                float d = Vector3.Distance(transform.position, ps.transform.position);
                if (d < minDist)
                {
                    minDist = d;
                    closest = ps.transform;
                }
            }
        }
        return closest;
    }

    // --- VISUAL DEBUGGER (Comments updated to match your mapping) ---
    private void OnDrawGizmosSelected()
    {
        if (!showGizmos) return; // Works in editor too now

        Vector3 origin = transform.position;
        float lineLength = 2.5f;

        // Only draw the white line if the game is playing
        if(Application.isPlaying)
        {
            Gizmos.color = Color.white;
            Gizmos.DrawLine(origin, origin + _directionToPlayer * lineLength);
        }

        // Draw Direction Vectors
        Gizmos.color = Color.red; // Your Top-Right
        Gizmos.DrawLine(origin, origin + directionVector_Red * lineLength);

        Gizmos.color = Color.blue; // Your Top-Left
        Gizmos.DrawLine(origin, origin + directionVector_Blue * lineLength);

        Gizmos.color = Color.green; // Your Bottom-Right
        Gizmos.DrawLine(origin, origin + directionVector_Green * lineLength);

        Gizmos.color = Color.yellow; // Your Bottom-Left
        Gizmos.DrawLine(origin, origin + directionVector_Yellow * lineLength);
    }
}