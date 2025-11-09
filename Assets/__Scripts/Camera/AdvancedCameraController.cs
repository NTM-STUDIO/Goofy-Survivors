using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class AdvancedCameraController : MonoBehaviour
{
    [Header("Follow Settings")]
    [SerializeField] private Transform target;

    [Header("Orthographic Settings")]
    [SerializeField] private float orthoMinSize = 10f;
    [SerializeField] private float orthoMaxSize = 20f;

    [Header("Perspective Settings")]
    [SerializeField] private float perspMinFov = 25f;
    [SerializeField] private float perspMaxFov = 60f;

    [Header("General Settings")]
    [SerializeField] private float zoomSpeed = 5f; // Renamed from sensitivity for clarity

    private Camera cam;
    private Vector3 offset;
    private bool initializedOnce = false;

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    void Start()
    {
        // --- Set the default starting state ---
        cam.orthographic = true;
        cam.orthographicSize = 20; // Default ortho zoom is 20

      /*  if (target == null)
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
        }*/
    }

    void Update()
    {

        // Handle zooming with the scroll wheel (for direct mouse polling)
        HandleScrollZoom();
    }

    // Use LateUpdate to ensure the player has finished moving for the frame.
    void LateUpdate()
    {
        if (target == null && !TryAssignTargetByTag()) return;

        // The follow logic remains the same
        transform.position = target.position + offset;
    }

    public void OnCameraZoom(InputAction.CallbackContext context)
    {
        // Read the raw scroll value from the Input Action
        float scrollValue = context.ReadValue<Vector2>().y;
        if (Mathf.Approximately(scrollValue, 0f)) return;

        ApplyZoom(scrollValue);
    }
    
    // --- UPDATED: This function polls the mouse directly every frame ---
    private void HandleScrollZoom()
    {
        if (Mouse.current == null) return;
        
        // Read the raw scroll value directly from the mouse device
        float scrollValue = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Approximately(scrollValue, 0f)) return;

        ApplyZoom(scrollValue);
    }

    // --- NEW: A single function to apply zoom, aware of the camera's current mode ---
    private void ApplyZoom(float scrollValue)
    {
        // Scrolling up (positive value) should zoom in (decrease size/FOV)
        // We normalize the raw scroll value (often +/- 120) to a smaller delta
        float delta = -scrollValue / 120f;

        if (cam.orthographic)
        {
            float newSize = cam.orthographicSize + delta * zoomSpeed;
            cam.orthographicSize = Mathf.Clamp(newSize, orthoMinSize, orthoMaxSize);
        }
        else // Perspective
        {
            float newFov = cam.fieldOfView + delta * zoomSpeed;
            cam.fieldOfView = Mathf.Clamp(newFov, perspMinFov, perspMaxFov);
        }
    }

    // --- Target finding logic remains the same ---
    private bool TryAssignTargetByTag()
    {
        // Prefer binding to the LOCAL player's NetworkObject if running with Netcode
        var nm = Unity.Netcode.NetworkManager.Singleton;
        if (nm != null && nm.IsListening && nm.LocalClient != null && nm.LocalClient.PlayerObject != null)
        {
            SetTarget(nm.LocalClient.PlayerObject.transform);
            return true;
        }

        // Fallback to a tagged player (single-player or if local not ready yet)
        GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
        if (taggedPlayer == null) return false;
        SetTarget(taggedPlayer.transform);
        return true;
    }

    private void SetTarget(Transform newTarget)
    {
        target = newTarget;
        offset = transform.position - target.position;
        initializedOnce = true;
    }

    // Public helper to bind to a new target while preserving the existing camera offset.
    // We DON'T snap the camera to the target; we keep the current framing and follow as before.
    public void BindAndCenter(Transform newTarget, bool resetZoom = true)
    {
        target = newTarget;
        offset = transform.position - newTarget.position;
        initializedOnce = true;
        if (resetZoom)
        {
            if (cam == null) cam = GetComponent<Camera>();
            if (cam != null)
            {
                if (cam.orthographic)
                {
                    cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, orthoMinSize, orthoMaxSize);
                }
                else
                {
                    cam.fieldOfView = Mathf.Clamp(cam.fieldOfView, perspMinFov, perspMaxFov);
                }
            }
        }
    }

    // Overload with snapToTarget option
    public void BindAndCenter(Transform newTarget, bool resetZoom, bool snapToTarget)
    {
        target = newTarget;
        offset = transform.position - newTarget.position;
        initializedOnce = true;
        if (resetZoom)
        {
            if (cam == null) cam = GetComponent<Camera>();
            if (cam != null)
            {
                if (cam.orthographic)
                {
                    cam.orthographicSize = Mathf.Clamp(cam.orthographicSize, orthoMinSize, orthoMaxSize);
                }
                else
                {
                    cam.fieldOfView = Mathf.Clamp(cam.fieldOfView, perspMinFov, perspMaxFov);
                }
            }
        }
    }
}