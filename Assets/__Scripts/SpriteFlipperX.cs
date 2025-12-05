using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class FlipXIsometricController : MonoBehaviour
{
    [Header("Assign Sprites")]
    [Tooltip("Sprite when facing upward (e.g., walking north).")]
    public Sprite UpSprite;

    [Tooltip("Sprite when facing downward (e.g., walking south).")]
    public Sprite DownSprite;

    [Header("Optional Child Object Control")]
    [Tooltip("(Optional) Assign a child object like a fire point. Its local X-position will flip based on direction.")]
    public Transform firePoint;

    [Header("Direction Settings")]
    [Tooltip("Angle threshold in degrees to switch between up and down sprites.")]
    public float upDownThreshold = 0f; // 0 means rely purely on Z axis comparison

    [Header("Debugging")]
    public bool showGizmos = true;

    private SpriteRenderer spriteRenderer;
    private Transform playerTransform;
    private Sprite currentSprite;
    private float firePointXMagnitude;
    private Vector3 _directionToPlayer;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();

        if (firePoint != null)
            firePointXMagnitude = Mathf.Abs(firePoint.localPosition.x);

        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }
        else
        {
            Debug.LogError("FlipXIsometricController: Player not found! Make sure your player is tagged 'Player'.", this);
            enabled = false;
        }
    }

    void LateUpdate()
    {
        if (playerTransform == null) return;

        Vector3 directionToPlayer = playerTransform.position - transform.position;
        directionToPlayer.y = 0f;
        directionToPlayer.Normalize();
        _directionToPlayer = directionToPlayer;

        if (directionToPlayer.sqrMagnitude < 0.001f) return;

        // --- Determine sprite (Up or Down) ---
        bool isFacingUp = directionToPlayer.z > upDownThreshold;
        Sprite newSprite = isFacingUp ? UpSprite : DownSprite;

        // --- Determine left or right flip ---
        bool isFacingRight = directionToPlayer.x >= 0f;
        spriteRenderer.flipX = !isFacingRight;

        // --- Flip optional fire point ---
        if (firePoint != null)
        {
            Vector3 fp = firePoint.localPosition;
            fp.x = firePointXMagnitude * (isFacingRight ? 1f : -1f);
            firePoint.localPosition = fp;
        }

        // --- Apply new sprite ---
        if (newSprite != null && newSprite != currentSprite)
        {
            spriteRenderer.sprite = newSprite;
            currentSprite = newSprite;
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

        // Visual guides
        Gizmos.color = Color.cyan;
        Gizmos.DrawRay(origin, Vector3.forward * lineLength);
        Gizmos.color = Color.magenta;
        Gizmos.DrawRay(origin, Vector3.right * lineLength);
    }
}
