using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CloneAIController : MonoBehaviour
{
    private PlayerStats ownerStats;
    private PlayerStats selfStats;
    private ShadowClone parentClone;
    private Rigidbody rb;
    private Transform currentTarget;

    [Header("Configurações")]
    public float thinkInterval = 0.2f;
    public float detectionRadius = 10f;
    public float attackRange = 2f;
    public float followDistance = 3f;
    public float moveSpeedMultiplier = 1f;

    [Header("Otimização")]
    public LayerMask enemyLayer;

    private Vector3 intendedVelocity;

    private enum CloneState
    {
        Following,      
        Engaging,       
        Retreating,     
        Defending       
    }

    private CloneState currentState = CloneState.Following;
    private float optimalAttackRange = 2f;
    private bool areWeaponsReady = true;

    public void Initialize(PlayerStats self, PlayerStats owner, ShadowClone clone)
    {
        this.selfStats = self;
        this.ownerStats = owner;
        this.parentClone = clone;

        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();

        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionY;
        
        rb.interpolation = RigidbodyInterpolation.Interpolate;

        DetermineWeaponStatus();

        StartCoroutine(ThinkLoop());
    }

    private void FixedUpdate()
    {
        if (rb != null)
        {
            rb.linearVelocity = intendedVelocity;
        }
    }

    private IEnumerator ThinkLoop()
    {
        var wait = new WaitForSeconds(thinkInterval);

        while (this != null && gameObject != null)
        {
            if (Time.frameCount % 15 == 0)
                DetermineWeaponStatus();

            ThinkOnce();
            yield return wait;
        }
    }

    private void DetermineWeaponStatus()
    {
        if (selfStats == null) {
            optimalAttackRange = attackRange;
            areWeaponsReady = true;
            return;
        }
        
        var weaponControllers = selfStats.GetComponentsInChildren<WeaponController>();
        if (weaponControllers.Length == 0) {
            optimalAttackRange = attackRange;
            areWeaponsReady = true;
            return;
        }

        float maxRange = 2f; // Minimo
        bool hasMelee = false;
        bool anyReady = false;

        foreach (var wc in weaponControllers)
        {
            if (wc.WeaponData == null) continue;
            
            if (wc.IsReady) anyReady = true;

            switch (wc.WeaponData.archetype)
            {
                case WeaponArchetype.Projectile:
                case WeaponArchetype.Laser:
                    maxRange = Mathf.Max(maxRange, 8f);
                    break;
                case WeaponArchetype.Melee:
                case WeaponArchetype.Whip:
                case WeaponArchetype.Shield:
                    hasMelee = true;
                    break;
                case WeaponArchetype.Aura:
                case WeaponArchetype.Orbit:
                    maxRange = Mathf.Max(maxRange, 4f);
                    break;
            }
        }
        
        optimalAttackRange = hasMelee ? 2.5f : maxRange;
        areWeaponsReady = anyReady;
    }

    private void ThinkOnce()
    {
        if (selfStats != null && selfStats.CurrentHp < selfStats.maxHp * 0.3f)
        {
            currentState = CloneState.Retreating;
            if (ownerStats != null)
            {
                MoveTowards(ownerStats.transform.position);
                return;
            }
        }

        Transform bestTarget = FindBestTarget();

        if (bestTarget != null)
        {
            currentTarget = bestTarget;
            if (ownerStats != null && Vector3.Distance(bestTarget.position, ownerStats.transform.position) < 5f)
            {
                currentState = CloneState.Defending;
            }
            else
            {
                currentState = CloneState.Engaging;
            }
        }
        else
        {
            currentTarget = null;
            currentState = CloneState.Following;
        }

        if (currentTarget != null)
        {
            float dist = Vector3.Distance(transform.position, currentTarget.position);
            
            float effectiveAttackRange = areWeaponsReady ? optimalAttackRange : optimalAttackRange * 1.5f;

            if (dist > effectiveAttackRange)
            {
                MoveTowards(currentTarget.position);
            }
            else
            {
                bool shouldKite = !areWeaponsReady || (optimalAttackRange > 4f && dist < optimalAttackRange * 0.5f);

                if (shouldKite)
                {
                    Vector3 awayDir = (transform.position - currentTarget.position).normalized;
                    Vector3 sideDir = Vector3.Cross(Vector3.up, awayDir);
                    Vector3 kiteDir = (awayDir + sideDir * 0.5f).normalized;
                    
                    MoveTowards(transform.position + kiteDir * 3f); 
                }
                else
                {
                    StopMoving();
                }
            }
        }
        else
        {
            if (ownerStats != null)
            {
                Vector3 ownerPos = ownerStats.transform.position;
                float dOwner = Vector3.Distance(transform.position, ownerPos);
                if (dOwner > followDistance)
                {
                    MoveTowards(ownerPos);
                }
                else
                {
                    StopMoving();
                }
            }
            else
            {
                StopMoving();
            }
        }
    }

    private Transform FindBestTarget()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, detectionRadius, enemyLayer);
        Transform bestTarget = null;
        float bestScore = float.MinValue;
        
        foreach (var hit in hits)
        {
            if (hit.transform == transform) continue;
            
            float distance = Vector3.Distance(transform.position, hit.transform.position);
            float score = 0f;
            
            score += Mathf.Clamp(10f - distance, 0f, 10f);
            
            if (ownerStats != null)
            {
                float distToOwner = Vector3.Distance(hit.transform.position, ownerStats.transform.position);
                if (distToOwner < 5f)
                {
                    score += 20f;
                }
            }
            
            var enemyStats = hit.GetComponent<EnemyStats>();
            if (enemyStats != null && enemyStats.MaxHealth > 0)
            {
                float healthPercent = enemyStats.CurrentHealth / enemyStats.MaxHealth;
                if (healthPercent < 0.3f)
                {
                    score += 5f;
                }
            }
            
            if (score > bestScore)
            {
                bestScore = score;
                bestTarget = hit.transform;
            }
        }
        
        return bestTarget;
    }

    private void MoveTowards(Vector3 worldTarget)
    {
        Vector3 dir = (worldTarget - transform.position);
        dir.y = 0;
        
        if (dir.sqrMagnitude < 0.0001f)
        {
            StopMoving();
            return;
        }

        dir.Normalize();

        float obstacleCheckDist = 1.5f;
        if (Physics.Raycast(transform.position, dir, out RaycastHit hit, obstacleCheckDist))
        {
            if (!IsEnemy(hit.collider)) 
            {
                Vector3 rightDir = Vector3.Cross(Vector3.up, dir);
                
                if (!Physics.Raycast(transform.position, rightDir, obstacleCheckDist))
                {
                     dir = Vector3.Lerp(dir, rightDir, 0.7f).normalized;
                }
                else
                {
                     dir = Vector3.Lerp(dir, -rightDir, 0.7f).normalized;
                }
            }
        }

        float baseSpeed = (selfStats != null ? selfStats.movementSpeed : (ownerStats != null ? ownerStats.movementSpeed : 3f));
        float finalSpeed = baseSpeed * moveSpeedMultiplier;

        intendedVelocity = dir * finalSpeed;
    }

    private bool IsEnemy(Collider col)
    {
        return ((1 << col.gameObject.layer) & enemyLayer) != 0;
    }

    private void StopMoving()
    {
        intendedVelocity = Vector3.zero;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 0.2f, 0.15f);
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
        
        Gizmos.color = new Color(0.8f, 0.2f, 0.2f, 0.15f);
        float rangeToShow = Application.isPlaying ? optimalAttackRange : attackRange;
        Gizmos.DrawWireSphere(transform.position, rangeToShow);

        if (currentTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, currentTarget.position);
        }
    }

    private void OnDestroy()
    {
        StopMoving();
    }
}