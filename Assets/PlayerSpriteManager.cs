using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public class Player4WaySpriteFlipperIsometric : MonoBehaviour
{
    public enum FlipMode { TwoWay, FourWay }
    [Header("Mode")]
    public FlipMode flipMode = FlipMode.FourWay;

    [Header("Assign Sprites (Screen Directions)")]
    public Sprite UpRightSprite;
    public Sprite UpLeftSprite;
    public Sprite DownRightSprite;
    public Sprite DownLeftSprite;

    [Header("Optional Child Object Flip (Firepoint)")]
    public Transform firePoint;

    [Header("Settings")]
    public float velocityThreshold = 0.1f;

    // --- Private ---
    private SpriteRenderer spriteRenderer;
    private Rigidbody rb;
    private Transform cam;

    private float firePointXMagnitude;
    private Sprite currentSprite;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        rb = GetComponentInParent<Rigidbody>();

        if (Camera.main != null)
            cam = Camera.main.transform;

        if (firePoint != null)
            firePointXMagnitude = Mathf.Abs(firePoint.localPosition.x);
    }

    void LateUpdate()
    {
        if (cam == null) return;

        Vector3 vel = rb.linearVelocity;
        vel.y = 0;

        if (vel.sqrMagnitude < velocityThreshold * velocityThreshold)
            return;

        vel.Normalize();

        Vector3 camForward = cam.forward;
        camForward.y = 0;
        camForward.Normalize();

        Vector3 camRight = cam.right;
        camRight.y = 0;
        camRight.Normalize();

        float dotForward = Vector3.Dot(vel, camForward);   // + = moving down screen
        float dotRight   = Vector3.Dot(vel, camRight);     // + = moving right

        Sprite newSprite = currentSprite;
        float sign = 1f;

        // -------- 2-WAY MODE --------
        if (flipMode == FlipMode.TwoWay)
        {
            if (dotRight >= 0)
            {
                // moving right
                newSprite = RightMostSprite(); 
                sign = 1f;
            }
            else
            {
                // moving left
                newSprite = LeftMostSprite();
                sign = -1f;
            }
        }
        else
        {
            // -------- 4-WAY MODE --------
            if (dotForward >= 0) // Down
            {
                if (dotRight >= 0)
                {
                    newSprite = DownRightSprite;
                    sign = 1f;
                }
                else
                {
                    newSprite = DownLeftSprite;
                    sign = -1f;
                }
            }
            else // Up
            {
                if (dotRight >= 0)
                {
                    newSprite = UpRightSprite;
                    sign = 1f;
                }
                else
                {
                    newSprite = UpLeftSprite;
                    sign = -1f;
                }
            }
        }

        // Apply sprite if changed
        if (newSprite != currentSprite)
        {
            spriteRenderer.sprite = newSprite;
            currentSprite = newSprite;
        }

        // Flip firepoint
        if (firePoint != null)
        {
            Vector3 p = firePoint.localPosition;
            p.x = firePointXMagnitude * sign;
            firePoint.localPosition = p;
        }
    }

    // Helper: pick best left sprite for 2-way mode:
    private Sprite LeftMostSprite()
    {
        if (UpLeftSprite != null) return UpLeftSprite;
        if (DownLeftSprite != null) return DownLeftSprite;
        return currentSprite;
    }

    // Helper: pick best right sprite for 2-way mode:
    private Sprite RightMostSprite()
    {
        if (UpRightSprite != null) return UpRightSprite;
        if (DownRightSprite != null) return DownRightSprite;
        return currentSprite;
    }
}
