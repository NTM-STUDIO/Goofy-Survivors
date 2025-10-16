using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Movement : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("The movement speed of the player in units per second.")]
    public float moveSpeed = 5f;
    [Tooltip("Keeps on-screen speed consistent when the camera is tilted (e.g. 26.565° in X for isometric).")]
    public bool compensateCameraTilt = true;

    private Rigidbody rb;
    private Camera referenceCamera;
    private Vector3 moveInput;

    void Start()
    {
        // Get the Rigidbody2D component attached to this GameObject
        rb = GetComponent<Rigidbody>();

        // We still don't want gravity and we don't want the player to spin
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        referenceCamera = Camera.main;
        if (referenceCamera == null)
        {
            Debug.LogWarning("Movement: unable to find a main camera, movement will use world axes.");
        }
    }

    void Update()
    {
        // Gather input from the horizontal and vertical axes (WASD or Arrow Keys)
        float horizontalInput = Input.GetAxisRaw("Horizontal");
        float verticalInput = Input.GetAxisRaw("Vertical");

        // Store the input in a Vector2. This vector now directly represents
        // the desired direction in world space.
        moveInput = new Vector3(horizontalInput, 0f, verticalInput);
    }

    void FixedUpdate()
    {
        // All physics calculations should happen in FixedUpdate

        // If a camera is available, reproject input onto the camera plane so isometric
        // movement keeps a consistent on-screen speed in both axes.
        Vector3 movementDirection;
        if (referenceCamera != null)
        {
            Vector2 planarInput = new Vector2(moveInput.x, moveInput.z);
            planarInput = Vector2.ClampMagnitude(planarInput, 1f);

            Vector3 projectedRight = Vector3.ProjectOnPlane(referenceCamera.transform.right, Vector3.up);
            Vector3 projectedForward = Vector3.ProjectOnPlane(referenceCamera.transform.forward, Vector3.up);

            float rightLength = projectedRight.magnitude;
            float forwardLength = projectedForward.magnitude;

            if (rightLength > Mathf.Epsilon && forwardLength > Mathf.Epsilon)
            {
                Vector3 normalizedRight = projectedRight / rightLength;
                Vector3 normalizedForward = projectedForward / forwardLength;
                float forwardCompensation = compensateCameraTilt ? rightLength / forwardLength : 1f;

                movementDirection = normalizedRight * planarInput.x + normalizedForward * (planarInput.y * forwardCompensation);
            }
            else
            {
                movementDirection = new Vector3(planarInput.x, 0f, planarInput.y);
            }
        }
        else
        {
            Vector2 planarInput = Vector2.ClampMagnitude(new Vector2(moveInput.x, moveInput.z), 1f);
            movementDirection = new Vector3(planarInput.x, 0f, planarInput.y);
        }

        if (movementDirection.sqrMagnitude > 0.0001f)
        {
            rb.linearVelocity = movementDirection * moveSpeed;
        }
        else
        {
            rb.linearVelocity = Vector3.zero;
        }
    }
}