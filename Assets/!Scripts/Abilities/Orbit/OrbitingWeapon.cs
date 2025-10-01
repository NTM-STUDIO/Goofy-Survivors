using UnityEngine;

public class OrbitingWeapon : MonoBehaviour
{
    // --- Stats passed from WeaponData ---
    public float damage;
    public int pierceCount;
    // We will set these from the controller that spawns this weapon
    [HideInInspector] public float rotationSpeed;
    [HideInInspector] public float orbitRadius;
    [HideInInspector] public Transform orbitCenter; // The player to orbit around

    // --- Private variables ---
    private float currentAngle; // The weapon's current angle in the orbit
    private int currentPierce;
    private float lifetime;

    /// <summary>
    /// Initializes the orbiting weapon's starting position and stats.
    /// This is called by the WeaponController that creates it.
    /// </summary>
    public void Initialize(WeaponData data, Transform center, float startAngle)
    {
        // Set stats from the Scriptable Object
        this.damage = data.damage;
        this.pierceCount = data.pierceCount;
        this.rotationSpeed = data.speed;

        // The 'area' stat now controls both the orbit radius AND the visual scale
        this.orbitRadius = data.area * 4f; // Adjust multiplier as needed for gameplay feel
        transform.localScale = Vector3.one * (data.area / 8f);

        // Set orbital parameters
        this.orbitCenter = center;
        this.currentAngle = startAngle;

        // Set the lifetime from the weapon's duration stat
        this.lifetime = data.duration;

        // Reset pierce count for this attack instance
        currentPierce = pierceCount;
    }

    void Update()
    {
        if (orbitCenter == null)
        {
            // If the center is gone (e.g., player died), destroy the weapon
            Destroy(gameObject);
            return;
        }

        // Tick down the lifetime timer. If it runs out, destroy the object.
        lifetime -= Time.deltaTime;
        if (lifetime <= 0f)
        {
            Destroy(gameObject);
            return; // Stop executing the rest of the Update method
        }

        // 1. Increment the angle based on rotation speed
        currentAngle += rotationSpeed * Time.deltaTime;
        if (currentAngle > 360f)
        {
            currentAngle -= 360f;
        }

        // 2. Calculate the new position using trigonometry
        float x = Mathf.Cos(currentAngle * Mathf.Deg2Rad) * orbitRadius;
        // --- THIS IS THE CORRECTED LINE ---
        float y = Mathf.Sin(currentAngle * Mathf.Deg2Rad) * orbitRadius;

        // 3. Set the new position relative to the orbit center
        transform.position = orbitCenter.position + new Vector3(x, y, 0);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Enemy"))
        {
            var enemyHealth = other.GetComponent<EnemyStats>(); // Replace with your actual enemy health script
            if (enemyHealth != null)
            {
                enemyHealth.Die(); // Or apply damage if you have a damage method

                // Handle piercing
                currentPierce--;
                if (currentPierce <= 0)
                {
                    // For a persistent weapon like King Bible, we usually don't destroy it.
                    // It would get destroyed when the weapon is removed or upgraded.
                }
            }
        }
    }
}