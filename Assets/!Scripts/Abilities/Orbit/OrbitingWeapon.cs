using UnityEngine;

public class OrbitingWeapon : MonoBehaviour
{
    // --- Stats passed from WeaponData ---
    public float damage;
    public int pierceCount;
    public float knockbackForce; // Renamed from knockback for clarity
    
    [HideInInspector] public float rotationSpeed;
    [HideInInspector] public float orbitRadius;
    [HideInInspector] public Transform orbitCenter;

    // --- Private variables ---
    private float currentAngle;
    private int currentPierce;
    private float lifetime;

    public void Initialize(WeaponData data, Transform center, float startAngle)
    {
        this.damage = data.damage;
        this.pierceCount = data.pierceCount;
        this.rotationSpeed = data.speed;
        this.knockbackForce = data.knockback;
        this.orbitRadius = data.area * 4f;
        transform.localScale = Vector3.one * (data.area / 8f);
        this.orbitCenter = center;
        this.currentAngle = startAngle;
        this.lifetime = data.duration;
        currentPierce = pierceCount;
    }

    void Update()
    {
        if (orbitCenter == null) { Destroy(gameObject); return; }

        lifetime -= Time.deltaTime;
        if (lifetime <= 0f) { Destroy(gameObject); return; }

        currentAngle += rotationSpeed * Time.deltaTime;
        if (currentAngle > 360f) currentAngle -= 360f;

        float x = Mathf.Cos(currentAngle * Mathf.Deg2Rad) * orbitRadius;
        float y = Mathf.Sin(currentAngle * Mathf.Deg2Rad) * orbitRadius;
        transform.position = orbitCenter.position + new Vector3(x, y, 0);
    }

    // --- THIS IS THE CRITICAL PART ---
    void OnTriggerEnter2D(Collider2D other)
    {
        // STEP 1: Does this message appear at all?
        Debug.Log("OnTriggerEnter2D fired! Hit: " + other.name, gameObject);

        if (other.CompareTag("Enemy"))
        {
            // STEP 2: Does this message appear? (Means tag is correct)
            Debug.Log("Enemy tag confirmed for: " + other.name, gameObject);

            var enemyStats = other.GetComponent<EnemyStats>();
            if (enemyStats != null)
            {
                // STEP 3: Does this message appear? (Means script was found)
                Debug.Log("EnemyStats script found. Applying knockback with force: " + knockbackForce, gameObject);

                enemyStats.TakeDamage((int)damage);
                
                Vector2 knockbackDirection = (other.transform.position - orbitCenter.position).normalized;
                enemyStats.ApplyKnockback(knockbackForce, knockbackDirection);

                currentPierce--;
                if (currentPierce <= 0)
                {
                    // Destroy(gameObject);
                }
            }
        }
    }
}