using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class AdvancedCameraController : MonoBehaviour
{
    [Header("Follow Settings")]
    [SerializeField] private Transform target;
    [SerializeField] private float smoothSpeed = 0.125f;

    [Header("Zoom Settings")]
    [SerializeField] private float zoomSensitivity = 1f;

    [Header("Orthographic Mode")]
    [SerializeField] private float orthoMinZoom = 10f;
    [SerializeField] private float orthoMaxZoom = 20f;

    [Header("Perspective Mode")]
    [SerializeField] private float perspMinZoom = 25f;
    [SerializeField] private float perspMaxZoom = 70f;

    private Camera cam;
    private Vector3 offset;
    private Vector3 followVelocity = Vector3.zero;

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    void Start()
    {
        if (target == null)
        {
            Debug.LogError("Camera Target not assigned!");
            return;
        }
        // Calculate the initial offset from the player
        offset = transform.position - target.position;
    }

    // Use LateUpdate to ensure the player has finished moving for the frame.
    void LateUpdate()
    {
        if (target == null) return;

        // Follow the player smoothly
        Vector3 desiredPosition = target.position + offset;
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref followVelocity, smoothSpeed);
    }

    // This function will be called by the PlayerInput component
    public void OnCameraSwitch(InputAction.CallbackContext context)
    {
        // We only care about when the button is pressed down
        if (context.performed)
        {
            // Toggle the camera's projection type
            cam.orthographic = !cam.orthographic;
        }
    }

    // This function will be called by the PlayerInput component
    public void OnCameraZoom(InputAction.CallbackContext context)
    {
        // Read the scroll value (we only need the Y axis)
        float scrollValue = context.ReadValue<Vector2>().y;

        // If there's no scroll input, do nothing
        if (scrollValue == 0) return;
        
        // The scroll wheel reports positive values for scrolling up and negative for down.
        // We subtract to make scrolling up zoom in and down zoom out.
        scrollValue *= -1;

        if (cam.orthographic)
        {
            // Adjust the Orthographic Size
            float newSize = cam.orthographicSize + (scrollValue * (zoomSensitivity / 2));
            cam.orthographicSize = Mathf.Clamp(newSize, orthoMinZoom, orthoMaxZoom);
        }
        else
        {
            // Adjust the Field of View (FOV)
            float newFov = cam.fieldOfView + (scrollValue * zoomSensitivity);
            cam.fieldOfView = Mathf.Clamp(newFov, perspMinZoom, perspMaxZoom);
        }
    }
}