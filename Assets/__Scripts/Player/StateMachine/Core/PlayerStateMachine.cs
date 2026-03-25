using UnityEngine;
using Unity.Netcode;
using System;
using System.Collections.Generic;

namespace PlayerAI
{
    [RequireComponent(typeof(PlayerStats))]
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerStateMachine : NetworkBehaviour
    {
        public event Action<PlayerStateType, PlayerStateType> OnStateChanged;

        [Header("State Machine")]
        [SerializeField] private PlayerStateType initialState = PlayerStateType.Idle;
        [SerializeField] private bool debugMode = false;

        [Header("Visual Settings")]
        [Tooltip("Enable this if your default animation/sprite faces LEFT.")]
        public bool SpriteFacesLeftByDefault = false;
        [Tooltip("Assign the default sprite to use when the player is stopped (Idle).")]
        public Sprite DefaultIdleSprite;
        [Tooltip("Scale to apply to the Idle Sprite (1 = normal, 0.5 = metade do tamanho).")]
        public float DefaultIdleSpriteScale = 1f;

        // Public context for states
        public PlayerStats Stats { get; private set; }
        public Rigidbody Rb { get; private set; }
        public Movement Movement { get; private set; }
        public Animator Animator { get; private set; }
        public SpriteRenderer SpriteRenderer { get; private set; }
        public Vector3 OriginalSpriteLocalScale { get; private set; }
        
        public PlayerStateType CurrentStateType => currentState?.StateType ?? PlayerStateType.None;
        public float TimeInCurrentState => Time.time - stateEnterTime;
        
        // Input tracking - exposed for states
        public Vector3 MoveInput { get; private set; }
        public bool HasMoveInput => MoveInput.sqrMagnitude > 0.0001f;
        
        // Visual state tracking
        public float LastHorizontalDirection { get; set; } = 1f; // 1 for Right, -1 for Left

        // Damage state data
        public float DamageStateEndTime { get; set; }
        public PlayerStateType PreDamageState { get; set; }

        private IPlayerState currentState;
        private float stateEnterTime;
        private Dictionary<PlayerStateType, IPlayerState> stateCache = new();
        
        // GameManager reference for network checks
        private GameManager gameManager;

        private void Awake()
        {
            Stats = GetComponent<PlayerStats>();
            Rb = GetComponent<Rigidbody>();
            Movement = GetComponent<Movement>();
            Animator = GetComponentInChildren<Animator>();
            SpriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (Animator == null) Debug.LogError($"[PlayerStateMachine] Animator not found on {gameObject.name} or its children!");
            if (SpriteRenderer == null) Debug.LogWarning($"[PlayerStateMachine] SpriteRenderer not found on {gameObject.name} or its children!");
            else OriginalSpriteLocalScale = SpriteRenderer.transform.localScale;

            // Check for conflicting scripts
            var spriteFlipper = GetComponentInChildren<Player4WaySpriteFlipperIsometric>();
            if (spriteFlipper != null && spriteFlipper.enabled)
            {
                Debug.LogWarning($"[PlayerStateMachine] Conflict detected: 'Player4WaySpriteFlipperIsometric' is enabled. It might override the Animator. Consider disabling it.");
            }
            
            gameManager = GameManager.Instance;
            CacheAllStates();
        }

        private void Start()
        {
            // Subscribe to PlayerStats events
            if (Stats != null)
            {
                Stats.OnDamaged += HandleDamaged;
                Stats.OnDeath += HandleDeath;
                Stats.OnHealed += HandleHealed;
            }
            
            TransitionTo(initialState);
        }

        protected new void OnDestroy()
        {
            if (Stats != null)
            {
                Stats.OnDamaged -= HandleDamaged;
                Stats.OnDeath -= HandleDeath;
                Stats.OnHealed -= HandleHealed;
            }
        }

        private void Update()
        {
            // Network ownership check
            if (gameManager != null && gameManager.isP2P && !IsOwner) return;
            if (currentState == null) return;

            // Update input
            UpdateInput();

            // Run state logic
            currentState.Tick(this);

            // Check for transitions
            var nextState = currentState.CheckTransitions(this);
            if (nextState.HasValue && nextState.Value != currentState.StateType)
            {
                TransitionTo(nextState.Value);
            }
        }

        private void FixedUpdate()
        {
            if (gameManager != null && gameManager.isP2P && !IsOwner) return;
            currentState?.FixedTick(this);
        }

        private void UpdateInput()
        {
            // Safety: Force zero input if game is paused (TimeScale ~ 0)
            if (Time.timeScale < 0.001f)
            {
                MoveInput = Vector3.zero;
                return;
            }

            // Only capture input if not downed
            if (Stats != null && Stats.IsDowned)
            {
                MoveInput = Vector3.zero;
                return;
            }

            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");

            // DEADZONE: Prevent stick drift or tiny inputs from preventing Idle state
            if (Mathf.Abs(h) < 0.1f) h = 0f;
            if (Mathf.Abs(v) < 0.1f) v = 0f;

            MoveInput = new Vector3(h, 0f, v);

            // Update facing direction memory immediately when input changes
            if (Mathf.Abs(MoveInput.x) > 0.01f)
            {
                LastHorizontalDirection = MoveInput.x;
            }
        }

        public void TransitionTo(PlayerStateType newStateType)
        {
            if (!stateCache.TryGetValue(newStateType, out var newState))
            {
                Debug.LogError($"[PlayerStateMachine] State {newStateType} not found!");
                return;
            }

            var previousType = currentState?.StateType ?? PlayerStateType.None;
            currentState?.Exit(this);
            
            currentState = newState;
            stateEnterTime = Time.time;
            
            if (debugMode) Debug.Log($"[PlayerSM] Entering state: {newStateType}");
            currentState.Enter(this);

            if (debugMode) Debug.Log($"[PlayerSM] {gameObject.name}: {previousType} -> {newStateType}");
            OnStateChanged?.Invoke(previousType, newStateType);
        }

        public void TriggerDamageState(float duration)
        {
            if (CurrentStateType == PlayerStateType.Downed) return;
            
            PreDamageState = CurrentStateType;
            DamageStateEndTime = Time.time + duration;
            TransitionTo(PlayerStateType.Damaged);
        }

        private void HandleDamaged()
        {
            // Damage state is triggered by PlayerStats.ApplyDamage via invincibility frames
        }

        private void HandleDeath()
        {
            TransitionTo(PlayerStateType.Downed);
        }

        private void HandleHealed()
        {
            if (CurrentStateType == PlayerStateType.Downed && Stats.CurrentHp > 0)
            {
                TransitionTo(PlayerStateType.Idle);
            }
        }

        public void StartReviving()
        {
            if (CurrentStateType == PlayerStateType.Downed)
            {
                TransitionTo(PlayerStateType.Reviving);
            }
        }

        public void CancelReviving()
        {
            if (CurrentStateType == PlayerStateType.Reviving)
            {
                TransitionTo(PlayerStateType.Downed);
            }
        }

        public void CompleteRevive()
        {
            TransitionTo(PlayerStateType.Idle);
        }

        private void CacheAllStates()
        {
            stateCache[PlayerStateType.Idle] = new States.IdleState();
            stateCache[PlayerStateType.Moving] = new States.MovingState();
            stateCache[PlayerStateType.Damaged] = new States.DamagedState();
            stateCache[PlayerStateType.Downed] = new States.DownedState();
            stateCache[PlayerStateType.Reviving] = new States.RevivingState();
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (Application.isPlaying && currentState != null)
            {
                UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, CurrentStateType.ToString());
            }
        }
#endif
    }
}
