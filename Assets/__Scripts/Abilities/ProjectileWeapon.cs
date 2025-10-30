using UnityEngine;
using Unity.Netcode;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(NetworkObject))]
public class ProjectileWeapon : NetworkBehaviour
{
    // --- STATS ---
    // These are set once on Initialize and do not change.
    private float damage;
    private bool wasCritical;
    private float speed;
    private float knockbackForce;
    private float lifetime;

    private Rigidbody rb;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = false;
        }
    }

    /// <summary>
    /// This single Initialize method works for both single-player and multiplayer.
    /// In SP, it's called by PlayerWeaponManager.
    /// In P2P, it's called by a ClientRpc from PlayerWeaponManager.
    /// </summary>
    public void Initialize(Transform targetEnemy, Vector3 initialDirection, float finalDamage, bool isCritical, float finalSpeed, float finalDuration, float finalKnockback, float finalSize)
    {
        this.damage = finalDamage;
        this.wasCritical = isCritical;
        this.speed = finalSpeed;
        this.knockbackForce = finalKnockback;
        this.lifetime = finalDuration > 0 ? finalDuration : 3f; // Use duration, fallback to 3s
        transform.localScale *= finalSize;

        if (rb != null)
        {
            rb.linearVelocity = initialDirection.normalized * speed;
        }
    }

    void Update()
    {
        // Only the server should control the lifetime of the projectile to ensure it disappears for everyone.
        if (!IsServer) return;

        lifetime -= Time.deltaTime;
        if (lifetime <= 0f)
        {
            // Destroy the object on the network.
            // A NetworkObject's destruction on the server is automatically synced to all clients.
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Handles hit detection. In multiplayer, this logic is server-authoritative.
    /// </summary>
    void OnTriggerEnter(Collider other)
    {
        // In P2P mode, only the server can register a hit. This prevents cheating.
        // In SP mode, IsServer is effectively true, so this logic runs correctly.
        if (!IsServer) return;

        if (other.CompareTag("Enemy") || other.CompareTag("Reaper"))
        {
            EnemyStats enemyStats = other.GetComponentInParent<EnemyStats>();
            if (enemyStats != null)
            {
                // The server applies the damage using the stats the projectile was spawned with.
                enemyStats.TakeDamage(damage, wasCritical);

                if (knockbackForce > 0 && !other.CompareTag("Reaper"))
                {
                    Vector3 knockbackDirection = (other.transform.position - transform.position).normalized;
                    knockbackDirection.y = 0;
                    enemyStats.ApplyKnockback(knockbackForce, 0.4f, knockbackDirection);
                }

                // The server destroys the projectile for everyone.
                Destroy(gameObject);
            }
        }
    }
}