using UnityEngine;

public class OrbitingWeapon : MonoBehaviour
{
    // --- Stats remain the same ---
    public float damage;
    public float knockbackForce;

    [HideInInspector] public float rotationSpeed;
    [HideInInspector] public float orbitRadius;
    [HideInInspector] public Transform orbitCenter;
    
    private float currentAngle;
    private float lifetime;

    // --- Initialize method is already compatible ---
    public void Initialize(Transform center, float startAngle, float finalDamage, float finalSpeed, float finalDuration, float finalKnockback, float finalSize)
    {
        this.orbitCenter = center;
        this.currentAngle = startAngle;
        
        this.damage = finalDamage;
        this.rotationSpeed = finalSpeed;
        this.lifetime = finalDuration;
        this.knockbackForce = finalKnockback;

        this.orbitRadius = finalSize * 10f; // Adjust multiplier as needed for 3D space
        transform.localScale = Vector3.one * finalSize;
    }

    void Update()
    {
        if (orbitCenter == null)
        {
            Destroy(gameObject);
            return;
        }

        lifetime -= Time.deltaTime;
        if (lifetime <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        currentAngle += rotationSpeed * Time.deltaTime;
        if (currentAngle > 360f) currentAngle -= 360f;

        // --- 3D Change: Orbit on the XZ plane instead of XY ---
        float x = Mathf.Cos(currentAngle * Mathf.Deg2Rad) * orbitRadius;
        float z = Mathf.Sin(currentAngle * Mathf.Deg2Rad) * orbitRadius; // Changed y to z
        transform.position = orbitCenter.position + new Vector3(x, 0, z); // Changed y component to 0
    }

    // --- 3D Change: Use 3D Trigger Collider ---
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            var enemyStats = other.GetComponent<EnemyStats>();
            if (enemyStats != null)
            {
                enemyStats.TakeDamage((int)damage);
                Debug.Log($"Orbiting weapon hit {other.name} for {damage} damage.");

                // --- 3D Change: Calculate knockback on the XZ plane ---
                Vector3 knockbackDirection = (other.transform.position - orbitCenter.position);
                knockbackDirection.y = 0; // Ensure knockback is horizontal
                knockbackDirection.Normalize();
                
                // Assumes your EnemyStats.ApplyKnockback now takes a Vector3
                enemyStats.ApplyKnockback(knockbackForce, 0.4f, knockbackDirection);
            }
        }
    }
}