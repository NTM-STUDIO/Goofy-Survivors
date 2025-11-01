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
            // Prevent random spinning/rotation drift across clients
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
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
        // If networking is not active, manage lifetime locally. If networked, server does it.
        bool isNetworked = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (isNetworked && !IsServer) return;

        lifetime -= Time.deltaTime;
        if (lifetime <= 0f)
        {
            // In MP server destroys for everyone; in SP local destroy is fine
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Handles hit detection. In multiplayer, this logic is server-authoritative.
    /// </summary>
    void OnTriggerEnter(Collider other)
    {
        // In MP, only the server should register hits. In SP, allow local hits.
        bool isNetworked = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (isNetworked && !IsServer) return;

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