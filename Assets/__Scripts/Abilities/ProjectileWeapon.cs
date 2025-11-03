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
    [Header("Visual")]
    [SerializeField] private Transform visual; // rotate this child on Z only
    [Tooltip("Degrees to add to the computed Z angle (use 180 if the visual is inverted, 90/-90 for sideways sprites)")]
    [SerializeField] private float visualZAngleOffset = 180f;
    private Vector3 visualBaseEuler; // preserve X/Y (e.g., 30x,45y) and base Z
    private Vector3 moveDir;

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

        // Auto-assign a visual child if not explicitly set
        if (visual == null)
        {
            if (transform.childCount > 0)
            {
                visual = transform.GetChild(0);
            }
            else
            {
                var sr = GetComponentInChildren<SpriteRenderer>();
                if (sr != null) visual = sr.transform;
                else
                {
                    var mr = GetComponentInChildren<MeshRenderer>();
                    if (mr != null) visual = mr.transform;
                }
            }
        }

        // Capture base local euler so we can preserve X and Y (e.g., 30x,45y) and only change Z at runtime
        if (visual != null)
        {
            visualBaseEuler = visual.localEulerAngles;
        }
        else
        {
            // Fallback to common isometric tilt if no visual child exists yet
            visualBaseEuler = new Vector3(30f, 45f, 0f);
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
        moveDir = initialDirection.normalized;

        if (rb != null)
        {
            rb.linearVelocity = moveDir * speed;
            rb.angularVelocity = Vector3.zero;
        }

        // Z-only orientation (2D-style) applied to the visual child only
        ApplyZOnlyRotation(moveDir);
    }

    void Update()
    {
        // If networking is not active, manage lifetime locally. If networked, server does it.
        bool isNetworked = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (isNetworked && !IsServer) return;

        lifetime -= Time.deltaTime;
        if (lifetime <= 0f)
        {
            // In MP the server should despawn the NetworkObject; in SP local destroy is fine
            var no = GetComponent<NetworkObject>();
            if (isNetworked && no != null && no.IsSpawned)
            {
                no.Despawn(true);
            }
            else
            {
                Destroy(gameObject);
            }
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

                // The server despawns the projectile for everyone when networked; otherwise destroy locally.
                var no = GetComponent<NetworkObject>();
                if (isNetworked && no != null && no.IsSpawned)
                {
                    no.Despawn(true);
                }
                else
                {
                    Destroy(gameObject);
                }
            }
        }
    }

    private void ApplyZOnlyRotation(Vector3 worldDir)
    {
        // Map XZ (x,z) into XY (x,y) to compute Z rotation angle
        Vector2 v = new Vector2(worldDir.x, worldDir.z);
        if (v.sqrMagnitude < 0.000001f) return;
        float angle = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg + visualZAngleOffset;
        if (visual != null)
        {
            visual.localRotation = Quaternion.Euler(visualBaseEuler.x, visualBaseEuler.y, angle);
        }
    }
}