using UnityEngine;
using Unity.Netcode;
using System;
using System.Collections.Generic;

namespace EnemyAI
{
    [RequireComponent(typeof(EnemyStats))]
    [RequireComponent(typeof(Rigidbody))]
    public class EnemyStateMachine : NetworkBehaviour
    {
        public event Action<EnemyStateType, EnemyStateType> OnStateChanged;

        [Header("State Machine")]
        [SerializeField] private EnemyStateType initialState = EnemyStateType.Chasing;
        [SerializeField] private bool debugMode = false;

        [Header("Targeting")]
        [SerializeField] private float targetUpdateInterval = 1f;
        
        [Header("Attack")]
        [SerializeField] private float attackRange = 1.5f;
        [SerializeField] private float attackCooldown = 1.5f;

        // Public context for states
        public EnemyStats Stats { get; private set; }
        public Rigidbody Rb { get; private set; }
        public EnemyMovement Movement { get; private set; }
        public Transform CurrentTarget { get; private set; }
        
        public EnemyStateType CurrentStateType => currentState?.StateType ?? EnemyStateType.None;
        public float TimeInCurrentState => Time.time - stateEnterTime;
        public float AttackRange => attackRange;
        public float AttackCooldown => attackCooldown;
        public float LastAttackTime { get; set; }
        
        // Knockback state data
        public Vector3 KnockbackDirection { get; set; }
        public float KnockbackForce { get; set; }
        public float KnockbackEndTime { get; set; }

        private IEnemyState currentState;
        private float stateEnterTime;
        private float targetSearchTimer;
        private Dictionary<EnemyStateType, IEnemyState> stateCache = new();

        private void Awake()
        {
            Stats = GetComponent<EnemyStats>();
            Rb = GetComponent<Rigidbody>();
            Movement = GetComponent<EnemyMovement>();
            CacheAllStates();
        }

        private void Start() => TransitionTo(initialState);

        private void Update()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !IsServer) return;
            if (currentState == null) return;

            targetSearchTimer -= Time.deltaTime;
            if (targetSearchTimer <= 0f)
            {
                UpdateTarget();
                targetSearchTimer = targetUpdateInterval;
            }

            currentState.Tick(this);

            var nextState = currentState.CheckTransitions(this);
            if (nextState.HasValue && nextState.Value != currentState.StateType)
            {
                TransitionTo(nextState.Value);
            }
        }

        private void FixedUpdate()
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && !IsServer) return;
            currentState?.FixedTick(this);
        }

        public void TransitionTo(EnemyStateType newStateType)
        {
            if (!stateCache.TryGetValue(newStateType, out var newState))
            {
                Debug.LogError($"[EnemyStateMachine] State {newStateType} not found!");
                return;
            }

            var previousType = currentState?.StateType ?? EnemyStateType.None;
            currentState?.Exit(this);
            
            currentState = newState;
            stateEnterTime = Time.time;
            currentState.Enter(this);

            if (debugMode) Debug.Log($"[SM] {gameObject.name}: {previousType} → {newStateType}");
            OnStateChanged?.Invoke(previousType, newStateType);
        }

        public void TriggerKnockback(Vector3 direction, float force, float duration)
        {
            KnockbackDirection = direction;
            KnockbackForce = force;
            KnockbackEndTime = Time.time + duration;
            TransitionTo(EnemyStateType.KnockedBack);
        }

        public float GetDistanceToTarget()
        {
            if (CurrentTarget == null) return float.MaxValue;
            return Vector3.Distance(transform.position, CurrentTarget.position);
        }

        public Vector3 GetDirectionToTarget()
        {
            if (CurrentTarget == null) return Vector3.zero;
            Vector3 dir = CurrentTarget.position - transform.position;
            dir.y = 0;
            return dir.normalized;
        }

        public bool CanAttack() => Time.time >= LastAttackTime + attackCooldown;
        public bool IsTargetInRange() => GetDistanceToTarget() <= attackRange;

        private void CacheAllStates()
        {
            stateCache[EnemyStateType.Idle] = new States.IdleState();
            stateCache[EnemyStateType.Chasing] = new States.ChasingState();
            stateCache[EnemyStateType.Attacking] = new States.AttackingState();
            stateCache[EnemyStateType.KnockedBack] = new States.KnockedBackState();
            stateCache[EnemyStateType.Stunned] = new States.StunnedState();
            stateCache[EnemyStateType.Fleeing] = new States.FleeingState();
            stateCache[EnemyStateType.Dying] = new States.DyingState();
            stateCache[EnemyStateType.Dead] = new States.DeadState();
        }

        private void UpdateTarget()
        {
            Transform bestTarget = null;
            float minSqrDist = float.MaxValue;
            Vector3 myPos = transform.position;

            if (PlayerManager.Instance != null && PlayerManager.Instance.ActivePlayers.Count > 0)
            {
                foreach (Transform t in PlayerManager.Instance.ActivePlayers)
                {
                    if (t == null) continue;
                    var ps = t.GetComponent<PlayerStats>();
                    if (ps != null && ps.IsDowned) continue;

                    float sqrDist = (t.position - myPos).sqrMagnitude;
                    if (sqrDist < minSqrDist)
                    {
                        minSqrDist = sqrDist;
                        bestTarget = t;
                    }
                }
            }
            else
            {
                var players = GameObject.FindGameObjectsWithTag("Player");
                foreach (var p in players)
                {
                    if (p == null) continue;
                    var ps = p.GetComponent<PlayerStats>();
                    if (ps != null && ps.IsDowned) continue;

                    float sqrDist = (p.transform.position - myPos).sqrMagnitude;
                    if (sqrDist < minSqrDist)
                    {
                        minSqrDist = sqrDist;
                        bestTarget = p.transform;
                    }
                }
            }

            CurrentTarget = bestTarget;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);

            if (Application.isPlaying && currentState != null)
            {
                UnityEditor.Handles.Label(transform.position + Vector3.up * 2f, CurrentStateType.ToString());
            }
        }
#endif
    }
}
