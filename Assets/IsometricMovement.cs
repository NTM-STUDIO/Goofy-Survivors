using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class IsometricPlayerMovement : MonoBehaviour
{
    [SerializeField] private float speed = 5f;

    private CharacterController controller;
    private Vector3 moveDirection;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        // Get input from WASD keys
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");

        // Create a direction vector based on input
        Vector3 inputDirection = new Vector3(horizontalInput, 0, verticalInput);

        // Get the camera's forward and right vectors
        Vector3 cameraForward = Camera.main.transform.forward;
        Vector3 cameraRight = Camera.main.transform.right;

        // Ensure the vectors are on the horizontal plane
        cameraForward.y = 0;
        cameraRight.y = 0;

        // Normalize the vectors to ensure consistent speed
        cameraForward.Normalize();
        cameraRight.Normalize();

        // Calculate the desired move direction relative to the camera
        moveDirection = (cameraForward * verticalInput + cameraRight * horizontalInput).normalized;

        // Move the character controller
        controller.Move(moveDirection * speed * Time.deltaTime);
    }
}