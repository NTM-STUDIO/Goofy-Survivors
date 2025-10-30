using UnityEngine;
using Unity.Netcode; // This is needed for NetworkBehaviour
using System.Collections.Generic;

[RequireComponent(typeof(NetworkObject))]
// THE FIX IS ON THIS LINE: It must inherit from NetworkBehaviour, not MonoBehaviour
public class OrbitingWeapon : NetworkBehaviour 
{
    // --- Live Data References ---
    private PlayerStats playerStats;
    private WeaponData weaponData;
    
    // --- Calculated Stats ---
    private float knockbackForce;
    private float rotationSpeed;
    private float orbitRadius;
    private Transform orbitCenter;
    private float lifetime;
    private float currentAngle;

    // This flag is set on initialization to tell the script which rules to follow.
    private bool isP2P = false;

    [Header("Weapon Settings")]
    [Range(0f, 1f)]
    [Tooltip("Controls the size of the safe zone as a RATIO of the total radius.")]
    public float innerRadiusRatio = 0.8f;
    private float yOffset = 1.5f;

    // --- Hit Detection ---
    private HashSet<GameObject> hitEnemies = new HashSet<GameObject>();
    private float hitResetTime = 1.0f;
    private float nextResetTime;

    #region Initialization
    public void LocalInitialize(Transform center, float startAngle, PlayerStats stats, WeaponData data)
    {
        this.isP2P = false;
        this.orbitCenter = center;
        this.currentAngle = startAngle;
        this.playerStats = stats;
        this.weaponData = data;
        CalculateStats();
    }

    public void NetworkInitialize(NetworkObjectReference ownerRef, WeaponData data, float startAngle)
    {
        this.isP2P = true;
        this.weaponData = data;
        this.currentAngle = startAngle;

        if (ownerRef.TryGet(out NetworkObject ownerNetObj))
        {
            this.orbitCenter = ownerNetObj.transform;
            this.playerStats = ownerNetObj.GetComponent<PlayerStats>();
            CalculateStats();
        }
        else
        {
            if(IsServer) Destroy(gameObject);
        }
    }

    private void CalculateStats()
    {
        if (playerStats == null || weaponData == null) return;
        
        float finalSize = weaponData.area * playerStats.projectileSizeMultiplier;
        rotationSpeed = weaponData.speed * playerStats.projectileSpeedMultiplier;
        lifetime = weaponData.duration * playerStats.durationMultiplier;
        knockbackForce = weaponData.knockback * playerStats.knockbackMultiplier;
        orbitRadius = finalSize * 16f;
        transform.localScale = Vector3.one * finalSize;
        nextResetTime = Time.time + hitResetTime;
    }
    #endregion

    void Update()
    {
        if (orbitCenter == null) 
        { 
            if (!isP2P || IsServer) Destroy(gameObject);
            return;
        }

        lifetime -= Time.deltaTime;
        if (lifetime <= 0f) 
        {
            if (!isP2P || IsServer)
            {
                // In P2P, the Host is the Server, so this will correctly destroy the object.
                // In SP, !isP2P is true, so this will correctly destroy the object.
                Destroy(gameObject);
            }
            return; 
        }

        currentAngle += rotationSpeed * Time.deltaTime;
        if (currentAngle > 360f) currentAngle -= 360f;

        float x = Mathf.Cos(currentAngle * Mathf.Deg2Rad) * orbitRadius;
        float z = Mathf.Sin(currentAngle * Mathf.Deg2Rad) * orbitRadius;
        float finalYOffset = yOffset * transform.localScale.y + 1.5f;
        transform.position = orbitCenter.position + new Vector3(x, finalYOffset, z);

        if (Time.time >= nextResetTime)
        {
            hitEnemies.Clear();
            nextResetTime = Time.time + hitResetTime;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!isP2P || IsServer)
        {
            if (playerStats == null || weaponData == null) return;

            EnemyStats enemyStats = other.GetComponentInParent<EnemyStats>();
            if (enemyStats == null || hitEnemies.Contains(enemyStats.gameObject)) return;

            Vector3 enemyPos2D = new Vector3(other.transform.position.x, 0, other.transform.position.z);
            Vector3 centerPos2D = new Vector3(orbitCenter.position.x, 0, orbitCenter.position.z);
            float distanceToCenter = Vector3.Distance(enemyPos2D, centerPos2D);
            float effectiveInnerRadius = orbitRadius * innerRadiusRatio;

            if (distanceToCenter >= effectiveInnerRadius)
            {
                hitEnemies.Add(enemyStats.gameObject);
                DamageResult damageResult = playerStats.CalculateDamage(weaponData.damage);
                enemyStats.TakeDamage(damageResult.damage, damageResult.isCritical);

                if (!enemyStats.CompareTag("Reaper"))
                {
                    Vector3 knockbackDir = (other.transform.position - orbitCenter.position).normalized;
                    knockbackDir.y = 0;
                    enemyStats.ApplyKnockback(knockbackForce, 0.4f, knockbackDir);
                }
            }
        }
    }

    #region Gizmos
    private void OnDrawGizmosSelected()
    {
        Transform center = orbitCenter != null ? orbitCenter : transform.parent;
        if (center != null)
        {
            Vector3 gizmoCenter = center.position + new Vector3(0, yOffset, 0);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(gizmoCenter, orbitRadius);
            float effectiveInnerRadius = orbitRadius * innerRadiusRatio;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(gizmoCenter, effectiveInnerRadius);
        }
    }
    #endregion
}