using UnityEngine;

public class OrbitingWeapon : MonoBehaviour
{
    // --- Stats ---
    public float damage;
    public float knockbackForce;

    // --- Inner radius settings ---
    [Range(0f, 1f)]
    [Tooltip("Controls the size of the safe zone as a RATIO of the total radius. " +
             "0 = No safe zone (hits everything inside). " +
             "0.8 = Safe zone is 80% of the radius (hits enemies in the outer 20%). " +
             "1 = Safe zone is 100% of the radius (hits nothing).")]
    public float innerRadiusRatio = 0.8f;

    [HideInInspector] public float rotationSpeed;
    [HideInInspector] public float orbitRadius;
    [HideInInspector] public Transform orbitCenter;

    private float size;

    private float currentAngle;
    private float lifetime;
    private float yOffset = 1.5f; // Store the height here for consistency

    // --- Initialize the weapon ---
    public void Initialize(Transform center, float startAngle, float finalDamage, float finalSpeed, float finalDuration, float finalKnockback, float finalSize)
    {
        orbitCenter = center;
        currentAngle = startAngle;
        damage = finalDamage;
        rotationSpeed = finalSpeed;
        lifetime = finalDuration;
        knockbackForce = finalKnockback;
        orbitRadius = finalSize * 16f;
        transform.localScale = Vector3.one * finalSize;
        size = finalSize;
    }

    void Update()
    {
        if (orbitCenter == null) { Destroy(gameObject); return; }

        lifetime -= Time.deltaTime;
        if (lifetime <= 0f) { Destroy(gameObject); return; }

        currentAngle += rotationSpeed * Time.deltaTime;
        if (currentAngle > 360f) currentAngle -= 360f;

        // The weapon orbits in a perfect circle in the world at the correct height
        float x = Mathf.Cos(currentAngle * Mathf.Deg2Rad) * orbitRadius;
        float z = Mathf.Sin(currentAngle * Mathf.Deg2Rad) * orbitRadius;
        transform.position = orbitCenter.position + new Vector3(x, yOffset * size + 1.5f, z);
    }

    // --- Trigger detection ---
void OnTriggerEnter(Collider other)
{
    // Return early if not an Enemy or Reaper
    if (!other.CompareTag("Enemy") && !other.CompareTag("Reaper")) return;

    // 1. Project to XZ plane for a 2D check
    Vector3 enemyPos2D = new Vector3(other.transform.position.x, 0, other.transform.position.z);
    Vector3 centerPos2D = new Vector3(orbitCenter.position.x, 0, orbitCenter.position.z);
    float distanceToCenter = Vector3.Distance(enemyPos2D, centerPos2D);

    // 2. Calculate the safe zone radius using the scalable RATIO
    float effectiveInnerRadius = orbitRadius * innerRadiusRatio;

    // 3. Only hit enemies outside the safe zone
    if (distanceToCenter >= effectiveInnerRadius)
    {
        EnemyStats enemyStats = other.GetComponentInParent<EnemyStats>();
        if (enemyStats != null)
        {
            enemyStats.TakeDamage((int)damage);

            // Only apply knockback if NOT a Reaper
            if (!other.CompareTag("Reaper"))
            {
                Vector3 knockbackDir = (other.transform.position - orbitCenter.position).normalized;
                knockbackDir.y = 0;
                enemyStats.ApplyKnockback(knockbackForce, 0.4f, knockbackDir);
            }
        }
    }
}

    // --- CORRECTED GIZMO ---
    // Now draws the spheres at the same height as the weapon's orbit.
    private void OnDrawGizmosSelected()
    {
        if (orbitCenter != null)
        {
            // Define the center point for the gizmos, including the correct height
            Vector3 gizmoCenter = orbitCenter.position + new Vector3(0, yOffset, 0);

            // Draw the outer orbit path (Red)
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(gizmoCenter, orbitRadius);

            // Calculate the inner radius using the ratio
            float effectiveInnerRadius = orbitRadius * innerRadiusRatio;

            // Draw the inner "safe zone" (Yellow)
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(gizmoCenter, effectiveInnerRadius);
        }
    }
}