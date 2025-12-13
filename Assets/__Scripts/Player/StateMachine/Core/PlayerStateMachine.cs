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

        // Public context for states
        public PlayerStats Stats { get; private set; }
        public Rigidbody Rb { get; private set; }
        public Movement Movement { get; private set; }
        
        public PlayerStateType CurrentStateType => currentState?.StateType ?? PlayerStateType.None;
        public float TimeInCurrentState => Time.time - stateEnterTime;
        
        // Input tracking - exposed for states
        public Vector3 MoveInput { get; private set; }
        public bool HasMoveInput => MoveInput.sqrMagnitude > 0.0001f;
        
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

        private void OnDestroy()
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
            // Only capture input if not downed
            if (Stats != null && Stats.IsDowned)
            {
                MoveInput = Vector3.zero;
                return;
            }

            MoveInput = new Vector3(
                Input.GetAxisRaw("Horizontal"),
                0f,
                Input.GetAxisRaw("Vertical")
            );
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
