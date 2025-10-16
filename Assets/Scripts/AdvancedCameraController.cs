using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class AdvancedCameraController : MonoBehaviour
{
    [Header("Follow Settings")]
    [SerializeField] private Transform target;

    [Header("Zoom Settings")]
    [SerializeField] private float zoomSensitivity = 1f;
    [SerializeField] private float minFieldOfView = 25f;
    [SerializeField] private float maxFieldOfView = 70f;

    private Camera cam;
    private Vector3 offset;

    void Awake()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = false;
    }

    void Start()
    {
        if (target == null)
        {
            if (!TryAssignTargetByTag())
            {
                Debug.LogError("Camera Target not assigned!");
                return;
            }
        }
        else
        {
            offset = transform.position - target.position;
        }
    }

    // Use LateUpdate to ensure the player has finished moving for the frame.
    void Update()
    {
        HandleScrollZoom();
    }

    void LateUpdate()
    {
        if (target == null && !TryAssignTargetByTag()) return;

        transform.position = target.position + offset;
    }

    // This function will be called by the PlayerInput component
    public void OnCameraZoom(InputAction.CallbackContext context)
    {
        float scrollValue = context.ReadValue<Vector2>().y;
        if (Mathf.Approximately(scrollValue, 0f)) return;

        ApplyZoomDelta(scrollValue * -1f);
    }

    private bool TryAssignTargetByTag()
    {
        GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
        if (taggedPlayer == null) return false;

        SetTarget(taggedPlayer.transform);
        return true;
    }

    private void SetTarget(Transform newTarget)
    {
        target = newTarget;
        offset = transform.position - target.position;
    }

    private void HandleScrollZoom()
    {
        if (Mouse.current == null) return;

        float scrollValue = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Approximately(scrollValue, 0f)) return;

        ApplyZoomDelta(scrollValue * -1f);
    }

    private void ApplyZoomDelta(float scrollValue)
    {
        float newFov = cam.fieldOfView + (scrollValue * zoomSensitivity);
        cam.fieldOfView = Mathf.Clamp(newFov, minFieldOfView, maxFieldOfView);
    }
}