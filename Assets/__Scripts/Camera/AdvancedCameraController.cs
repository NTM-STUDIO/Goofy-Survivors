using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;

[RequireComponent(typeof(Camera))]
public class AdvancedCameraController : NetworkBehaviour
{
    [Header("Follow Settings")]
    [SerializeField] private Transform target;

    [Header("Orthographic Settings")]
    [SerializeField] private float orthoMinSize = 30f;
    [SerializeField] private float orthoMaxSize = 20f;

    [Header("Perspective Settings")]
    [SerializeField] private float perspMinFov = 25f;
    [SerializeField] private float perspMaxFov = 60f;

    [Header("General Settings")]
    [SerializeField] private float zoomSpeed = 5f; // Renamed from sensitivity for clarity

    private Camera cam;
    private AudioListener audioListener;
    private Vector3 offset;
    private bool initializedOnce = false;
    private bool isMultiplayer = false;

    void Awake()
    {
        cam = GetComponent<Camera>();
        audioListener = GetComponent<AudioListener>();
        
        // Auto-assign parent (player) as target if not manually set
        if (target == null && transform.parent != null)
        {
            target = transform.parent;
            Debug.Log($"[AdvancedCameraController] Auto-assigned parent as target: {target.name}");
        }
        
        // Check if we're in multiplayer mode - NetworkManager must exist AND be actively listening
        isMultiplayer = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        
        Debug.Log($"[AdvancedCameraController] Awake on {gameObject.name}, isMultiplayer: {isMultiplayer}");
        
        // In multiplayer, disable by default until we know if we're the owner
        // In singleplayer, keep camera and audio listener enabled
        if (isMultiplayer)
        {
            if (cam != null) cam.enabled = false;
            if (audioListener != null) audioListener.enabled = false;
            Debug.Log($"[AdvancedCameraController] Multiplayer mode - camera disabled until ownership confirmed");
        }
        else
        {
            // In singleplayer, camera stays enabled
            if (cam != null) cam.enabled = true;
            if (audioListener != null) audioListener.enabled = true;
            Debug.Log($"[AdvancedCameraController] Singleplayer mode - camera enabled");
        }
    }
    
    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        // Only enable camera and audio listener for the local player (owner)
        bool shouldBeActive = IsOwner;
        
        if (cam != null)
        {
            cam.enabled = shouldBeActive;
            Debug.Log($"[AdvancedCameraController] Multiplayer camera set to: {shouldBeActive} (IsOwner: {IsOwner})");
        }
        
        if (audioListener != null)
        {
            audioListener.enabled = shouldBeActive;
            Debug.Log($"[AdvancedCameraController] Multiplayer AudioListener set to: {shouldBeActive} (IsOwner: {IsOwner})");
        }
    }

    void Start()
    {
        // --- Set the default starting state ---
        cam.orthographic = true;
        cam.orthographicSize = 30; // Default ortho zoom is 20

        // If target was assigned (either manually or auto-assigned to parent), calculate offset
        if (target != null)
        {
            offset = transform.position - target.position;
            initializedOnce = true;
            Debug.Log($"[AdvancedCameraController] Initialized with target: {target.name}, offset: {offset}");
        }
        else
        {
            // Fallback: try to find player by tag (for scene cameras)
            TryAssignTargetByTag();
        }
    }

    void Update()
    {

        // Handle zooming with the scroll wheel (for direct mouse polling)
        HandleScrollZoom();
    }

    // Use LateUpdate to ensure the player has finished moving for the frame.
    void LateUpdate()
    {
        // In multiplayer, only update camera if this is the owner's camera
        if (isMultiplayer && !IsOwner) return;
        
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
        // In multiplayer, only allow zoom for the owner's camera
        if (isMultiplayer && !IsOwner) return;
        
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
        var nm = NetworkManager.Singleton;
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

    // Simple function to rebind camera to new player while keeping original behavior
    public void RebindToPlayer(Transform newTarget)
    {
        if (newTarget != null)
        {
            target = newTarget;
            // Keep the same offset as before, or use default if not set
            if (!initializedOnce)
            {
                offset = transform.position - newTarget.position;
            }
            // Snap camera to player position + current offset
            transform.position = newTarget.position + offset;
            initializedOnce = true;
            Debug.Log("[AdvancedCameraController] Rebound to new player");
        }
    }
}