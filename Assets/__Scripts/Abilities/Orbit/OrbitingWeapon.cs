using UnityEngine;
using System.Collections.Generic;

public class OrbitingWeapon : MonoBehaviour
{
    // --- NEW: References to live data ---
    private PlayerStats playerStats;
    private WeaponData weaponData;
    
    // --- Stats (no longer set directly) ---
    private float knockbackForce;

    [Range(0f, 1f)]
    [Tooltip("Controls the size of the safe zone as a RATIO of the total radius.")]
    public float innerRadiusRatio = 0.8f;

    [HideInInspector] public float rotationSpeed;
    [HideInInspector] public float orbitRadius;
    [HideInInspector] public Transform orbitCenter;

    private float size;
    private float currentAngle;
    private float lifetime;
    private float yOffset = 1.5f;

    private HashSet<GameObject> hitEnemies = new HashSet<GameObject>();
    private float hitResetTime = 1.0f;
    private float nextResetTime;

    // --- MODIFIED: Initialize now takes references, not pre-calculated values ---
    public void Initialize(Transform center, float startAngle, PlayerStats stats, WeaponData data, float finalSpeed, float finalDuration, float finalKnockback, float finalSize)
    {
        orbitCenter = center;
        currentAngle = startAngle;
        
        // Store the references to calculate damage on hit
        playerStats = stats;
        weaponData = data;

        rotationSpeed = finalSpeed;
        lifetime = finalDuration;
        knockbackForce = finalKnockback;
        orbitRadius = finalSize * 16f;
        transform.localScale = Vector3.one * finalSize;
        size = finalSize;

        nextResetTime = Time.time + hitResetTime;
    }

    void Update()
    {
        if (orbitCenter == null) { Destroy(gameObject); return; }

        lifetime -= Time.deltaTime;
        if (lifetime <= 0f) { Destroy(gameObject); return; }

        currentAngle += rotationSpeed * Time.deltaTime;
        if (currentAngle > 360f) currentAngle -= 360f;

        float x = Mathf.Cos(currentAngle * Mathf.Deg2Rad) * orbitRadius;
        float z = Mathf.Sin(currentAngle * Mathf.Deg2Rad) * orbitRadius;
        transform.position = orbitCenter.position + new Vector3(x, yOffset * size + 1.5f, z);

        if (Time.time >= nextResetTime)
        {
            hitEnemies.Clear();
            nextResetTime = Time.time + hitResetTime;
        }
    }

    // --- MODIFIED: Trigger detection now calculates its own damage and crit chance on every hit ---
    void OnTriggerEnter(Collider other)
    {
        // Must have valid references to calculate damage
        if (playerStats == null || weaponData == null) return;

        GameObject enemyObject = other.GetComponentInParent<EnemyStats>()?.gameObject;

        if (enemyObject == null || hitEnemies.Contains(enemyObject))
        {
            return;
        }

        EnemyStats enemyStats = enemyObject.GetComponent<EnemyStats>();

        Vector3 enemyPos2D = new Vector3(other.transform.position.x, 0, other.transform.position.z);
        Vector3 centerPos2D = new Vector3(orbitCenter.position.x, 0, orbitCenter.position.z);
        float distanceToCenter = Vector3.Distance(enemyPos2D, centerPos2D);

        float effectiveInnerRadius = orbitRadius * innerRadiusRatio;

        if (distanceToCenter >= effectiveInnerRadius)
        {
            hitEnemies.Add(enemyObject);

            // --- CRITICAL STRIKE CALCULATION ON HIT ---
            // Perform a new, independent damage calculation for this specific hit.
            DamageResult damageResult = playerStats.CalculateDamage(weaponData.damage);

            // Pass the final damage and the crit status to the enemy.
            enemyStats.TakeDamage(damageResult.damage, damageResult.isCritical);

            if (!enemyObject.CompareTag("Reaper"))
            {
                Vector3 knockbackDir = (other.transform.position - orbitCenter.position).normalized;
                knockbackDir.y = 0;
                enemyStats.ApplyKnockback(knockbackForce, 0.4f, knockbackDir);
            }
        }
    }

    // --- GIZMO ---
    private void OnDrawGizmosSelected()
    {
        if (orbitCenter != null)
        {
            Vector3 gizmoCenter = orbitCenter.position + new Vector3(0, yOffset, 0);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(gizmoCenter, orbitRadius);

            float effectiveInnerRadius = orbitRadius * innerRadiusRatio;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(gizmoCenter, effectiveInnerRadius);
        }
    }
}