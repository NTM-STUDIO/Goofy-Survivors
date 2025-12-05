using System;
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
    private float knockbackPenetration; // How much enemy knockback resistance to ignore (0-1)
    private float lifetime;

    private int sourceWeaponId = -1;
    private string sourceWeaponName;

    private Rigidbody rb;
    // Attacker attribution
    private ulong ownerNetId = 0; // used in MP on server to resolve attacker PlayerStats
    private PlayerStats ownerStats = null; // used in SP
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
    /// <param name="knockbackPen">How much of enemy knockback resistance to penetrate (0-1). Higher knockbackMultiplier = more penetration.</param>
    public void Initialize(Transform targetEnemy, Vector3 initialDirection, float finalDamage, bool isCritical, float finalSpeed, float finalDuration, float finalKnockback, float finalSize, float knockbackPen = 0f)
    {
        this.damage = finalDamage;
        this.wasCritical = isCritical;
        this.speed = finalSpeed;
        this.knockbackForce = finalKnockback;
        this.knockbackPenetration = knockbackPen;
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

    public void ConfigureSource(int weaponId, string weaponName)
    {
        sourceWeaponId = weaponId;
        sourceWeaponName = weaponName;
    }

    private string GetAbilityLabel()
    {
        if (!string.IsNullOrWhiteSpace(sourceWeaponName))
        {
            return sourceWeaponName;
        }

        return sourceWeaponId >= 0 ? $"Ability {sourceWeaponId}" : gameObject.name;
    }
    // Called on server after spawn (MP) to set the owner NetworkObject id
    public void SetOwnerNetworkId(ulong netId)
    {
        ownerNetId = netId;
    }

    // Called in SP local to attribute the attacker directly
    public void SetOwnerLocal(PlayerStats stats)
    {
        ownerStats = stats;
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
                PlayerStats attacker = null;
                if (ownerStats != null) attacker = ownerStats; // SP path
                else if (ownerNetId != 0 && NetworkManager != null && NetworkManager.SpawnManager != null)
                {
                    if (NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(ownerNetId, out var ownerNO))
                    {
                        attacker = ownerNO.GetComponent<PlayerStats>();
                    }
                }
                if (attacker != null)
                    enemyStats.TakeDamageFromAttacker(damage, wasCritical, attacker);
                else
                    enemyStats.TakeDamage(damage, wasCritical);

                if (attacker != null)
                {
                    string abilityLabel = GetAbilityLabel();
                    // Server registra localmente
                    AbilityDamageTracker.RecordDamage(abilityLabel, damage, gameObject, attacker);
                    // Notifica o client owner para registrar também
                    if (isNetworked)
                    {
                        attacker.RecordAbilityDamageClientRpc(abilityLabel, damage, gameObject.GetHashCode());
                    }
                }

                if (knockbackForce > 0 && !other.CompareTag("Reaper"))
                {
                    Vector3 knockbackDirection = (other.transform.position - transform.position).normalized;
                    knockbackDirection.y = 0;
                    // Pass knockback penetration to ignore enemy resistance
                    enemyStats.ApplyKnockback(knockbackForce, 0.4f, knockbackDirection, knockbackPenetration);
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