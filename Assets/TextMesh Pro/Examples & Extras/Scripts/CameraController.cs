using UnityEngine;
using System.Collections;
using Unity.Netcode;

namespace TMPro.Examples
{
    
    public class CameraController : NetworkBehaviour
    {
        public enum CameraModes { Follow, Isometric }

        private Transform cameraTransform;
        private Transform target; // Parent (player) used as follow target

        public float FollowDistance = 30.0f;
        public float MaxFollowDistance = 100.0f;
        public float MinFollowDistance = 2.0f;

        public float ElevationAngle = 30.0f;
        public float MaxElevationAngle = 85.0f;
        public float MinElevationAngle = 0f;

        public float OrbitalAngle = 0f;

        public CameraModes CameraMode = CameraModes.Follow;

        public bool MovementSmoothing = true;
        public bool RotationSmoothing = false;
        private bool previousSmoothing;

        public float MovementSmoothingValue = 25f;
        public float RotationSmoothingValue = 5.0f;

        public float MoveSensitivity = 2.0f;

        private Vector3 currentVelocity = Vector3.zero;
        private Vector3 desiredPosition;
        private float mouseX;
        private float mouseY;
        private Vector3 moveVector;
        private float mouseWheel;

        private Camera cam;
        private AudioListener audioListener;
        private bool isMultiplayer = false;

        // Controls for Touches on Mobile devices
        //private float prev_ZoomDelta;


        private const string event_SmoothingValue = "Slider - Smoothing Value";
        private const string event_FollowDistance = "Slider - Camera Zoom";


        void Awake()
        {
            Debug.Log($"[CameraController] AWAKE called on {gameObject.name}, parent: {(transform.parent != null ? transform.parent.name : "NULL")}");
            
            if (QualitySettings.vSyncCount > 0)
                Application.targetFrameRate = 60;
            else
                Application.targetFrameRate = -1;

            if (Application.platform == RuntimePlatform.IPhonePlayer || Application.platform == RuntimePlatform.Android)
                Input.simulateMouseWithTouches = false;

            cameraTransform = transform;
            previousSmoothing = MovementSmoothing;
            target = transform.parent; // auto-assign parent as target
            
            cam = GetComponent<Camera>();
            audioListener = GetComponent<AudioListener>();
            
            Debug.Log($"[CameraController] Camera component: {(cam != null ? "FOUND" : "NULL")}, AudioListener: {(audioListener != null ? "FOUND" : "NULL")}");
            
            // Check if we're in multiplayer mode - NetworkManager must exist AND be actively listening
            isMultiplayer = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
            
            Debug.Log($"[CameraController] NetworkManager.Singleton: {(NetworkManager.Singleton != null ? "EXISTS" : "NULL")}, IsListening: {(NetworkManager.Singleton != null ? NetworkManager.Singleton.IsListening.ToString() : "N/A")}, isMultiplayer: {isMultiplayer}");
            
            // In multiplayer, disable by default until we know if we're the owner
            // In singleplayer, keep camera and audio listener enabled
            if (isMultiplayer)
            {
                if (cam != null) cam.enabled = false;
                if (audioListener != null) audioListener.enabled = false;
                Debug.Log($"[CameraController] Multiplayer mode - camera disabled until ownership confirmed");
            }
            else
            {
                // In singleplayer, ensure camera and audio listener are enabled
                if (cam != null) cam.enabled = true;
                if (audioListener != null) audioListener.enabled = true;
                Debug.Log($"[CameraController] Singleplayer mode - camera ENABLED at local pos: {transform.localPosition}");
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
                Debug.Log($"[CameraController] Multiplayer camera on {gameObject.name} set to: {shouldBeActive} (IsOwner: {IsOwner})");
            }
            
            if (audioListener != null)
            {
                audioListener.enabled = shouldBeActive;
                Debug.Log($"[CameraController] Multiplayer AudioListener on {gameObject.name} set to: {shouldBeActive} (IsOwner: {IsOwner})");
            }

            // If no target is set, use the parent (player) transform
            if (target == null && transform.parent != null)
            {
                target = transform.parent;
                Debug.Log($"[CameraController] Target set to parent: {target.name}");
            }
            
            // Camera stays at its local position as child of player - no movement needed
            Debug.Log($"[CameraController] Camera active at local position: {transform.localPosition}, local rotation: {transform.localEulerAngles}");
        }


        // Use this for initialization
        void Start() {}

        // Update is called once per frame
        void LateUpdate()
        {
            // Camera is a child of the player - it follows automatically
            // No manual position/rotation updates needed
            // This script only handles enabling/disabling camera based on ownership in multiplayer
        }



        void GetPlayerInput()
        {
            // Mouse scroll
            mouseWheel = Input.GetAxis("Mouse ScrollWheel");
            int touchCount = Input.touchCount;

            // Orbit & elevation with right mouse
            if (Input.GetMouseButton(1))
            {
                mouseY = Input.GetAxis("Mouse Y");
                mouseX = Input.GetAxis("Mouse X");
                if (Mathf.Abs(mouseY) > 0.01f)
                {
                    ElevationAngle -= mouseY * MoveSensitivity;
                    ElevationAngle = Mathf.Clamp(ElevationAngle, MinElevationAngle, MaxElevationAngle);
                }
                if (Mathf.Abs(mouseX) > 0.01f)
                {
                    OrbitalAngle += mouseX * MoveSensitivity;
                }
            }

            // Touch single finger rotate / elevate
            if (touchCount == 1 && Input.GetTouch(0).phase == TouchPhase.Moved)
            {
                Vector2 d = Input.GetTouch(0).deltaPosition;
                if (Mathf.Abs(d.y) > 0.01f)
                {
                    ElevationAngle -= d.y * 0.1f;
                    ElevationAngle = Mathf.Clamp(ElevationAngle, MinElevationAngle, MaxElevationAngle);
                }
                if (Mathf.Abs(d.x) > 0.01f)
                {
                    OrbitalAngle += d.x * 0.1f;
                }
            }

            // Pinch zoom
            if (touchCount == 2)
            {
                Touch t0 = Input.GetTouch(0);
                Touch t1 = Input.GetTouch(1);
                Vector2 p0Prev = t0.position - t0.deltaPosition;
                Vector2 p1Prev = t1.position - t1.deltaPosition;
                float prevDist = (p0Prev - p1Prev).magnitude;
                float dist = (t0.position - t1.position).magnitude;
                float zoomDelta = prevDist - dist;
                if (Mathf.Abs(zoomDelta) > 0.01f)
                {
                    FollowDistance += zoomDelta * 0.25f;
                    FollowDistance = Mathf.Clamp(FollowDistance, MinFollowDistance, MaxFollowDistance);
                }
            }

            // Mouse wheel zoom
            if (Mathf.Abs(mouseWheel) > 0.01f)
            {
                FollowDistance -= mouseWheel * 5f;
                FollowDistance = Mathf.Clamp(FollowDistance, MinFollowDistance, MaxFollowDistance);
            }
        }
    }
}