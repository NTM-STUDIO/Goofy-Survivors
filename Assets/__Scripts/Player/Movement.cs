using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class Movement : MonoBehaviour
{
    [Header("Movement Settings")]
    [Tooltip("The movement speed of the player in units per second.")]
    public float moveSpeed = 5f;

    private Rigidbody rb;
    private Vector3 moveInput;

    void Start()
    {
        // Get the Rigidbody2D component attached to this GameObject
        rb = GetComponent<Rigidbody>();

        // We still don't want gravity and we don't want the player to spin
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation;
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

        // We normalize the input vector. This is important to prevent the player
        // from moving faster diagonally when two keys (e.g., W and D) are pressed.
        // It ensures the movement speed is consistent in all directions.
        Vector3 normalizedInput = moveInput.normalized;

        // Apply the calculated velocity to the Rigidbody.
        // The movement is now directly mapped from the input.
        rb.linearVelocity = normalizedInput * moveSpeed;
    }
}