using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

public class MeleePitchfork : NetworkBehaviour
{
    [Header("References")]
    [Tooltip("ARRASTA O OBJETO FILHO (Visuals) PARA AQUI!")]
    [SerializeField] private Transform visualTransform; 

    [Header("Settings")]
    [SerializeField] private float stabDistance = 2.5f; 
    [SerializeField] private AnimationCurve stabCurve = new AnimationCurve(new Keyframe(0, 0), new Keyframe(0.5f, 1), new Keyframe(1, 0));
    [SerializeField] private float rotationSpeed = 10f;

    [Header("Fixed Rotation (X and Y are locked)")]
    [SerializeField] private float fixedXRotation = 30f;
    [SerializeField] private float fixedYRotation = 45f;

    private PlayerStats ownerStats;
    private WeaponData weaponData;
    private HashSet<GameObject> hitEnemiesThisStab = new HashSet<GameObject>(); 
    private bool isSinglePlayer;
    private bool isInitialized = false;
    private float sizeScale = 1f;
    private float stabDuration = 0.3f;
    private float finalDistance = 2.5f;
    private float currentZAngle = 0f;  // Only Z rotates to aim

    public void Initialize(Vector3 direction, PlayerStats stats, WeaponData data)
    {
        ownerStats = stats;
        weaponData = data;
        isSinglePlayer = (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening);

        if (visualTransform == null) 
        {
            if (transform.childCount > 0) visualTransform = transform.GetChild(0);
            else visualTransform = transform;
        }

        // Calculate scale based on stats
        sizeScale = weaponData.area * (stats != null ? stats.projectileSizeMultiplier : 1f);
        visualTransform.localScale = Vector3.one * sizeScale;

        // Calculate attack duration based on attack speed (faster attack speed = faster stab)
        float speedMult = (stats != null) ? Mathf.Max(0.1f, stats.attackSpeedMultiplier) : 1f;
        stabDuration = weaponData.duration / speedMult;
        finalDistance = stabDistance * sizeScale;

        // Initial Z angle from direction (X and Y are fixed)
        if (direction != Vector3.zero)
        {
            currentZAngle = Mathf.Atan2(direction.z, direction.x) * Mathf.Rad2Deg;
        }
        ApplyRotation();

        isInitialized = true;
        
        // Start infinite attack loop
        StartCoroutine(InfiniteAttackLoop());
    }

    /// <summary>
    /// Apply rotation with fixed X=30, Y=45, and only Z rotating to aim at enemies
    /// </summary>
    private void ApplyRotation()
    {
        if (visualTransform != null)
        {
            visualTransform.localRotation = Quaternion.Euler(fixedXRotation, fixedYRotation, currentZAngle);
        }
    }

    private void Update()
    {
        if (!isInitialized || visualTransform == null) return;

        // Find closest enemy and smoothly rotate Z to aim at them
        Transform closestEnemy = FindClosestEnemy();
        
        if (closestEnemy != null)
        {
            Vector3 dirToEnemy = closestEnemy.position - transform.position;
            dirToEnemy.y = 0;
            
            if (dirToEnemy.sqrMagnitude > 0.01f)
            {
                float targetZAngle = Mathf.Atan2(dirToEnemy.z, dirToEnemy.x) * Mathf.Rad2Deg;
                currentZAngle = Mathf.LerpAngle(currentZAngle, targetZAngle, Time.deltaTime * rotationSpeed);
                ApplyRotation();
            }
        }
        else
        {
            // No enemies - aim in player's movement direction
            if (ownerStats != null && ownerStats.TryGetComponent<Rigidbody>(out var rb))
            {
                Vector3 vel = rb.linearVelocity;
                vel.y = 0;
                if (vel.sqrMagnitude > 0.1f)
                {
                    float targetZAngle = Mathf.Atan2(vel.z, vel.x) * Mathf.Rad2Deg;
                    currentZAngle = Mathf.LerpAngle(currentZAngle, targetZAngle, Time.deltaTime * rotationSpeed);
                    ApplyRotation();
                }
            }
        }
    }

    private Transform FindClosestEnemy()
    {
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        if (enemies.Length == 0) return null;

        Transform closest = null;
        float minDist = float.MaxValue;
        Vector3 myPos = transform.position;

        foreach (GameObject enemy in enemies)
        {
            if (enemy == null) continue;
            float dist = (enemy.transform.position - myPos).sqrMagnitude;
            if (dist < minDist)
            {
                minDist = dist;
                closest = enemy.transform;
            }
        }

        return closest;
    }

    private IEnumerator InfiniteAttackLoop()
    {
        while (true)
        {
            // Wait for cooldown (uses CDR to reduce cooldown)
            float cdr = (ownerStats != null) ? ownerStats.cooldownReduction : 0f;
            float cooldown = weaponData.cooldown * (1f - Mathf.Clamp(cdr, 0f, 0.9f));
            yield return new WaitForSeconds(Mathf.Max(0.1f, cooldown));

            // Clear hit list for this stab
            hitEnemiesThisStab.Clear();

            // Execute stab animation
            yield return StartCoroutine(StabRoutine());
        }
    }

    private IEnumerator StabRoutine()
    {
        float timer = 0f;
        Vector3 startLocalPos = Vector3.zero;

        while (timer < stabDuration)
        {
            timer += Time.deltaTime;
            float progress = timer / stabDuration;
            float curveVal = stabCurve.Evaluate(progress);

            // Calculate stab direction based on current Z angle (in world XZ plane)
            float zRad = currentZAngle * Mathf.Deg2Rad;
            Vector3 stabDir = new Vector3(Mathf.Cos(zRad), 0, Mathf.Sin(zRad));
            
            // Move visual in stab direction
            visualTransform.localPosition = startLocalPos + (stabDir * curveVal * finalDistance);

            yield return null;
        }

        // Return to start position
        visualTransform.localPosition = startLocalPos;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (weaponData == null || ownerStats == null) return;
        if (hitEnemiesThisStab.Contains(other.gameObject)) return;

        if (other.CompareTag("Enemy"))
        {
            var enemy = other.GetComponent<EnemyStats>();
            if (enemy != null)
            {
                hitEnemiesThisStab.Add(other.gameObject);
                DamageResult dmg = ownerStats.CalculateDamage(weaponData.damage);

                if (isSinglePlayer || IsServer)
                {
                    enemy.TakeDamageFromAttacker(dmg.damage, dmg.isCritical, ownerStats);
                    
                    // Knockback in the direction the weapon is aiming (based on Z angle)
                    float zRad = currentZAngle * Mathf.Deg2Rad;
                    Vector3 knockbackDir = new Vector3(Mathf.Cos(zRad), 0, Mathf.Sin(zRad));
                    
                    float kb = weaponData.knockback * ownerStats.knockbackMultiplier;
                    float knockbackPen = Mathf.Clamp01((ownerStats.knockbackMultiplier - 1f) * 0.5f);
                    if (kb > 0) enemy.ApplyKnockback(kb, 0.2f, knockbackDir, knockbackPen);
                }
            }
        }
    }
}