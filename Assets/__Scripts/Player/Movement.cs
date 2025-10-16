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
        rb = GetComponent<Rigidbody>();
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
        float horizontalInput = Input.GetAxisRaw("Horizontal");
        float verticalInput = Input.GetAxisRaw("Vertical");
        moveInput = new Vector3(horizontalInput, 0f, verticalInput);
    }

    void FixedUpdate()
    {
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

                // --- THIS IS THE NEW LOGIC ---
                // 1. Create a multiplier that defaults to 1.
                float orthoVerticalMultiplier = 1f;

                // 2. If the camera is in Orthographic mode, set the multiplier to 2.
                if (referenceCamera.orthographic)
                {
                    orthoVerticalMultiplier = 2f;
                }
                // -----------------------------

                // 3. Apply the multiplier to the vertical input (planarInput.y).
                movementDirection = normalizedRight * planarInput.x + normalizedForward * (planarInput.y * forwardCompensation * orthoVerticalMultiplier);
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
            // CORRECTED to use .velocity for 3D Rigidbody
            rb.linearVelocity = movementDirection * moveSpeed;
        }
        else
        {
            // CORRECTED to use .velocity for 3D Rigidbody
            rb.linearVelocity = Vector3.zero;
        }
    }
}